"""P1-20 aggregate gate: git-diff error handling in
validate_fsr_qualification_receipts.py — same class of Run 2 regression as
run_fsr_qualification_cell.py's _git_changed_paths, guarded independently
since these are two separate subprocess.run call sites.

Runs in the standard `scripts/tests` lane: no Unity, no real git failure —
subprocess.run is monkeypatched.
"""
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS.parent))
import validate_fsr_qualification_receipts as validator  # noqa: E402


def test_git_changed_paths_raises_fsr_qualification_error_on_shallow_clone_failure(
    monkeypatch: pytest.MonkeyPatch,
):
    def _fail(*args, **kwargs):
        raise subprocess.CalledProcessError(
            128, args[0], output="", stderr="fatal: bad revision '7875430f..HEAD'\n"
        )

    monkeypatch.setattr(subprocess, "run", _fail)

    with pytest.raises(validator.fq.FsrQualificationError):
        validator._git_changed_paths("7875430f73d28a043806742164ab478145dedafe")
