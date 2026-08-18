"""Gate 3: Write Operations — mutations verified by independent readback."""

import pytest

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_create_object_postcondition(conformance_worker):
    """create_object → find_objects confirms presence."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_create"
    try:
        # Layer 1: execute
        resp = await bridge.send("create_object", {"name": name})
        assert resp.get("ok", False), f"create_object failed: {resp}"

        # Layer 2: readback
        find_resp = await bridge.send("find_objects", {"name": name})
        found = find_resp.get("data", "")
        assert name in found, f"created object not found: {found}"
    finally:
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_set_property_postcondition(conformance_worker):
    """set_property → get_component confirms value changed."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_prop"
    try:
        await bridge.send("create_object", {"name": name})

        # Layer 1: set position
        resp = await bridge.send("set_property", {
            "path": f"/{name}",
            "component": "Transform",
            "prop": "m_LocalPosition",
            "value": "3,5,7",
        })
        assert resp.get("ok", False), f"set_property failed: {resp}"

        # Layer 2: readback
        comp_resp = await bridge.send("get_component", {
            "path": f"/{name}",
            "type": "Transform",
        })
        comp_data = comp_resp.get("data", "")
        # Position should contain the values we set
        assert "3" in comp_data and "5" in comp_data and "7" in comp_data, \
            f"position not reflected: {comp_data[:200]}"
    finally:
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_manage_component_add_readback(conformance_worker):
    """manage_component(add) → get_component confirms component present."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_comp"
    try:
        await bridge.send("create_object", {"name": name})

        # Layer 1: add component
        resp = await bridge.send("manage_component", {
            "path": f"/{name}",
            "type": "Rigidbody",
            "action": "add",
        })
        assert resp.get("ok", False), f"manage_component failed: {resp}"
        assert "Rigidbody" in resp.get("data", ""), "response doesn't mention Rigidbody"

        # Layer 2: readback
        comp_resp = await bridge.send("get_component", {
            "path": f"/{name}",
            "type": "Rigidbody",
        })
        comp_data = comp_resp.get("data", "")
        assert comp_resp.get("ok", False), f"Rigidbody not found on readback: {comp_resp}"
        assert len(comp_data) > 0, "Rigidbody readback empty"
    finally:
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_set_active_readback(conformance_worker):
    """set_active(false) → get_hierarchy shows deactivated marker."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_active"
    try:
        await bridge.send("create_object", {"name": name})

        # Layer 1: deactivate
        resp = await bridge.send("set_active", {
            "path": f"/{name}",
            "active": "false",
        })
        assert resp.get("ok", False), f"set_active failed: {resp}"

        # Layer 2: readback via get_hierarchy — inactive objects show ! prefix
        hier_resp = await bridge.send("get_hierarchy", {"depth": 1})
        hier_data = hier_resp.get("data", "")
        # Inactive objects are suffixed with ! in hierarchy (e.g. "  Name @ref !")
        assert any(name in line and line.rstrip().endswith("!")
                   for line in hier_data.splitlines()), \
            f"deactivated object not marked in hierarchy: {hier_data[:300]}"
    finally:
        # Reactivate before delete (good practice)
        await bridge.send("set_active", {"path": f"/{name}", "active": "true"})
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_delete_object_confirms_absent(conformance_worker):
    """delete → find_objects confirms gone."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_del"

    try:
        await bridge.send("create_object", {"name": name})

        # Delete
        resp = await bridge.send("delete_object", {"path": f"/{name}"})
        assert resp.get("ok", False), f"delete_object failed: {resp}"

        # Layer 2: confirm absent
        find_resp = await bridge.send("find_objects", {"name": name})
        found = find_resp.get("data", "")
        assert name not in found or found.strip() == "none", \
            f"deleted object still found: {found}"
    finally:
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_write_cleanup_restores_scene(conformance_worker):
    """All mutations in scene_ns → cleanup → prove no scene_ns objects remain."""
    worker, bridge = conformance_worker
    names = [f"{worker.scene_ns}_cleanup_{i}" for i in range(3)]

    try:
        # Create several objects
        for n in names:
            resp = await bridge.send("create_object", {"name": n})
            assert resp.get("ok", False), f"create {n} failed"
    finally:
        # Cleanup all
        for n in names:
            await bridge.send("delete_object", {"path": f"/{n}"})

    # Verify none remain
    hier_resp = await bridge.send("get_hierarchy", {"depth": 1})
    hier_data = hier_resp.get("data", "")
    assert worker.scene_ns not in hier_data, \
        f"scene_ns objects remain after cleanup: {hier_data[:300]}"
