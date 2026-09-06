"""Live integration test: SDK tool wrappers read-mutate-read through the real
middleware pipeline against real Unity, proving post-mutation reads are fresh
(not stale middleware cache). Requires Unity Editor running with MCP plugin.
Run with: pytest -m live
"""
import re
import uuid

import pytest

pytestmark = pytest.mark.live

_POSITION_RE = re.compile(r"m_localposition:\s*\(([^)]+)\)", re.IGNORECASE)


async def test_sdk_create_read_mutate_read(sdk_tools):
    """create_object -> get_component (default) -> set_property -> get_component (fresh).

    The post-mutation read may be served as `[CACHED:reflect-snapshot]`, seeded
    from Unity's own post-write read-back in ExecSetProperty (still wire-derived,
    not stale) -- Test 3's raw bridge read is the middleware-free oracle for that.
    """
    name = f"Live_sdk_{uuid.uuid4().hex[:8]}"
    path = f"/{name}"
    await sdk_tools.create_object(name=name)
    try:
        before = await sdk_tools.get_component(path, type="Transform")
        assert _POSITION_RE.search(before) is None, (
            f"unexpected position in fresh object: {before}"
        )

        await sdk_tools.set_property(
            path=path, component="Transform", prop="m_LocalPosition", value="5,10,15"
        )

        after = await sdk_tools.get_component(path, type="Transform")
        match = _POSITION_RE.search(after)
        assert match, after
        assert match.group(1).strip() == "5, 10, 15", after
    finally:
        await sdk_tools.bridge.send("delete_object", {"path": path})


async def test_sdk_cache_invalidation_after_mutation(sdk_tools):
    """create_object (a WRITE_CMD) must invalidate the middleware hierarchy cache."""
    name = f"Live_sdk_{uuid.uuid4().hex[:8]}"
    path = f"/{name}"
    before_hierarchy = await sdk_tools.get_hierarchy(depth=1)
    assert name not in before_hierarchy, before_hierarchy

    await sdk_tools.create_object(name=name)
    try:
        after_hierarchy = await sdk_tools.get_hierarchy(depth=1)
        assert name in after_hierarchy, after_hierarchy
        assert after_hierarchy != before_hierarchy
    finally:
        await sdk_tools.bridge.send("delete_object", {"path": path})


async def test_sdk_set_property_independent_wire_verify(sdk_tools):
    """Raw TCP read (bypassing all middleware) must match the SDK-mutated value."""
    name = f"Live_sdk_{uuid.uuid4().hex[:8]}"
    path = f"/{name}"
    await sdk_tools.create_object(name=name)
    try:
        await sdk_tools.set_property(
            path=path, component="Transform", prop="m_LocalPosition", value="7,8,9"
        )

        raw = await sdk_tools.bridge._raw.send(
            "get_component", {"path": path, "type": "Transform"}
        )
        assert raw.get("ok"), raw
        assert "m_LocalPosition: (7, 8, 9)" in raw.get("data", ""), raw
    finally:
        await sdk_tools.bridge.send("delete_object", {"path": path})
