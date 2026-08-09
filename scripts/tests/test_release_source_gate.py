"""Adversarial source and policy binding tests for the release gate."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.release_gate import GateError  # noqa: E402
from release_gate_test_support import prepare_bundle, validate_bundle  # noqa: E402


def test_release_gate_rejects_dirty_or_wrong_source_checkout(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    policy = json.loads(paths["policy"].read_text(encoding="utf-8"))
    policy["policy_version"] = "substituted"
    paths["policy"].write_text(json.dumps(policy), encoding="utf-8")

    with pytest.raises(GateError, match="tracked worktree"):
        validate_bundle(paths)

    paths = prepare_bundle(tmp_path / "wrong-head")
    with pytest.raises(GateError, match="HEAD"):
        validate_bundle(paths, expected_head_sha="0" * 40)


def test_release_gate_rejects_symlinked_policy_entrypoint(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    outside = tmp_path / "outside-policy.json"
    outside.write_bytes(paths["policy"].read_bytes())
    paths["policy"].unlink()
    try:
        paths["policy"].symlink_to(outside)
    except OSError as exc:
        pytest.skip(f"symlinks are unavailable on this platform: {exc}")

    with pytest.raises(GateError, match="inside the source root|regular file"):
        validate_bundle(paths)


def test_release_gate_rejects_tracked_catalog_not_bound_by_policy(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    policy = json.loads(paths["policy"].read_text(encoding="utf-8"))
    policy["contract_catalog_sha"] = "0" * 64
    paths["policy"].write_text(json.dumps(policy), encoding="utf-8")
    subprocess.run(
        ["git", "-C", str(paths["source_root"]), "add", str(paths["policy"])],
        check=True,
    )
    subprocess.run(
        ["git", "-C", str(paths["source_root"]), "commit", "--amend", "--no-edit", "-q"],
        check=True,
    )
    head = subprocess.run(
        ["git", "-C", str(paths["source_root"]), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()

    with pytest.raises(GateError, match="contract catalog digest"):
        validate_bundle(paths, expected_head_sha=head)
