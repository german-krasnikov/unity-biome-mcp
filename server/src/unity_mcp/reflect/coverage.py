"""Live coverage metric: % of verifiable write commands with reflect rules."""
from ..middleware_types import WRITE_CMDS
from ..tools.tags import tagged
from . import _RULES

# Commands with no meaningful response-parseable invariant (intentionally silent).
_SILENT_CMDS: frozenset[str] = tagged("reflect_silent")


def coverage() -> dict:
    """Return coverage statistics for reflect rules.

    Returns {"covered": int, "total": int, "pct": float, "missing": list[str]}.
    """
    verifiable = WRITE_CMDS - _SILENT_CMDS
    covered = set(_RULES.keys()) & verifiable
    pct = round(100 * len(covered) / max(len(verifiable), 1), 1)
    return {
        "covered": len(covered),
        "total": len(verifiable),
        "pct": pct,
        "missing": sorted(verifiable - covered),
    }
