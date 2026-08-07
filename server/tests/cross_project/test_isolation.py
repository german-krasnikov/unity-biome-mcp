from __future__ import annotations

import pytest

pytestmark = [pytest.mark.live, pytest.mark.cross_project, pytest.mark.asyncio(loop_scope="session")]


async def test_create_in_a_not_visible_in_b(dual_worker_session):
    worker_a, bridge_a, worker_b, bridge_b = dual_worker_session
    name = f"{worker_a.scene_ns}_iso_a"
    try:
        resp = await bridge_a.send("create_object", {"name": name})
        assert resp["ok"], f"create_object failed on A: {resp}"

        hier_b = await bridge_b.send("get_hierarchy", {"depth": 1})
        assert name not in hier_b.get("data", ""), \
            f"object created in A appeared in B: {hier_b['data']}"
    finally:
        await bridge_a.send("delete_object", {"path": f"/{name}"})


async def test_create_in_b_not_visible_in_a(dual_worker_session):
    worker_a, bridge_a, worker_b, bridge_b = dual_worker_session
    name = f"{worker_b.scene_ns}_iso_b"
    try:
        resp = await bridge_b.send("create_object", {"name": name})
        assert resp["ok"], f"create_object failed on B: {resp}"

        hier_a = await bridge_a.send("get_hierarchy", {"depth": 1})
        assert name not in hier_a.get("data", ""), \
            f"object created in B appeared in A: {hier_a['data']}"
    finally:
        await bridge_b.send("delete_object", {"path": f"/{name}"})


async def test_aba_revert(dual_worker_session):
    worker_a, bridge_a, worker_b, bridge_b = dual_worker_session
    name = f"{worker_a.scene_ns}_aba"
    created = False
    try:
        resp = await bridge_a.send("create_object", {"name": name})
        assert resp["ok"], f"create_object failed: {resp}"
        created = True

        hier_b = await bridge_b.send("get_hierarchy", {"depth": 1})
        assert name not in hier_b.get("data", ""), "object leaked from A to B"

        del_resp = await bridge_a.send("delete_object", {"path": f"/{name}"})
        assert del_resp["ok"], f"delete_object failed: {del_resp}"
        created = False

        hier_a = await bridge_a.send("get_hierarchy", {"depth": 1})
        assert name not in hier_a.get("data", ""), "deleted object still in A hierarchy"
    finally:
        if created:
            await bridge_a.send("delete_object", {"path": f"/{name}"})
