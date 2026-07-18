"""Smoke tests: minimal roundtrip through real TCP bridge."""
import pytest

from tests.live.conftest import strip_markers

pytestmark = pytest.mark.live


async def test_get_console_returns_text(wrapped_bridge):
    """get_console via middleware pipeline returns str (not dict)."""
    r = await wrapped_bridge.send("get_console", {"limit": 1})
    assert isinstance(r, str)


async def test_create_object_appears_in_hierarchy(wrapped_bridge, sandbox):
    r = await wrapped_bridge.send("get_hierarchy", {})
    # sandbox fixture name starts with "Live_"
    assert "Live_" in r


async def test_destroy_object_cleans_up(wrapped_bridge):
    """Create object, delete via delete_object, verify absent from find_objects."""
    import uuid
    name = f"LiveDel_{uuid.uuid4().hex[:6]}"
    await wrapped_bridge.send("create_object", {"name": name})
    try:
        await wrapped_bridge.send("delete_object", {"path": f"/{name}"})
        r2 = await wrapped_bridge.send("find_objects", {"name": name})
        assert not strip_markers(r2).strip(), f"Object still exists after destroy: {r2}"
    finally:
        try:
            await wrapped_bridge.send("delete_object", {"path": f"/{name}"})
        except Exception:
            pass
