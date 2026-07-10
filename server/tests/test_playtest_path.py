"""Tests for run_playtest path= parameter."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.server import run_playtest


async def test_run_playtest_path_forwards_to_bridge(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 5 steps"}
    result = await run_playtest(path="Assets/Playtests/farm.playtest")
    call = mock_bridge.send.call_args
    assert call[0][0] == "run_playtest"
    assert call[0][1]["path"] == "Assets/Playtests/farm.playtest"
    assert "script" not in call[0][1]
    assert result == "PASS: 5 steps"


async def test_run_playtest_both_args_raises(mock_bridge):
    with pytest.raises(ValueError, match="mutually exclusive"):
        await run_playtest(script="WAIT 1", path="Assets/Playtests/farm.playtest")


async def test_run_playtest_no_args_raises(mock_bridge):
    with pytest.raises(ValueError, match="required"):
        await run_playtest()


async def test_run_playtest_path_with_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest(path="Assets/test.playtest", timeout=60.0)
    call = mock_bridge.send.call_args
    assert call[0][1]["timeout"] == "60.0"
    assert call[1]["timeout"] == 80.0


async def test_run_playtest_path_with_defs(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest(path="Assets/test.playtest", defs="VAL $hp /Player|HP|health")
    call = mock_bridge.send.call_args
    assert call[0][1]["defs"] == "VAL $hp /Player|HP|health"
    assert call[0][1]["path"] == "Assets/test.playtest"


async def test_run_playtest_path_has_explicit_path_flag():
    """_explicit_path must be passed to _send so middleware skips path resolution.
    Middleware strips it before reaching bridge — patch at runtime module level."""
    from unittest.mock import patch, AsyncMock
    import unity_mcp.tools.runtime as rt
    mock_send = AsyncMock(return_value="PASS")
    with patch.object(rt, "_send", mock_send):
        await rt.run_playtest(path="Assets/Playtests/farm.playtest")
    call = mock_send.call_args
    assert call[0][1].get("_explicit_path") == "true"


async def test_run_playtest_non_assets_path_forwards(mock_bridge):
    """Path outside Assets/ (e.g. Playtests/) must be forwarded as-is, no Assets/ prefix required."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest(path="Playtests/farm.playtest")
    call = mock_bridge.send.call_args
    assert call[0][1]["path"] == "Playtests/farm.playtest"


async def test_normalize_defs_cases():
    from unity_mcp.tools.runtime import _normalize_defs
    assert _normalize_defs("val $hp /P|HP|h") == "VAL $hp /P|HP|h"
    assert _normalize_defs("Val $hp /P|HP|h") == "VAL $hp /P|HP|h"
    assert _normalize_defs("VAL $hp /P|HP|h") == "VAL $hp /P|HP|h"
    assert _normalize_defs("$hp /P|HP|h") == "VAL $hp /P|HP|h"
    assert _normalize_defs("# comment\n\nval $a /O|C|f") == "VAL $a /O|C|f"


async def test_run_playtest_script_mode_unchanged(mock_bridge):
    """Existing script= flow must not be affected."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 3 steps"}
    script = "WAIT 1\nASSERT_CONSOLE_CLEAN"
    result = await run_playtest(script=script)
    call = mock_bridge.send.call_args
    assert call[0][0] == "run_playtest"
    assert call[0][1]["script"] == script
    assert "path" not in call[0][1]
    assert result == "PASS: 3 steps"


async def test_run_playtest_script_with_comment_only_defs(mock_bridge):
    """defs with only comments should not crash."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest(script="WAIT 1", defs="# just a comment")
    call = mock_bridge.send.call_args
    assert call[0][1]["script"] == "WAIT 1"
