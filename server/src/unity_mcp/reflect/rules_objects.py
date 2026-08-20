"""Reflection rules for object-mutation commands."""
import re
from collections.abc import Awaitable, Callable  # noqa: TC003

from . import Mismatch, _parse_snapshot, _values_close, register_rule
from .factory import _has_error

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
    if _has_error(response):
        return Mismatch(f"set_active: error in response: {response[:80]!r}")
    m = re.search(r"active=(\w+)", response, re.IGNORECASE)
    if not m:
        return None  # no active= token in response — cannot verify

    actual = m.group(1).lower()
    expected = str(args.get("active", "")).lower()
    if expected in ("true", "false") and actual != expected:
        return Mismatch(f"set_active: expected active={expected}, got {actual}")
    return None


# ── create_object ─────────────────────────────────────────────────────────────

def _extract_created_path(response: str) -> str | None:
    """Parse 'Created <name> at <path>' or 'Created <path>' — O(n), no backtracking.

    Replaces S8786-flagged regex `\\S[^\\n]*\\s+at\\s+` which was O(n²) due to
    overlapping quantifiers ([^\\n]* and \\s+ both matching space characters).
    """
    for line in response.splitlines():
        stripped = line.lstrip()
        if not stripped.startswith("Created "):
            continue
        rest = stripped[8:]  # skip "Created "
        if not rest or rest[0].isspace():
            continue  # require non-space first char (mirrors original \S)
        at_idx = rest.find(" at ")
        if at_idx >= 0:
            raw = rest[at_idx + 4:]
            # Strip optional bracket metadata suffix e.g. " [inst=12345]"
            bracket = raw.find(" [")
            return (raw[:bracket] if bracket >= 0 else raw).strip()
        # Fallback: single-word path "Created /path/to/obj"
        parts = rest.split()
        return parts[0] if parts else None
    return None


@register_rule("create_object")
async def _rule_create_object(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    name = args.get("name", "")
    parent = args.get("parent", "")

    path = _extract_created_path(response)
    if path is None:
        return None

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
    if _has_error(response):
        return Mismatch(f"delete_object: error in response: {response[:80]!r}")
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

    if _has_error(response):
        return None  # error response — can't verify

    if action == "add":
        if leaf and not re.search(rf"\b{re.escape(leaf)}\b", low):
            return Mismatch(f"manage_component add: '{component}' not confirmed in response")
    elif action == "remove" and "removed:" not in low:
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
