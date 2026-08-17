"""Asymmetric Reflection: compare mutation args (expectation) vs response snapshot (observation).

On mismatch, returns Mismatch(msg). Silent on match or no rule.
"""
import math
import re
from collections.abc import Awaitable, Callable
from dataclasses import dataclass

from unity_mcp.metrics import METRICS

# ── Public types ──────────────────────────────────────────────────────────────

@dataclass(frozen=True)
class Mismatch:
    msg: str


ReflectFn = Callable[[dict, str, Callable[..., Awaitable[str]]], Awaitable[Mismatch | None]]
_RULES: dict[str, ReflectFn] = {}


def _registry_count() -> int:
    return len(_RULES)


# ── Rule registration ─────────────────────────────────────────────────────────

def register_rule(cmd: str) -> Callable[[ReflectFn], ReflectFn]:
    def decorator(fn: ReflectFn) -> ReflectFn:
        _RULES[cmd] = fn
        return fn
    return decorator


# ── Shared helpers ────────────────────────────────────────────────────────────

_SNAP_LINE = re.compile(r"^\s*([A-Za-z_][\w]*)\s*:\s*(.+?)\s*$")


def _parse_snapshot(response: str) -> dict[str, str]:
    out: dict[str, str] = {}
    in_snap = False
    for line in response.splitlines():
        if line.startswith("---"):
            in_snap = True
            continue
        if not in_snap:
            continue
        m = _SNAP_LINE.match(line)
        if m:
            out[m.group(1).lower()] = m.group(2)
    return out


def _values_close(expected: str, actual: str) -> bool:
    if expected == actual:
        return True
    # Boolean normalisation
    bool_map = {"true": True, "false": False}
    if expected.lower() in bool_map and actual.lower() in bool_map:
        return bool_map[expected.lower()] == bool_map[actual.lower()]
    # ObjectReference: C# appends " $HEX" (new) or " #instanceId" (legacy) — prefix match
    if (actual.startswith(expected + " #") or actual.startswith(expected + "#")
            or actual.startswith(expected + " $")):
        return True
    # Vector form (x,y,z) — also handles RGB vs RGBA (prefix match on components)
    e = expected.strip("() ")
    a = actual.strip("() ")
    e_parts = [p.strip() for p in e.split(",")]
    a_parts = [p.strip() for p in a.split(",")]
    if len(e_parts) > 1 and len(a_parts) >= len(e_parts):
        try:
            prefix_match = all(
                math.isclose(float(ep), float(ap), rel_tol=1e-4, abs_tol=1e-5)
                for ep, ap in zip(e_parts, a_parts, strict=False)
            )
            if prefix_match:
                return True
        except ValueError:
            pass
    # Scalar float
    try:
        return math.isclose(float(expected), float(actual), rel_tol=1e-4, abs_tol=1e-5)
    except ValueError:
        pass
    return False


# ── Entry point ───────────────────────────────────────────────────────────────

async def reflect(
    cmd: str,
    args: dict,
    response: str,
    send_fn: Callable[..., Awaitable[str]],
) -> Mismatch | None:
    METRICS.inc("reflect.checked")
    rule = _RULES.get(cmd)
    if rule is None:
        METRICS.inc("reflect.skipped_no_rule")
        return None
    try:
        result = await rule(args, response, send_fn)
        if result is not None:
            METRICS.inc("reflect.mismatch")
        return result
    except Exception:
        METRICS.inc("reflect.rule_crashed")
        return None


# ── Auto-discovery: trigger rule registration ─────────────────────────────────
from . import rules_assets, rules_batch, rules_objects, rules_runtime, rules_scene, rules_system  # noqa: E402,F401
