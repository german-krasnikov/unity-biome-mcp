"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: git-diff error
handling in run_fsr_qualification_cell.py.

Run 2 (33378221200) crashed 4/6 cells with an unhandled
subprocess.CalledProcessError from `git diff --name-only <base>..HEAD`
(actions/checkout's default shallow clone has no history for the frozen
base_product_sha) — the exception propagated past main()'s except tuple
entirely, so no receipt.json was ever written and the workflow's fallback
INFRASTRUCTURE_BLOCKED step had to guess at the cause. This guards the fix:
any git-diff failure, for any reason, must surface as a caught
FsrQualificationCellError so a receipt always gets written.

Runs in the standard `scripts/tests` lane: no Unity, no real git failure —
subprocess.run is monkeypatched.
"""
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import run_fsr_qualification_cell as cell_script  # noqa: E402


def test_git_changed_paths_raises_cell_error_on_shallow_clone_failure(
    monkeypatch: pytest.MonkeyPatch,
):
    def _fail(*args, **kwargs):
        raise subprocess.CalledProcessError(
            128, args[0], output="", stderr="fatal: bad revision '7875430f..HEAD'\n"
        )

    monkeypatch.setattr(subprocess, "run", _fail)

    with pytest.raises(cell_script.FsrQualificationCellError):
        cell_script._git_changed_paths("7875430f73d28a043806742164ab478145dedafe")


def test_git_changed_paths_returns_list_on_success(monkeypatch: pytest.MonkeyPatch):
    class _Result:
        stdout = "scripts/foo.py\nscripts/bar.py\n"

    monkeypatch.setattr(subprocess, "run", lambda *a, **k: _Result())

    assert cell_script._git_changed_paths("a" * 40) == ["scripts/foo.py", "scripts/bar.py"]
