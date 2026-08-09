from __future__ import annotations

import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]

sys.path.insert(0, str(REPO_ROOT / "scripts"))

from gauntlet.player_playtest_evidence import (  # noqa: E402
    PlayerPlaytestEvidenceError,
    build_player_playtest_evidence,
)

HEAD_SHA = "a" * 40
SUCCESS_STEPS = [
    "LOG Player PlayTest CI GridDemo smoke started",
    "TIMESCALE 10",
    "INVOKE /GridPlayer GridPlayer ResetState",
    "SET /GridPlayer GridPlayer MoveSpeed 50",
    "ASSERT /GridPlayer|GridPlayer|StateText contains pos=0,0",
    "INVOKE /GridPlayer GridPlayer Move north",
    "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 3",
    "ASSERT /GridPlayer|GridPlayer|PosZ == 1",
    "ASSERT /GridPlayer|GridPlayer|MoveCount >= 1",
    "ASSERT /GridPlayer|GridPlayer|StateText contains pos=0,1",
    "ASSERT /GridPlayer|GridPlayer|BoardText contains P",
    "SNAPSHOT /GridPlayer|GridPlayer|StateText,/GridPlayer|GridPlayer|BoardText",
    "TIMESCALE 1",
    "ASSERT_CONSOLE_CLEAN",
]
FAILURE_STEPS = [
    "LOG Player PlayTest CI expected failure started",
    "INVOKE /GridPlayer GridPlayer ResetState",
    "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
    "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
    "ASSERT_CONSOLE_CLEAN",
]
GRAPHICS_STEPS = [
    "LOG Player PlayTest optional graphics smoke started",
    "WAIT_UNTIL /Main Camera|activeInHierarchy == True TIMEOUT 5",
    "ASSERT /Main Camera|Camera|enabled == True",
    "ASSERT_CONSOLE_CLEAN",
]


def _write_json(path: Path, steps: list[str], failed_raw: set[str]) -> None:
    payload = {
        "schema_version": 1,
        "passed": len(steps) - len(failed_raw),
        "failed": len(failed_raw),
        "duration_seconds": 0.25,
        "steps": [
            {
                "raw": raw,
                "passed": raw not in failed_raw,
                "message": "ok" if raw not in failed_raw else "expected failure",
            }
            for raw in steps
        ],
    }
    path.write_text(json.dumps(payload), encoding="utf-8")


def _write_junit(path: Path, steps: list[str], failed_raw: set[str]) -> None:
    root = ET.Element(
        "testsuite",
        {
            "name": "UnityMCP.PlayerPlaytest",
            "tests": str(len(steps)),
            "failures": str(len(failed_raw)),
        },
    )
    for raw in steps:
        case = ET.SubElement(root, "testcase", {"name": raw})
        if raw in failed_raw:
            ET.SubElement(case, "failure", {"message": "expected failure"})
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


def _write_receipt_pair(root: Path, stem: str, steps: list[str], failed_raw: set[str]) -> None:
    _write_json(root / f"{stem}.json", steps, failed_raw)
    _write_junit(root / f"{stem}.xml", steps, failed_raw)


def _stage_artifacts(tmp_path: Path) -> tuple[Path, Path]:
    artifacts = tmp_path / "artifacts"
    artifacts.mkdir()
    player = tmp_path / "player"
    player.mkdir()
    (player / "UnityMCP").write_bytes(b"player executable")
    (player / "Data").mkdir()
    (player / "Data" / "globalgamemanagers").write_bytes(b"data")
    (artifacts / "player-build.log").write_text("build ok", encoding="utf-8")
    _write_receipt_pair(artifacts, "player-playtest", SUCCESS_STEPS, set())
    _write_receipt_pair(
        artifacts,
        "player-playtest-failure",
        FAILURE_STEPS,
        {
            "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
            "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
        },
    )
    return player, artifacts


def test_builds_exact_player_playtest_evidence(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)

    evidence = build_player_playtest_evidence(
        head_sha=HEAD_SHA,
        run_id="123",
        run_attempt="2",
        runner_os="Linux",
        matrix_name="Linux",
        player_path=player,
        artifacts_dir=artifacts,
    )

    assert evidence["schema_version"] == 1
    assert evidence["head_sha"] == HEAD_SHA
    assert evidence["github"]["run_id"] == "123"
    assert evidence["player"]["kind"] == "directory"
    assert evidence["player"]["file_count"] == 2
    assert evidence["receipts"]["success"]["steps"] == 14
    assert evidence["receipts"]["success"]["failed"] == 0
    assert evidence["receipts"]["expected_failure"]["failed"] == 2
    assert evidence["receipts"]["expected_failure"]["failed_steps"] == [
        "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
        "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
    ]
    assert evidence["receipts"]["graphics"] is None
    assert len(evidence["evidence_sha256"]) == 64


def test_player_tree_digest_changes_when_build_output_changes(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    first = build_player_playtest_evidence(
        head_sha=HEAD_SHA,
        run_id="123",
        run_attempt="1",
        runner_os="Linux",
        matrix_name="Linux",
        player_path=player,
        artifacts_dir=artifacts,
    )

    (player / "Data" / "globalgamemanagers").write_bytes(b"changed")
    second = build_player_playtest_evidence(
        head_sha=HEAD_SHA,
        run_id="123",
        run_attempt="1",
        runner_os="Linux",
        matrix_name="Linux",
        player_path=player,
        artifacts_dir=artifacts,
    )

    assert second["player"]["sha256"] != first["player"]["sha256"]


def test_rejects_success_receipt_with_wrong_step_count(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    _write_receipt_pair(artifacts, "player-playtest", SUCCESS_STEPS[:-1], set())

    with pytest.raises(PlayerPlaytestEvidenceError, match="success PlayTest step count"):
        build_player_playtest_evidence(
            head_sha=HEAD_SHA,
            run_id="123",
            run_attempt="1",
            runner_os="Linux",
            matrix_name="Linux",
            player_path=player,
            artifacts_dir=artifacts,
        )


def test_rejects_expected_failure_that_passed(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    _write_receipt_pair(artifacts, "player-playtest-failure", FAILURE_STEPS, set())

    with pytest.raises(PlayerPlaytestEvidenceError, match="expected-failure PlayTest"):
        build_player_playtest_evidence(
            head_sha=HEAD_SHA,
            run_id="123",
            run_attempt="1",
            runner_os="Linux",
            matrix_name="Linux",
            player_path=player,
            artifacts_dir=artifacts,
        )


def test_includes_optional_graphics_receipt_when_present(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    _write_receipt_pair(artifacts, "player-playtest-graphics", GRAPHICS_STEPS, set())

    evidence = build_player_playtest_evidence(
        head_sha=HEAD_SHA,
        run_id="123",
        run_attempt="1",
        runner_os="macOS",
        matrix_name="macOS",
        player_path=player,
        artifacts_dir=artifacts,
    )

    assert evidence["receipts"]["graphics"]["steps"] == 4
    assert evidence["receipts"]["graphics"]["failed"] == 0


def test_rejects_missing_receipt_file(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    (artifacts / "player-playtest.xml").unlink()

    with pytest.raises(PlayerPlaytestEvidenceError, match="receipt file"):
        build_player_playtest_evidence(
            head_sha=HEAD_SHA,
            run_id="123",
            run_attempt="1",
            runner_os="Linux",
            matrix_name="Linux",
            player_path=player,
            artifacts_dir=artifacts,
        )


def test_cli_writes_evidence_file(tmp_path: Path) -> None:
    player, artifacts = _stage_artifacts(tmp_path)
    output = artifacts / "player-playtest-evidence.json"

    result = subprocess.run(
        [
            sys.executable,
            str(REPO_ROOT / "scripts" / "write_player_playtest_evidence.py"),
            "--head-sha",
            HEAD_SHA,
            "--run-id",
            "123",
            "--run-attempt",
            "1",
            "--runner-os",
            "Linux",
            "--matrix-name",
            "Linux",
            "--player-path",
            str(player),
            "--artifacts-dir",
            str(artifacts),
            "--out",
            str(output),
        ],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    evidence = json.loads(output.read_text(encoding="utf-8"))
    assert evidence["evidence_sha256"]
    assert evidence["receipts"]["success"]["steps"] == 14
