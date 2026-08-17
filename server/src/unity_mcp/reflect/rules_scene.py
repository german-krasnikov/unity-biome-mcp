"""Reflection rules for scene-object mutation commands."""
import re

from ..middleware_types import ACTION_READS
from . import Mismatch, register_rule
from .factory import _has_error, make_action_guarded_no_error_rule, make_no_error_rule

# ── Group A: path-echo verification ──────────────────────────────────────────


@register_rule("set_parent")
async def _rule_set_parent(args: dict, response: str, send_fn) -> Mismatch | None:
    parent = args.get("parent", "")
    if not parent or not response:
        return None
    if not (response.startswith(parent + "/") or response == parent):
        return Mismatch(f"set_parent: expected path under '{parent}', got '{response}'")
    return None


@register_rule("rename_object")
async def _rule_rename_object(args: dict, response: str, send_fn) -> Mismatch | None:
    name = args.get("name", "")
    if not name or not response:
        return None
    if not (response.endswith("/" + name) or response == name):
        return Mismatch(f"rename_object: path '{response}' does not end with /{name}")
    return None


# ── Group B: key=value tokens ─────────────────────────────────────────────────


@register_rule("set_sibling_index")
async def _rule_set_sibling_index(args: dict, response: str, send_fn) -> Mismatch | None:
    m = re.search(r"index=(\d+)", response)
    if not m:
        return None  # unknown format — silent
    actual = int(m.group(1))
    expected = args.get("index")
    if expected is not None and int(expected) != actual:
        return Mismatch(f"set_sibling_index: expected index={expected}, got {actual}")
    return None


@register_rule("set_material")
async def _rule_set_material(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    shader = args.get("shader")
    color = args.get("color")
    if shader and "shader=" not in response:
        return Mismatch("set_material: expected 'shader=' token in response")
    if color and "color=" not in response:
        return Mismatch("set_material: expected 'color=' token in response")
    return None


# ── Group C: typed keyword confirmation ───────────────────────────────────────


@register_rule("autofit_collider")
async def _rule_autofit_collider(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "fitted" not in response.lower():
        return Mismatch("autofit_collider: expected 'fitted' in response")
    return None


@register_rule("region_clear")
async def _rule_region_clear(args: dict, response: str, send_fn) -> Mismatch | None:
    dry = str(args.get("dry_run", "false")).lower() == "true"
    expected_token = "DRY" if dry else "DELETED"
    if expected_token not in response:
        return Mismatch(f"region_clear: expected '{expected_token}' in response")
    return None


@register_rule("transfer_object")
async def _rule_transfer_object(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "Moved" not in response and "→" not in response:
        return Mismatch("transfer_object: expected 'Moved' or '→' in response")
    return None


@register_rule("unwire_event")
async def _rule_unwire_event(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "Cleared" not in response and "Removed" not in response:
        return Mismatch("unwire_event: expected 'Cleared' or 'Removed' in response")
    return None


@register_rule("auto_wire")
async def _rule_auto_wire(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "Wired:" not in response:
        return Mismatch("auto_wire: expected 'Wired:' in response")
    return None


@register_rule("undo_last")
async def _rule_undo_last(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    low = response.lower()
    if "reverted" not in low and "nothing" not in low:
        return Mismatch("undo_last: expected 'reverted' or 'nothing' in response")
    return None


@register_rule("recompile")
async def _rule_recompile(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "ok" not in response.lower():
        return Mismatch("recompile: expected 'ok' in response")
    return None


@register_rule("create_ui")
async def _rule_create_ui(args: dict, response: str, send_fn) -> Mismatch | None:
    if _has_error(response):
        return None
    if "Created" not in response:
        return Mismatch("create_ui: expected 'Created' in response")
    return None


make_no_error_rule("set_rect")
make_no_error_rule("apply_scene_change")

# ── Group D: action-aware (scene and scene_environment) ───────────────────────

make_action_guarded_no_error_rule("scene", ACTION_READS.get("scene", frozenset()))
make_action_guarded_no_error_rule("scene_environment", ACTION_READS.get("scene_environment", frozenset()))
