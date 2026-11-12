"""Thin re-export shim (PT-4): the real scanner now lives in the installed
package (`unity_mcp.playtest_header`) so it ships in the wheel. CI scripts
run against a bare repo checkout (no `unity_mcp` pip-installed, e.g.
unity-player-playtest.yml's `shell: python` steps) still `import
playtest_header` directly, so this keeps that working by pointing sys.path
at `server/src` -- same pattern as scripts/export_tools.py.
"""
import pathlib
import sys

# Allow running from repo root without installing the package.
_SERVER_SRC = pathlib.Path(__file__).parent.parent / "server" / "src"
if str(_SERVER_SRC) not in sys.path:
    sys.path.insert(0, str(_SERVER_SRC))

from unity_mcp.playtest_header import Header, scan  # noqa: E402,F401
