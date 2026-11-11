"""Reflection rules for scene-object mutation commands that compare an
echoed value against an arg (path-echo, key=value) — not just a literal
token's presence, so they stay hand-written (SV5: moved the 9 token/arg
rules that used to live here, plus set_material, to the EXPECT catalog —
dsl/tools.biome, reflect/rules_catalog.py).

set_rect/apply_scene_change/scene/scene_environment moved to reflect_noerror's
single installer loop (rules_assets.py) — SV3-DESIGN.md §3.7 #4.
"""
import re

from . import Mismatch, register_rule

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
