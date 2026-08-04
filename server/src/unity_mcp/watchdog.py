"""Proactive watchdog: background scan after risky mutations.

Enable with UNITY_MCP_WATCHDOG=1.
"""
import asyncio
import time
from collections.abc import Callable

from .console_levels import PROBLEM_LEVELS
from .middleware_types import is_write

HIGH_BLAST_CMDS = {"delete_object", "scene", "batch"}


class ProactiveWatchdog:
    def __init__(self, send_fn: Callable, *, interval: int = 5,
                 budget_gate: Callable[[], bool] = lambda: True):
        self._send = send_fn
        self.interval = interval
        self._budget_gate = budget_gate
        self._counter: int = 0
        self._pending_alert: str | None = None
        self._last_alert_hash: int | None = None
        self._last_alert_time: float = 0.0
        self._task: asyncio.Task | None = None

    def maybe_trigger(self, cmd: str, args: dict | None = None) -> None:
        if not is_write(cmd, args):
            return
        threshold = 1 if cmd in HIGH_BLAST_CMDS else self.interval
        self._counter += 1
        if self._counter >= threshold:
            self._counter = 0
            if not self._budget_gate():
                return
            if self._task is None or self._task.done():
                try:
                    self._task = asyncio.get_running_loop().create_task(self._scan())
                except RuntimeError:
                    pass  # no event loop in sync context

    async def _scan(self) -> None:
        try:
            refs, console = await asyncio.gather(
                self._send("validate_references", {"path": "/", "depth": "3"}, timeout=5.0),
                self._send("get_console", {"count": "5", "level": PROBLEM_LEVELS}, timeout=5.0),
            )
            # NOTE: do NOT run console through editor_log.corroborate() here — that
            # helper is scoped to compile-error corroboration, not runtime console
            # logs. Applying it here could replace a real runtime error with a
            # stale compile-error dump. Trust get_console() output directly.
            issues = []
            if "ERROR" in refs:
                issues.append(refs.split("\n")[0])
            if console.strip():
                issues.append(f"console: {console.strip()[:80]}")
            if issues:
                msg = "[ISSUES] " + "; ".join(issues[:3])
                h = hash(msg)
                now = time.time()
                if h != self._last_alert_hash or (now - self._last_alert_time) > 60:
                    self._pending_alert = msg
                    self._last_alert_hash = h
                    self._last_alert_time = now
        except Exception:
            pass

    def consume_alert(self) -> str | None:
        alert, self._pending_alert = self._pending_alert, None
        return alert

    async def cancel(self) -> None:
        if self._task and not self._task.done():
            self._task.cancel()
            try:
                await asyncio.wait_for(self._task, 2.0)
            except (asyncio.CancelledError, asyncio.TimeoutError, Exception):
                pass
