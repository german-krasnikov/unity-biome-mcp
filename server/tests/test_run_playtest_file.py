"""Tests for run_playtest_file tool (P0.1)."""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import run_playtest_file


async def test_path_forwarded_to_send(mock_bridge):
    """path= arg forwarded to run_playtest cmd; no script= key."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest_file("Playtests/farm.playtest")
    cmd, sent_args = mock_bridge.send.call_args[0]
    assert cmd == "run_playtest"
    assert sent_args["path"] == "Playtests/farm.playtest"
    assert "script" not in sent_args


async def test_defs_normalized_and_forwarded(mock_bridge):
    """defs are normalized (VAL prefix added) and forwarded."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest_file("Playtests/farm.playtest", defs="$foo /Obj|Comp|field")
    sent_args = mock_bridge.send.call_args[0][1]
    assert "defs" in sent_args
    assert sent_args["defs"].startswith("VAL")


async def test_compress_report_applied(mock_bridge):
    """Passing step lines are stripped from output."""
    raw = "PLAYTEST: 3/3 (1.0s)\n[1] ASSERT x == 1 — PASS (1)\n[2] ASSERT y == 2 — PASS (2)"
    mock_bridge.send.return_value = {"ok": True, "data": raw}
    result = await run_playtest_file("Playtests/test.playtest")
    assert "— PASS" not in result
    assert "PLAYTEST: 3/3" in result


async def test_missing_file_error_propagated(mock_bridge):
    """err: string from Unity is returned as-is (not swallowed by compress)."""
    mock_bridge.send.return_value = {"ok": True, "data": "err: file not found: missing.playtest"}
    result = await run_playtest_file("missing.playtest")
    assert "err: file not found" in result


async def test_abort_on_fail_forwarded(mock_bridge):
    """abort_on_fail=True sends abort_on_fail='true' in args."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest_file("Playtests/farm.playtest", abort_on_fail=True)
    sent_args = mock_bridge.send.call_args[0][1]
    assert sent_args.get("abort_on_fail") == "true"
