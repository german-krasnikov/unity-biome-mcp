"""Graceful degradation ladder helper.

degrade(feature, steps) — runs steps until one returns truthy.
Zero overhead on success path. NO retry inside ladder.
"""
import inspect
import os
from collections.abc import Callable  # noqa: TC003
from typing import Any

from .metrics import METRICS


async def degrade(
    feature: str,
    steps: list[tuple[str, Callable]],
) -> tuple[str, Any]:
    """Run steps until one returns truthy. Returns (step_name, value).

    Each step is (name, callable) returning value | None | coroutine. asyncio.Task not supported.
    First truthy result wins. If all fail → (last_step_name, None).

    Side effects on each failed step:
      METRICS.inc('degraded.{feature}.{step}')
      METRICS.event('degraded', feature=..., step=..., reason=...)
    On any ladder fall (at least one step failed before success or exhaustion):
      METRICS.inc('degraded.{feature}')
    """
    disabled = os.environ.get("UNITY_MCP_DEGRADE_DISABLED") == "1"

    last_name = steps[-1][0] if steps else ""
    fell = False

    for name, fn in steps:
        reason = "none"
        if disabled:
            # No try/except — exceptions propagate raw; None still tries next rung
            result = fn()
            if inspect.iscoroutine(result):
                result = await result
        else:
            try:
                result = fn()
                if inspect.iscoroutine(result):
                    result = await result
            except Exception as e:
                result = None
                reason = f"raised:{type(e).__name__}"

        if result:
            if fell:
                METRICS.inc(f"degraded.{feature}")
            return (name, result)

        # Step returned None (or falsy)
        fell = True
        METRICS.inc(f"degraded.{feature}.{name}")
        if not disabled:
            METRICS.event("degraded", feature=feature, step=name, reason=reason)
        last_name = name

    # All steps exhausted
    if fell:
        METRICS.inc(f"degraded.{feature}")
    return (last_name, None)


def wrap_degraded(feature: str, step: str, value: Any | None) -> str:
    """Format degraded surface marker.

    value is None → '[DEGRADED:feature:step:no_fallback]'
    value is truthy → '[DEGRADED:feature:step]\\n<value>'
    """
    if value is None:
        return f"[DEGRADED:{feature}:{step}:no_fallback]"
    return f"[DEGRADED:{feature}:{step}]\n{value}"
