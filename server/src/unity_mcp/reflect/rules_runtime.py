"""Reflection rules for runtime / UI mutation commands."""
import re
from collections.abc import Awaitable, Callable  # noqa: TC003

from . import Mismatch, _values_close, register_rule


@register_rule("set_runtime_property")
async def _rule_set_runtime_property(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    # C# RuntimeHelper returns "field=value" (e.g. "health=100"), no snapshot block.
    if "Failed" in response or "Error" in response:
        return None
    field = args.get("field", "")
    if not field:
        return None
    m = re.match(rf"^{re.escape(field)}=(.+)$", response.strip(), re.IGNORECASE)
    if not m:
        return None  # unexpected format — silent, can't verify
    actual = m.group(1).strip()
    expected = str(args.get("value", ""))
    if expected and not _values_close(expected, actual):
        return Mismatch(f"set_runtime_property: expected {field}={expected}, got {actual}")
    return None


# set_rect, set_material, move_to: dead rules removed (field_key mismatch, KISS)
