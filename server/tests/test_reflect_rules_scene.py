"""TDD tests for reflect/rules_scene.py."""
import pytest
from unity_mcp.reflect import reflect, Mismatch


async def _r(cmd, args, response):
    return await reflect(cmd, args, response, None)


# ── set_parent ────────────────────────────────────────────────────────────────

async def test_set_parent_happy():
    """Path starts with parent prefix → None."""
    result = await _r("set_parent", {"parent": "/Root/A"}, "/Root/A/Child")
    assert result is None


async def test_set_parent_mismatch():
    """Path under different parent → Mismatch."""
    result = await _r("set_parent", {"parent": "/Root/A"}, "/Root/B/Child")
    assert isinstance(result, Mismatch)
    assert "Root/A" in result.msg


async def test_set_parent_no_parent_arg():
    """No parent arg → always None (can't verify root reparent)."""
    result = await _r("set_parent", {}, "/Root/Child")
    assert result is None


async def test_set_parent_empty_response():
    result = await _r("set_parent", {"parent": "/Root"}, "")
    assert result is None


async def test_set_parent_equals_parent():
    """Response equals parent exactly (reparented to root-level object)."""
    result = await _r("set_parent", {"parent": "/Root"}, "/Root")
    assert result is None


# ── rename_object ─────────────────────────────────────────────────────────────

async def test_rename_object_happy():
    result = await _r("rename_object", {"name": "NewName"}, "/Parent/NewName")
    assert result is None


async def test_rename_object_root_level():
    """Root-level rename: response = name only."""
    result = await _r("rename_object", {"name": "NewName"}, "NewName")
    assert result is None


async def test_rename_object_mismatch():
    result = await _r("rename_object", {"name": "NewName"}, "/Parent/OldName")
    assert isinstance(result, Mismatch)
    assert "NewName" in result.msg


async def test_rename_object_no_name_arg():
    result = await _r("rename_object", {}, "/Parent/Whatever")
    assert result is None


async def test_rename_object_empty_response():
    result = await _r("rename_object", {"name": "X"}, "")
    assert result is None


# ── set_sibling_index ─────────────────────────────────────────────────────────

async def test_set_sibling_index_happy():
    result = await _r("set_sibling_index", {"index": "2"}, "ok: /Obj index=2")
    assert result is None


async def test_set_sibling_index_mismatch():
    result = await _r("set_sibling_index", {"index": "2"}, "ok: /Obj index=3")
    assert isinstance(result, Mismatch)
    assert "2" in result.msg and "3" in result.msg


async def test_set_sibling_index_no_token():
    """Unknown format → silent."""
    result = await _r("set_sibling_index", {"index": "2"}, "success")
    assert result is None


async def test_set_sibling_index_no_arg():
    """No index arg → silent."""
    result = await _r("set_sibling_index", {}, "ok: /Obj index=5")
    assert result is None


# ── set_material ──────────────────────────────────────────────────────────────

async def test_set_material_shader_happy():
    result = await _r("set_material", {"shader": "Standard"}, "shader=Standard color=#FF0000FF")
    assert result is None


async def test_set_material_shader_mismatch():
    """shader arg given but 'shader=' not in response → Mismatch."""
    result = await _r("set_material", {"shader": "Standard"}, "ok: material applied")
    assert isinstance(result, Mismatch)
    assert "shader=" in result.msg


async def test_set_material_color_mismatch():
    result = await _r("set_material", {"color": "#FF0000"}, "shader=Standard")
    assert isinstance(result, Mismatch)
    assert "color=" in result.msg


async def test_set_material_no_args():
    """No shader or color arg → silent."""
    result = await _r("set_material", {}, "ok: material set")
    assert result is None


async def test_set_material_error_fail_open():
    result = await _r("set_material", {"shader": "S"}, "Error: material not found")
    assert result is None


# ── autofit_collider ──────────────────────────────────────────────────────────

async def test_autofit_collider_happy():
    result = await _r("autofit_collider", {}, "BoxCollider fitted: bounds (1,1,1)")
    assert result is None


async def test_autofit_collider_mismatch():
    result = await _r("autofit_collider", {}, "ok: collider processed")
    assert isinstance(result, Mismatch)


async def test_autofit_collider_error_fail_open():
    result = await _r("autofit_collider", {}, "Error: no collider found")
    assert result is None


# ── region_clear ──────────────────────────────────────────────────────────────

async def test_region_clear_live_happy():
    result = await _r("region_clear", {}, "DELETED: 3 object(s)")
    assert result is None


async def test_region_clear_dry_run_happy():
    result = await _r("region_clear", {"dry_run": "true"}, "DRY: 3 would be deleted")
    assert result is None


async def test_region_clear_live_mismatch():
    """Live mode expects DELETED, not DRY."""
    result = await _r("region_clear", {}, "DRY: 3 would be deleted")
    assert isinstance(result, Mismatch)


async def test_region_clear_dry_mismatch():
    result = await _r("region_clear", {"dry_run": "true"}, "DELETED: 3 object(s)")
    assert isinstance(result, Mismatch)


# ── transfer_object ───────────────────────────────────────────────────────────

async def test_transfer_object_moved():
    result = await _r("transfer_object", {}, "Moved /Obj → Scene2")
    assert result is None


async def test_transfer_object_arrow():
    result = await _r("transfer_object", {}, "/Obj → Scene2")
    assert result is None


async def test_transfer_object_mismatch():
    result = await _r("transfer_object", {}, "ok: object transferred")
    assert isinstance(result, Mismatch)


async def test_transfer_object_error_fail_open():
    result = await _r("transfer_object", {}, "Error: scene not loaded")
    assert result is None


# ── unwire_event ──────────────────────────────────────────────────────────────

async def test_unwire_event_cleared():
    result = await _r("unwire_event", {}, "Cleared onClick (2 removed)")
    assert result is None


async def test_unwire_event_removed():
    result = await _r("unwire_event", {}, "Removed onClick[0], 1 remaining")
    assert result is None


async def test_unwire_event_mismatch():
    result = await _r("unwire_event", {}, "ok: event unwired")
    assert isinstance(result, Mismatch)


async def test_unwire_event_error_fail_open():
    result = await _r("unwire_event", {}, "Error: event not found")
    assert result is None


# ── auto_wire ─────────────────────────────────────────────────────────────────

async def test_auto_wire_happy():
    result = await _r("auto_wire", {}, "Wired: 3 | Ambiguous: 0 | No match: 0")
    assert result is None


async def test_auto_wire_mismatch():
    result = await _r("auto_wire", {}, "ok: wiring complete")
    assert isinstance(result, Mismatch)


async def test_auto_wire_error_fail_open():
    result = await _r("auto_wire", {}, "Error: no GameObjects")
    assert result is None


# ── undo_last ─────────────────────────────────────────────────────────────────

async def test_undo_last_reverted():
    result = await _r("undo_last", {}, "reverted 2 turn(s)")
    assert result is None


async def test_undo_last_nothing():
    result = await _r("undo_last", {}, "nothing to undo")
    assert result is None


async def test_undo_last_mismatch():
    result = await _r("undo_last", {}, "operation complete")
    assert isinstance(result, Mismatch)


async def test_undo_last_error_fail_open():
    result = await _r("undo_last", {}, "Error: undo failed")
    assert result is None


# ── recompile ─────────────────────────────────────────────────────────────────

async def test_recompile_happy():
    result = await _r("recompile", {}, "ok")
    assert result is None


async def test_recompile_mismatch():
    result = await _r("recompile", {}, "compiling...")
    assert isinstance(result, Mismatch)


async def test_recompile_error_fail_open():
    result = await _r("recompile", {}, "Error: compile failed")
    assert result is None


# ── create_ui ─────────────────────────────────────────────────────────────────

async def test_create_ui_happy():
    result = await _r("create_ui", {}, "Created Button at /Canvas/Button")
    assert result is None


async def test_create_ui_mismatch():
    result = await _r("create_ui", {}, "ok: element added")
    assert isinstance(result, Mismatch)


async def test_create_ui_error_fail_open():
    result = await _r("create_ui", {}, "Error: canvas not found")
    assert result is None


# ── set_rect ──────────────────────────────────────────────────────────────────

async def test_set_rect_clean():
    result = await _r("set_rect", {}, "ok: rect set")
    assert result is None


async def test_set_rect_error():
    result = await _r("set_rect", {}, "Error: RectTransform not found")
    assert isinstance(result, Mismatch)


# ── apply_scene_change ────────────────────────────────────────────────────────

async def test_apply_scene_change_clean():
    result = await _r("apply_scene_change", {}, "applied 5 mutations")
    assert result is None


async def test_apply_scene_change_error():
    result = await _r("apply_scene_change", {}, "Error: plan not found")
    assert isinstance(result, Mismatch)


# ── scene (action-aware) ──────────────────────────────────────────────────────

async def test_scene_write_action_clean():
    result = await _r("scene", {"action": "save"}, "saved")
    assert result is None


async def test_scene_read_action_skips():
    """Read action 'list' never fires → None even if error."""
    result = await _r("scene", {"action": "list"}, "Error: blah")
    assert result is None


async def test_scene_write_action_error():
    result = await _r("scene", {"action": "new"}, "Error: scene failed")
    assert isinstance(result, Mismatch)


# ── scene_environment (action-aware) ─────────────────────────────────────────

async def test_scene_environment_write_clean():
    result = await _r("scene_environment", {"action": "set"}, "ok")
    assert result is None


async def test_scene_environment_read_skips():
    """'get' is a read action → skip."""
    result = await _r("scene_environment", {"action": "get"}, "Error: blah")
    assert result is None


async def test_scene_environment_write_error():
    result = await _r("scene_environment", {"action": "set"}, "Error: environment failed")
    assert isinstance(result, Mismatch)
