"""Cross-language guard: Python SERVER_NAME/MCP_BLANKET must match PermissionConfig.cs.

v0.70.7 shipped a near-miss where two independent config writers used *different*
server-name literals and a CLI silently registered two MCP servers on the same port.
Nothing else in the codebase prevents that class of drift from recurring — this is
a pure text-file assertion, no C#/NUnit/Unity runtime needed. Runs in the `not live`
unit suite, $0 cost.
"""
import re
from pathlib import Path

from unity_mcp.config.merger import SERVER_NAME

_CSHARP_FILE = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor" / "PermissionConfig.cs"


def test_python_server_name_matches_csharp_constant():
    text = _CSHARP_FILE.read_text(encoding="utf-8")
    m = re.search(r'SERVER_NAME\s*=\s*"([^"]+)"', text)
    assert m, f"SERVER_NAME const not found in {_CSHARP_FILE}"
    assert m.group(1) == SERVER_NAME, (
        f"DRIFT: Python SERVER_NAME={SERVER_NAME!r} != C# SERVER_NAME={m.group(1)!r}. "
        "This is exactly the v0.70.7 duplicate-MCP-server bug class — fix one side."
    )


def test_csharp_mcp_blanket_matches_python():
    from unity_mcp.backend_def import MCP_BLANKET

    text = _CSHARP_FILE.read_text(encoding="utf-8")
    m = re.search(r'MCP_BLANKET\s*=\s*"mcp__"\s*\+\s*SERVER_NAME', text)
    assert m, "PermissionConfig.cs must derive MCP_BLANKET from SERVER_NAME"
    m2 = re.search(r'SERVER_NAME\s*=\s*"([^"]+)"', text)
    assert m2, f"SERVER_NAME const not found in {_CSHARP_FILE}"
    assert f"mcp__{m2.group(1)}" == MCP_BLANKET
