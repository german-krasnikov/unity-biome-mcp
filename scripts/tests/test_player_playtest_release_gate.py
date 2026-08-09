from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.release_gate import GateError, validate_release_gate  # noqa: E402
from player_playtest_gate_test_support import (  # noqa: E402
    player_evidence_paths,
    write_player_playtest_evidence,
)
from release_gate_test_support import prepare_bundle, read_head, validate_bundle  # noqa: E402


def test_release_gate_accepts_exact_player_playtest_evidence_set(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)

    validate_bundle(paths)


def test_release_gate_requires_player_playtest_evidence(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)

    with pytest.raises(GateError, match="Player PlayTest"):
        validate_release_gate(
            policy_path=paths["policy"],
            source_root=paths["source_root"],
            artifact_manifest_path=paths["manifest"],
            artifact_root=paths["artifact_root"],
            evidence_paths=(paths["evidence"],),
            expected_head_sha=read_head(paths),
        )


def test_release_gate_requires_all_player_playtest_matrices(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)

    with pytest.raises(GateError, match="matrix"):
        validate_release_gate(
            policy_path=paths["policy"],
            source_root=paths["source_root"],
            artifact_manifest_path=paths["manifest"],
            artifact_root=paths["artifact_root"],
            evidence_paths=(paths["evidence"],),
            player_playtest_evidence_paths=player_evidence_paths(paths)[:2],
            expected_head_sha=read_head(paths),
        )


def test_release_gate_rejects_player_playtest_head_mismatch(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    write_player_playtest_evidence(
        paths["player_evidence_linux"],
        head_sha="b" * 40,
        matrix="Linux",
    )

    with pytest.raises(GateError, match="head"):
        validate_bundle(paths)


def test_release_gate_rejects_player_playtest_contract_drift(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    evidence = json.loads(paths["player_evidence_linux"].read_text(encoding="utf-8"))
    evidence["receipts"]["success"]["steps"] = 13
    evidence["evidence_sha256"] = "0" * 64
    paths["player_evidence_linux"].write_text(json.dumps(evidence), encoding="utf-8")

    with pytest.raises(GateError, match="hash|success"):
        validate_bundle(paths)
