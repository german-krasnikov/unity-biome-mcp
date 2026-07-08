"""Live tests: console first param + compile_status."""
import re

import pytest

pytestmark = pytest.mark.live


async def test_compile_status_format(bridge):
    """compile_status returns 'idle|N' or 'compiling|N' format."""
    result = await bridge.send("compile_status", {})
    text = result.get("data", "") if isinstance(result, dict) else str(result)
    assert re.match(r"(idle(-never|-failed|-stale)?|compiling)\|\d", text), f"Bad format: {text}"


async def test_console_first_param_no_crash(bridge):
    """get_console with first=3 doesn't crash."""
    result = await bridge.send("get_console", {"first": "3", "count": "10"})
    text = result.get("data", "") if isinstance(result, dict) else str(result)
    # Just verify no error — content depends on what's in console
    assert "err" not in text.lower() or "[Error]" in text, f"Unexpected error: {text}"
