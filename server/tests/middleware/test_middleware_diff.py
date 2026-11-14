"""Tests for HierarchyDiff — _maybe_diff_hierarchy method on Middleware."""
from unity_mcp.middleware import Middleware


def test_first_hierarchy_call_returns_full_caches():
    mw = Middleware()
    full = "/Root\n  /A\n  /B\n  /C"
    result = mw._maybe_diff_hierarchy(full)
    assert result == full
    assert mw._last_hierarchy_full == full


def test_second_hierarchy_call_small_change_returns_diff():
    mw = Middleware()
    mw._maybe_diff_hierarchy("/Root\n  /A\n  /B\n  /C")
    new = "/Root\n  /A\n  /B\n  /C\n  /D"
    result = mw._maybe_diff_hierarchy(new)
    assert "[DIFF since #" in result
    assert "/D" in result


def test_diff_threshold_50pct_returns_full():
    mw = Middleware()
    mw._maybe_diff_hierarchy("/A\n/B")
    new = "/X\n/Y\n/Z\n/W\n/V"  # >50% changed
    result = mw._maybe_diff_hierarchy(new)
    assert result == new
    assert "[DIFF" not in result


def test_no_change_returns_no_change_marker():
    mw = Middleware()
    s = "/Root\n  /A"
    mw._maybe_diff_hierarchy(s)
    result = mw._maybe_diff_hierarchy(s)
    assert "NO_CHANGE" in result


def test_call_id_increments():
    mw = Middleware()
    mw._maybe_diff_hierarchy("/A")
    mw._maybe_diff_hierarchy("/A\n/B")
    assert mw._hierarchy_call_id == 2


async def test_no_distill_bypasses_diff():
    """_no_distill=True in args skips diff and returns full hierarchy."""
    import os
    os.environ["UNITY_MCP_VALIDATE"] = "0"
    from unity_mcp.middleware import wrap_send, Middleware

    mw = Middleware()
    call_results = ["/A\n/B", "/A\n/B\n/C"]
    idx = [0]

    async def mock_send(cmd, args, timeout=30.0):
        if cmd == "get_hierarchy":
            r = call_results[min(idx[0], len(call_results) - 1)]
            idx[0] += 1
            return r
        return "ok"

    wrapped = wrap_send(mock_send, mw)

    # First call populates diff state
    r1 = await wrapped("get_hierarchy", {"summary": "true"})
    # Second call without _no_distill → diff
    r2 = await wrapped("get_hierarchy", {"summary": "true"})
    # Third call with _no_distill → full hierarchy (no diff applied)
    r3 = await wrapped("get_hierarchy", {"summary": "true", "_no_distill": True})
    # r2 may be diff or NO_CHANGE; r3 must not contain [DIFF
    assert "[DIFF" not in r3
