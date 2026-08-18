
import pytest

pytestmark = [pytest.mark.live, pytest.mark.cross_project, pytest.mark.asyncio(loop_scope="session")]


async def test_read_only_blocks_create_object(dual_worker_session):
    worker_a, _, worker_b, bridge_b = dual_worker_session
    name = f"{worker_b.scene_ns}_ro_create"
    resp = await bridge_b.send("create_object", {"name": name})
    assert not resp.get("ok", True), \
        f"Expected create_object to be blocked on read-only Worker B, got ok=True: {resp}"
    assert resp.get("err", ""), "Expected an error message from blocked create_object"


async def test_read_only_blocks_delete_object(dual_worker_session):
    """Create on Worker A, then verify Worker B blocks the delete (not just 'not found')."""
    worker_a, bridge_a, worker_b, bridge_b = dual_worker_session
    name = f"{worker_a.scene_ns}_ro_del"
    try:
        create = await bridge_a.send("create_object", {"name": name})
        assert create["ok"], f"create_object failed on A: {create}"

        resp = await bridge_b.send("delete_object", {"path": f"/{name}"})
        assert not resp.get("ok", True), \
            f"Expected delete_object to be blocked on read-only Worker B: {resp}"
    finally:
        await bridge_a.send("delete_object", {"path": f"/{name}"})


async def test_read_only_blocks_set_property(dual_worker_session):
    worker_a, _, worker_b, bridge_b = dual_worker_session
    resp = await bridge_b.send("set_property", {
        "path": "/Main Camera",
        "component": "Transform",
        "prop": "m_LocalPosition",
        "value": "0,0,0",
    })
    assert not resp.get("ok", True), \
        f"Expected set_property to be blocked on read-only Worker B, got ok=True: {resp}"


async def test_read_only_allows_get_hierarchy(dual_worker_session):
    _, _, _, bridge_b = dual_worker_session
    resp = await bridge_b.send("get_hierarchy", {"depth": 1})
    assert resp["ok"], \
        f"get_hierarchy should be allowed on read-only Worker B: {resp}"
