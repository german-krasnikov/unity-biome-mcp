"""Differential tests: batch vs sequential must produce identical scene state.

Uses distinct namespaced prefixes (seq_ vs bat_) to avoid needing undo —
two independent object sets are created and compared structurally.
"""

import pytest

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_batch_vs_sequential_create_identical_hierarchy(seam_bridge, seam_worker):
    """Batch create-3 vs sequential create-3: all objects visible in hierarchy."""
    seq_names = [seam_worker.name(f"seq_{i}") for i in range(3)]
    bat_names = [seam_worker.name(f"bat_{i}") for i in range(3)]

    # Sequential path
    for n in seq_names:
        resp = await seam_bridge.send("create_object", {"name": n})
        assert resp.get("ok"), f"sequential create {n} failed: {resp}"

    # Batch path
    batch_cmds = "\n".join(f"create_object name={n}" for n in bat_names)
    resp = await seam_bridge.send("batch", {"commands": batch_cmds})
    assert resp.get("ok"), f"batch create failed: {resp.get('err', resp)}"
    text = resp.get("data", "") or resp.get("err", "")
    assert "err:" not in text, f"batch create had errors: {text[:200]}"

    # Both sets of objects must be present in hierarchy
    hier = (await seam_bridge.send("get_hierarchy", {"depth": 1})).get("data", "")
    for n in seq_names + bat_names:
        assert n in hier, f"object {n} missing from hierarchy after create"

    # Objects created via batch should have same Transform state as sequential
    for seq_n, bat_n in zip(seq_names, bat_names):
        seq_resp = await seam_bridge.send("get_component", {"path": f"/{seq_n}", "type": "Transform"})
        bat_resp = await seam_bridge.send("get_component", {"path": f"/{bat_n}", "type": "Transform"})
        assert bat_resp.get("ok"), (
            f"batch-created object get_component failed: "
            f"{bat_n} err={bat_resp.get('err', bat_resp)}"
        )


async def test_batch_vs_sequential_set_property_identical_values(seam_bridge, seam_worker):
    """set_property via batch vs direct: Transform position values identical."""
    seq_name = seam_worker.name("dseq")
    bat_name = seam_worker.name("dbat")

    for n in [seq_name, bat_name]:
        await seam_bridge.send("create_object", {"name": n})

    # Sequential
    await seam_bridge.send("set_property", {
        "path": f"/{seq_name}",
        "component": "Transform",
        "prop": "m_LocalPosition",
        "value": "1,2,3",
    })

    # Batch
    await seam_bridge.send("batch", {"commands": (
        f"set_property path=/{bat_name} component=Transform "
        f"prop=m_LocalPosition value=1,2,3"
    )})

    seq_resp = await seam_bridge.send("get_component", {"path": f"/{seq_name}", "type": "Transform"})
    bat_resp = await seam_bridge.send("get_component", {"path": f"/{bat_name}", "type": "Transform"})

    for v in ["1", "2", "3"]:
        assert v in seq_resp.get("data", ""), f"sequential set_property: {v} missing in Transform"
        assert v in bat_resp.get("data", ""), f"batch set_property: {v} missing in Transform"


async def test_batch_vs_sequential_set_active_identical_hierarchy(seam_bridge, seam_worker):
    """set_active(false) via batch vs direct: both appear inactive in hierarchy."""
    seq_name = seam_worker.name("aseq")
    bat_name = seam_worker.name("abat")

    for n in [seq_name, bat_name]:
        await seam_bridge.send("create_object", {"name": n})

    await seam_bridge.send("set_active", {"path": f"/{seq_name}", "active": "false"})
    await seam_bridge.send("batch", {"commands": f"set_active path=/{bat_name} active=false"})

    hier = (await seam_bridge.send("get_hierarchy", {"depth": 1})).get("data", "")
    lines = hier.splitlines()

    seq_inactive = any(seq_name in ln and ln.rstrip().endswith("!") for ln in lines)
    bat_inactive = any(bat_name in ln and ln.rstrip().endswith("!") for ln in lines)

    assert seq_inactive, "sequential set_active(false): object not marked inactive in hierarchy"
    assert bat_inactive, "batch set_active(false): object not marked inactive in hierarchy"
    assert seq_inactive == bat_inactive, "batch and sequential set_active disagree on inactive state"
