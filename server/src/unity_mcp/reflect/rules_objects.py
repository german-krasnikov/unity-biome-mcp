"""Reflection rules for object-mutation commands."""
import re
from collections.abc import Awaitable, Callable

from . import Mismatch, _parse_snapshot, _values_close, register_rule

# ── set_property ──────────────────────────────────────────────────────────────

@register_rule("set_property")
async def _rule_set_property(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    if args.get("dry_run") == "true":
        return None
    if "Failed" in response or "Error" in response:
        return None

    snap = _parse_snapshot(response)
    if not snap:
        return None  # no snapshot — silent, can't verify (e.g. FindObject failed)

    prop = args.get("prop", "")
    leaf = prop.rsplit(".", 1)[-1].lower()
    actual = snap.get(leaf)
    if actual is None:
        return None  # field not in snapshot — silent

    expected = str(args.get("value", ""))
    if not _values_close(expected, actual):
        return Mismatch(f"set_property: expected {leaf}={expected}, got {actual}")
    return None


# ── set_property_delta ────────────────────────────────────────────────────────

@register_rule("set_property_delta")
async def _rule_set_property_delta(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    if "Failed" in response or "Error" in response:
        return None
    if " → " not in response:
        return None  # unexpected format — silent
    # Delta is relative, can't verify absolute value without readback. Stay silent.
    return None


# ── set_active ────────────────────────────────────────────────────────────────

@register_rule("set_active")
async def _rule_set_active(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    m = re.search(r"active=(\w+)", response, re.IGNORECASE)
    if not m:
        return None  # no active= token in response — cannot verify

    actual = m.group(1).lower()
    expected = str(args.get("active", "")).lower()
    if expected in ("true", "false") and actual != expected:
        return Mismatch(f"set_active: expected active={expected}, got {actual}")
    return None


# ── create_object ─────────────────────────────────────────────────────────────

@register_rule("create_object")
async def _rule_create_object(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    name = args.get("name", "")
    parent = args.get("parent", "")

    # Parse "Created <name> at <path>" or "Created <path>"
    m = re.search(r"Created\s+\S+\s+at\s+(\S+)", response)
    if not m:
        m2 = re.search(r"^Created\s+(\S+)", response, re.MULTILINE)
        if not m2:
            return None
        path = m2.group(1)
    else:
        path = m.group(1)

    if name and not path.endswith(f"/{name}"):
        return Mismatch(f"create_object: path '{path}' does not end with /{name}")
    if parent and not path.startswith(parent):
        return Mismatch(f"create_object: expected parent '{parent}', got path '{path}'")
    return None


# ── delete_object ─────────────────────────────────────────────────────────────

@register_rule("delete_object")
async def _rule_delete_object(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    # C# ExecDeleteObject takes id (int) and returns "Deleted #12345" — path never echoed.
    if "deleted" not in response.lower():
        return Mismatch("delete_object: response does not confirm deletion")
    return None


# ── manage_component ──────────────────────────────────────────────────────────

@register_rule("manage_component")
async def _rule_manage_component(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    # C# ExecManageComponent returns "Added: {type}. Components: a,b,c" or
    # "Removed: {type}. Remaining: a,b,c" (Cycle 6d format).
    action = args.get("action", "").lower()
    # C# uses "type" key; fall back to "component" for compatibility
    component = args.get("type", args.get("component", ""))
    leaf = component.split(".")[-1].lower() if component else ""
    low = response.lower()

    if action == "add":
        if leaf and leaf not in low:
            return Mismatch(f"manage_component add: '{component}' not confirmed in response")
    elif action == "remove":
        if "removed:" not in low:
            return Mismatch("manage_component remove: expected 'Removed:' in response")
    return None


# ── wire_event ────────────────────────────────────────────────────────────────

@register_rule("wire_event")
async def _rule_wire_event(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    low = response.lower()
    if "wired" not in low and "connected" not in low:
        return Mismatch("wire_event: no 'wired'/'connected' confirmation in response")
    return None
