"""Speculative pre-fetch: predict next call and fetch eagerly.

Enable with UNITY_MCP_SPECULATION=1.
"""
import asyncio
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass

from .console_levels import PROBLEM_LEVELS


@dataclass
class Prediction:
    cmd: str
    args: dict
    reason: str


class SpeculativeLayer:
    def __init__(self, send_fn: Callable, *, enabled: bool = True):
        self._send = send_fn
        self.enabled = enabled
        self._hits: int = 0
        self._misses: int = 0
        self._last_prediction: str | None = None

    def predict(self, cmd: str, args: dict, result: str) -> Prediction | None:
        if cmd == "set_property" and args.get("prop", "").endswith("Reference"):
            return Prediction(
                "get_component",
                {"path": args["path"], "type": args.get("component", "")},
                "verify ref",
            )
        if cmd == "wire_event":
            return Prediction(
                "validate_references",
                {"path": args.get("path", "/")},
                "confirm wire",
            )
        if cmd == "batch":
            return Prediction("get_console", {"count": "5", "level": PROBLEM_LEVELS}, "batch errors")
        if cmd == "recompile":
            return Prediction("get_compile_errors", {}, "post-compile check")
        return None

    def _hit_rate_ok(self) -> bool:
        total = self._hits + self._misses
        if total < 50:
            return True
        return (self._hits / total) >= 0.4

    async def maybe_prefetch(self, cmd: str, args: dict, result: str) -> str:
        if not self.enabled:
            return result
        if not self._hit_rate_ok():
            return result
        pred = self.predict(cmd, args, result)
        if pred is None:
            return result
        try:
            data = await asyncio.wait_for(self._send(pred.cmd, pred.args), timeout=0.3)
            if len(data) > 800:
                return result
            self._last_prediction = pred.cmd
            return f"{result}\n[PREFETCH {pred.cmd}: {data[:200]}]"
        except Exception:
            return result

    def record_actual_next(self, cmd: str) -> None:
        if self._last_prediction is None:
            return
        from .metrics import METRICS
        METRICS.inc("speculation.predict")
        if cmd == self._last_prediction:
            self._hits += 1
            METRICS.inc("speculation.hit")
        else:
            self._misses += 1
            METRICS.inc("speculation.miss")
        self._last_prediction = None
