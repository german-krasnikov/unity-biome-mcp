"""Parametric round-trip tests: mutate → readback → semantic verify.

Each case in ROUND_TRIP_CASES defines:
  (mutate_cmd, mutate_args_template, read_cmd, read_args_template, check_fn)

{ns} in arg values is substituted with seam_worker.ns at runtime.
check_fn signature: (resp: dict, ns: str) -> bool
Prerequisites (object creation) are handled by _setup_prerequisites.
"""
from __future__ import annotations

import pytest

from tests.seams.invariants import assert_round_trip

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


# Each entry: (mutate_cmd, mutate_args_t, read_cmd, read_args_t, check_fn)
# check_fn(resp, ns) -> bool — True = pass, False = fail
ROUND_TRIP_CASES = [
    pytest.param(
        "create_object",
        {"name": "{ns}_rt_create"},
        "get_hierarchy",
        {"depth": "1"},
        lambda resp, ns: (ns + "_rt_create") in resp.get("data", ""),
        id="create_object→hierarchy",
    ),
    pytest.param(
        "set_property",
        {"path": "/{ns}_rt_prop", "component": "Transform",
         "prop": "m_LocalPosition", "value": "3,5,7"},
        "get_component",
        {"path": "/{ns}_rt_prop", "type": "Transform"},
        lambda resp, ns: all(v in resp.get("data", "") for v in ["3", "5", "7"]),
        id="set_property→get_component",
    ),
    pytest.param(
        "manage_component",
        {"path": "/{ns}_rt_comp", "type": "Rigidbody", "action": "add"},
        "get_component",
        {"path": "/{ns}_rt_comp", "type": "Rigidbody"},
        lambda resp, ns: resp.get("ok") and len(resp.get("data", "")) > 0,
        id="manage_component_add→get_component",
    ),
    pytest.param(
        "set_active",
        {"path": "/{ns}_rt_active", "active": "false"},
        "get_hierarchy",
        {"depth": "1"},
        lambda resp, ns: any(
            (ns + "_rt_active") in line and line.rstrip().endswith("!")
            for line in resp.get("data", "").splitlines()
        ),
        id="set_active_false→hierarchy_inactive_marker",
    ),
    pytest.param(
        "delete_object",
        {"path": "/{ns}_rt_del"},
        "get_hierarchy",
        {"depth": "1"},
        lambda resp, ns: (ns + "_rt_del") not in resp.get("data", ""),
        id="delete_object→hierarchy_absent",
    ),
    pytest.param(
        "set_parent",
        {"path": "/{ns}_rt_child", "parent": "/{ns}_rt_parent"},
        "get_hierarchy",
        {"depth": "2"},
        lambda resp, ns: (ns + "_rt_child") in resp.get("data", ""),
        id="set_parent→hierarchy_nesting",
    ),
    pytest.param(
        "rename_object",
        {"path": "/{ns}_rt_old", "name": "{ns}_rt_new"},
        "get_hierarchy",
        {"depth": "1"},
        lambda resp, ns: (ns + "_rt_new") in resp.get("data", ""),
        id="rename_object→hierarchy",
    ),
    pytest.param(
        "set_property_delta",
        {"path": "/{ns}_rt_delta", "component": "Transform",
         "prop": "m_LocalPosition.x", "delta": "5"},
        "get_component",
        {"path": "/{ns}_rt_delta", "type": "Transform"},
        lambda resp, ns: resp.get("ok") and len(resp.get("data", "")) > 0,
        id="set_property_delta→get_component",
    ),
]

# Commands that need a pre-existing object at their 'path' argument
_NEEDS_PATH = frozenset({
    "set_property", "manage_component", "set_active", "delete_object",
    "rename_object", "set_property_delta",
})


async def _setup_prerequisites(bridge, worker, mutate_cmd: str, mutate_args: dict) -> None:
    """Create objects required before the mutation runs.

    Rules:
    - set_parent: create both 'path' and 'parent' objects
    - _NEEDS_PATH commands: create the 'path' object
    - create_object, scene_environment: no prereqs needed
    """
    if mutate_cmd == "set_parent":
        for key in ("path", "parent"):
            path = mutate_args.get(key, "")
            if path:
                name = path.lstrip("/").split("/")[0]
                await bridge.send("create_object", {"name": name})

    elif mutate_cmd in _NEEDS_PATH:
        path = mutate_args.get("path", "")
        if path:
            name = path.lstrip("/").split("/")[0]
            await bridge.send("create_object", {"name": name})


@pytest.mark.parametrize(
    "mutate_cmd,mutate_args_t,read_cmd,read_args_t,check_fn",
    ROUND_TRIP_CASES,
)
async def test_round_trip(
    seam_bridge,
    seam_worker,
    mutate_cmd,
    mutate_args_t,
    read_cmd,
    read_args_t,
    check_fn,
):
    """Generic round-trip: mutate → readback → semantic check."""
    ns = seam_worker.ns
    mutate_args = {k: v.format(ns=ns) for k, v in mutate_args_t.items()}
    read_args = {k: v.format(ns=ns) for k, v in read_args_t.items()}

    await _setup_prerequisites(seam_bridge, seam_worker, mutate_cmd, mutate_args)

    await assert_round_trip(
        seam_bridge,
        mutate_cmd, mutate_args,
        read_cmd, read_args,
        lambda resp: check_fn(resp, ns),
    )
