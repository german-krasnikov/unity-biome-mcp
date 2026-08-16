"""Surface reachability: every non-direct_only, non-runtime_only tool must be
routable via batch without producing 'Unknown command'.

Auto-generated from _SPECS at import time to stay in sync as tools are added.

Test failure = C# CommandRouter has no handler for a tool that claims
batch-callable status. Tool-level errors (wrong args, missing object)
are acceptable — they prove the handler exists.
"""
from __future__ import annotations

import pytest

from tests.seams.invariants import parse_batch_result
from unity_mcp.tools.tool_specs import _SPECS

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


# Minimal args for tools that need specific parameters to avoid C# parse errors.
# Preference: use nonexistent paths so write tools fail at the object-lookup level
# rather than with a parse/routing error. The test only checks absence of
# "Unknown command", not whether the command succeeds.
_MINIMAL_ARGS: dict[str, str] = {
    # Tools needing run/request IDs
    "cancel_test_run": "run_id=__seam_nonexistent",
    "get_test_progress": "run_id=__seam_nonexistent",
    "get_test_results": "run_id=__seam_nonexistent",
    "get_test_run": "run_id=__seam_nonexistent",
    "resolve_test_request": "request_id=__seam_nonexistent",
    # Tools needing action enum
    "asset": "action=list",
    "editor": "action=status",
    "prefab": "action=list",
    "scene": "action=get",
    "scene_environment": "action=get",
    "scriptable_object": "action=list",
    "checkpoint": "action=list",
    "animation": "path=/Main Camera action=list",
    "animator": "path=/Main Camera",
    # Tools needing cmd/query
    "get_schema": "cmd=get_status",
    "search_scene": "query=__seam_nonexistent",
    # Tools needing script
    "lint_playtest": "script=ASSERT_CONSOLE_CLEAN",
    # Tools needing from/to
    "serialized_field_rename_audit": "from=OldField to=NewField",
    # Tools needing specific path args (nonexistent — returns object-not-found, not Unknown)
    "auto_wire": "path=/__seam_nonexistent",
    "autofit_collider": "path=/__seam_nonexistent",
    "bake": "path=/__seam_nonexistent",
    "check_colliders": "path=/__seam_nonexistent",
    "create_object": "name=__seam_surface_probe",
    "create_ui": "type=Button parent=/__seam_nonexistent",
    "delete_object": "path=/__seam_nonexistent",
    "export_playtest_aliases_to_defs": "path=/__seam_nonexistent.defs",
    "find_objects": "name=__seam_surface_nonexistent",
    "get_component": "path=/__seam_nonexistent type=Transform",
    "get_components_list": "path=/__seam_nonexistent",
    "get_hierarchy": "depth=1",
    "get_object_detail": "path=/__seam_nonexistent",
    "get_spatial_context": "path=/__seam_nonexistent",
    "get_unity_events": "path=/__seam_nonexistent",
    "inspect": "paths=/Main Camera",
    "list_events": "path=/__seam_nonexistent",
    "manage_component": "path=/__seam_nonexistent type=Rigidbody action=add",
    "material": "path=/__seam_nonexistent action=get",
    "material_audit": "path=/__seam_nonexistent",
    "menu": "name=File/Save",
    "object_diff": "path=/__seam_nonexistent",
    "particle": "path=/__seam_nonexistent",
    "ping_object": "path=/__seam_nonexistent",
    "project_settings": "path=ProjectSettings/TagManager",
    "references": "action=get path=/__seam_nonexistent",
    "region_clear": "region=__seam_nonexistent",
    "rename_object": "path=/__seam_nonexistent name=__seam_renamed",
    "scene_diff": "path=/__seam_nonexistent",
    "set_active": "path=/__seam_nonexistent active=false",
    "set_material": "path=/__seam_nonexistent material=Default-Material",
    "set_parent": "path=/__seam_nonexistent parent=/",
    "set_property": "path=/__seam_nonexistent component=Transform prop=m_LocalPosition value=0,0,0",
    "set_property_delta": "path=/__seam_nonexistent component=Transform prop=m_LocalPosition.x delta=1",
    "set_rect": "path=/__seam_nonexistent",
    "set_sibling_index": "path=/__seam_nonexistent index=0",
    "shader": "path=/__seam_nonexistent",
    "spatial_query": "path=/__seam_nonexistent",
    "sync_playtest_aliases_from_defs": "path=/__seam_nonexistent.defs",
    "timeline": "path=/__seam_nonexistent",
    "transfer_object": "path=/__seam_nonexistent to=/__seam_target",
    "undo_last": "",
    "unwire_event": "path=/__seam_nonexistent event=onClick",
    "validate_references": "path=/__seam_nonexistent",
    "validate_triggers": "path=/__seam_nonexistent",
    "wire_event": "path=/__seam_nonexistent event=onClick target=/__seam_tgt method=SetActive",
    "inspect_uitk": "path=/__seam_nonexistent",
    "uitk_element": "path=/__seam_nonexistent action=get",
    "attach_uitk": "path=/__seam_nonexistent",
    "batch": "commands=get_status",
}


def _build_surface_cases() -> list:
    """Auto-generate parametrized cases from _SPECS.

    Excludes:
    - direct_only tools (batch pre-filters them; routing not via C# CommandRouter)
    - runtime_only tools (require Play Mode; out of scope for static surface test)
    - _INTERNAL category (protocol-only, not MCP tools)
    """
    cases = []
    for name, spec in sorted(_SPECS.items()):
        if spec.direct_only or spec.runtime_only or spec.category == "_INTERNAL":
            continue
        args = _MINIMAL_ARGS.get(name, "")
        cases.append(pytest.param(name, args, id=name))
    return cases


SURFACE_CASES = _build_surface_cases()


@pytest.mark.parametrize("cmd,args_str", SURFACE_CASES)
async def test_tool_reachable_via_batch(seam_bridge, seam_worker, cmd, args_str):
    """D7 routing seam: tool must not return 'Unknown command' via batch.

    Failure = C# CommandRouter has no handler registered for this tool.
    Tool-level errors (wrong args, missing object) are acceptable — they
    prove the handler exists. Only 'Unknown command' indicates routing failure.
    """
    line = f"{cmd} {args_str}".strip()
    resp = await seam_bridge.send("batch", {"commands": line})
    text = resp.get("data", "") or resp.get("err", "")

    # Some tools may produce an ok:1 summary before the [0] line;
    # others may return ok=False with err message at the response level.
    # We check both the data body and err field.
    assert "Unknown command" not in text, (
        f"D7: '{cmd}' claims batch-capable (not direct_only) but C# CommandRouter "
        f"returned 'Unknown command': {text[:300]}"
    )

    # Best-effort: verify summary exists so we know the response is valid batch format
    # (not a connection-level error). OK to skip if response is non-batch error.
    if "ok:" in text:
        result = parse_batch_result(text)
        assert result.is_coherent(), (
            f"D7: '{cmd}' batch response summary/body mismatch: "
            f"summary ok:{result.ok_count} err:{result.err_count} "
            f"vs body ok:{result.body_ok} err:{result.body_err}"
        )
