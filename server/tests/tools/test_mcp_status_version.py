"""TDD tests for mcp_status version fields (GAP 1 — Subtask 1B)."""
import re


async def test_mcp_status_includes_python_version(mock_bridge, bridge_response):
    """mcp_status() appends python_version= to the C# response."""
    bridge_response(data="scene=Main\ndirty=False\nplaying=False\ncompiling=False\nport=9500\naliases=0")
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert "python_version=" in result


async def test_mcp_status_python_version_is_semver(mock_bridge, bridge_response):
    """python_version= value starts with a semver-like major.minor.patch prefix."""
    bridge_response(data="scene=Main\ndirty=False\nplaying=False\ncompiling=False\nport=9500\naliases=0")
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    line = next(l for l in result.splitlines() if l.startswith("python_version="))
    ver = line.split("=", 1)[1]
    assert re.match(r"\d+\.\d+\.\d+", ver), f"Expected semver prefix, got: {ver}"


async def test_mcp_status_forwards_plugin_version_from_cs(mock_bridge, bridge_response):
    """mcp_status() transparently forwards plugin_version= and protocol= from C# get_status."""
    bridge_response(data="scene=Main\nplugin_version=1.46.1\nprotocol=3")
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert "plugin_version=1.46.1" in result
    assert "protocol=3" in result
