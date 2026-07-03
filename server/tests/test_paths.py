"""Regression guard: every ~/.unity-mcp base-dir reference must go through
paths.unity_mcp_dir(), not a hand-rolled Path.home() / ".unity-mcp" literal."""
import re
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent.parent
_SRC_ROOT = _REPO_ROOT / "server" / "src" / "unity_mcp"

_FILES_TO_CHECK = [
    _SRC_ROOT / "lockfile.py",
    _SRC_ROOT / "server_lifespan.py",
    _SRC_ROOT / "server_control.py",
    _SRC_ROOT / "unity_state.py",
    _SRC_ROOT / "_update_check.py",
    _SRC_ROOT / "crash_log.py",
    _SRC_ROOT / "doctor.py",
    _SRC_ROOT / "budget" / "cost_tracker.py",
    _REPO_ROOT / "install" / "commands.py",
]

_BYPASS_RE = re.compile(r'Path\.home\(\)\s*/\s*[\'"]\.unity-mcp[\'"]')


def test_all_unity_mcp_dir_consumers_use_canonical_helper():
    offenders = []
    for f in _FILES_TO_CHECK:
        text = f.read_text(encoding="utf-8")
        if _BYPASS_RE.search(text):
            offenders.append(str(f.relative_to(_REPO_ROOT)))
    assert not offenders, f"Bypasses canonical unity_mcp_dir(): {offenders}"
