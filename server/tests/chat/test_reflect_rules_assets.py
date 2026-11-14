"""TDD tests for reflect/rules_assets.py — action-aware no-error rules."""
import pytest
from unity_mcp.reflect import reflect, Mismatch


async def _r(cmd, args, response):
    return await reflect(cmd, args, response, None)


_ASSET_CMDS = [
    "animation", "animator", "particle", "timeline", "material",
    "asset", "prefab", "scriptable_object", "project_settings",
    "shader", "bake", "references", "navmesh_query",
]

# ── Common pattern: each command has read and write actions ───────────────────

_READ_ACTION_MAP = {
    "animation": "get",
    "animator": "get",
    "particle": "get",
    "timeline": "get",
    "material": "get",
    "asset": "find",
    "prefab": "get_overrides",
    "scriptable_object": "get",
    "project_settings": "get",
    "shader": "get",
    "bake": "status",
    "references": None,   # no read actions
    "navmesh_query": "status",
}

_WRITE_ACTION_MAP = {
    "animation": "set",
    "animator": "set_param",
    "particle": "set",
    "timeline": "set",
    "material": "set",
    "asset": "create",
    "prefab": "apply",
    "scriptable_object": "set",
    "project_settings": "set",
    "shader": "set_prop",
    "bake": "start",
    "references": "copy",
    "navmesh_query": "bake",
}


async def test_all_asset_cmds_registered():
    """All 13 asset commands have reflect rules registered."""
    from unity_mcp.reflect import _RULES
    for cmd in _ASSET_CMDS:
        assert cmd in _RULES, f"No rule registered for {cmd}"


async def test_write_action_clean_response_returns_none():
    """Write action + clean response → None for all asset commands."""
    for cmd in _ASSET_CMDS:
        write_action = _WRITE_ACTION_MAP[cmd]
        result = await _r(cmd, {"action": write_action}, "ok: mutation applied")
        assert result is None, f"{cmd}: expected None for clean response"


async def test_write_action_error_returns_mismatch():
    """Write action + Error in response → Mismatch for all asset commands."""
    for cmd in _ASSET_CMDS:
        write_action = _WRITE_ACTION_MAP[cmd]
        result = await _r(cmd, {"action": write_action}, "Error: operation failed")
        assert isinstance(result, Mismatch), f"{cmd}: expected Mismatch for error"


async def test_read_action_skips():
    """Read actions never trigger Mismatch (even with error in response)."""
    for cmd, read_action in _READ_ACTION_MAP.items():
        if read_action is None:
            continue
        result = await _r(cmd, {"action": read_action}, "Error: blah")
        assert result is None, f"{cmd}: read action '{read_action}' should skip"


async def test_no_action_arg_write_path():
    """Missing action arg = write path → mismatch on error response."""
    for cmd in _ASSET_CMDS:
        result = await _r(cmd, {}, "Error: no action provided")
        assert isinstance(result, Mismatch), f"{cmd}: missing action should use write path"


async def test_failed_token_triggers_mismatch():
    """'Failed' token also triggers Mismatch on write actions."""
    for cmd in _ASSET_CMDS:
        write_action = _WRITE_ACTION_MAP[cmd]
        result = await _r(cmd, {"action": write_action}, "Failed to complete operation")
        assert isinstance(result, Mismatch), f"{cmd}: 'Failed' should trigger Mismatch"


async def test_unknown_write_action_uses_write_path():
    """An unknown action not in read_actions uses the write path."""
    result = await _r("animation", {"action": "add_event"}, "Error: clip not found")
    assert isinstance(result, Mismatch)


# ── Spot checks on specific commands ─────────────────────────────────────────

async def test_animation_read_get_events_skips():
    result = await _r("animation", {"action": "get_events"}, "Error: clip not found")
    assert result is None


async def test_shader_read_graph_get_skips():
    result = await _r("shader", {"action": "graph_get"}, "Error: shader failed")
    assert result is None


async def test_material_read_list_shaders_skips():
    result = await _r("material", {"action": "list_shaders"}, "Error: blah")
    assert result is None


async def test_navmesh_query_read_path_skips():
    result = await _r("navmesh_query", {"action": "path"}, "Error: no path")
    assert result is None


async def test_navmesh_query_write_bake_error():
    result = await _r("navmesh_query", {"action": "bake"}, "Error: bake failed")
    assert isinstance(result, Mismatch)
