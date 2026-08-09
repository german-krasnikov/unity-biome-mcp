from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from player_playtest_gate_test_support import player_evidence_paths  # noqa: E402
from release_gate_test_support import VERSION, prepare_bundle, read_head  # noqa: E402

CLI = SCRIPTS / "validate_release_evidence.py"


def test_release_gate_cli_is_nonzero_for_missing_evidence(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    command = [
        sys.executable,
        str(CLI),
        "--policy",
        str(paths["policy"]),
        "--source-root",
        str(paths["source_root"]),
        "--artifact-manifest",
        str(paths["manifest"]),
        "--artifact-root",
        str(paths["artifact_root"]),
        "--head-sha",
        read_head(paths),
    ]

    failed = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", timeout=30)
    passed = subprocess.run(
        [
            *command,
            "--evidence",
            str(paths["evidence"]),
            *[
                item
                for player_path in player_evidence_paths(paths)
                for item in ("--player-playtest-evidence", str(player_path))
            ],
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=30,
    )

    assert failed.returncode == 1
    assert "FAIL" in failed.stderr
    assert passed.returncode == 0
    assert "PASS" in passed.stdout
    assert f"product={VERSION}" in passed.stdout
    assert "player_playtest=3" in passed.stdout
