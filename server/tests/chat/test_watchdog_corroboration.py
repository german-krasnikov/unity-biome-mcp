"""C5 regression guard: ProactiveWatchdog._scan must NOT run get_console output
through editor_log.corroborate(). That helper is scoped to compile-error
corroboration; applying it to runtime console logs could replace a real
runtime error with a stale compile-error dump. Trust get_console() directly.
"""
from unittest.mock import AsyncMock, patch

from unity_mcp.watchdog import ProactiveWatchdog


async def test_scan_does_not_corroborate_console_text():
    """_scan must NOT call editor_log.corroborate() on the raw console text."""
    raw_console = "NullRef exception in Update"

    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return ""
        return raw_console

    wd = ProactiveWatchdog(send)

    with patch("unity_mcp.editor_log.corroborate", return_value="") as mock_cor:
        await wd._scan()

    mock_cor.assert_not_called()


async def test_scan_alerts_on_raw_console_text_uncorroborated():
    """A stale compile-error corroborate() result must not suppress a real runtime
    console alert — _scan must use get_console()'s raw text as-is."""
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return ""
        return "stale console text"

    wd = ProactiveWatchdog(send)

    # Even if corroborate() would clear the text, _scan must not call it —
    # the alert must still fire from the raw console text.
    with patch("unity_mcp.editor_log.corroborate", return_value="") as mock_cor:
        await wd._scan()

    mock_cor.assert_not_called()
    assert wd._pending_alert is not None
    assert "console:" in wd._pending_alert
