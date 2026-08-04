"""Retry decision policy -- extracted from UnityBridge (C8 / SOLID finding S2).

Unifies both retry surfaces (exception-path TimeoutError/ConnectionError via
decide(), hint-path Unity 'retry' JSON sentinel via allow_hint_retry()) behind
one is_retry_safe gate, closing the class of bug where a second ad-hoc retry
path could silently bypass the safety gate (C1 / A1).
"""
import asyncio
import time
from collections.abc import Callable
from dataclasses import dataclass

from .bridge_reload_state import DomainReloadTracker
from .compile_state import CompileStateProbe


@dataclass
class RetryPolicy:
    probe: CompileStateProbe
    reload: DomainReloadTracker
    is_retry_safe: Callable[[str], bool]
    max_retries: int

    def decide(self, error: Exception, attempt: int, session_deadline: float,
               cmd: str = "") -> tuple[bool, float, str]:
        """Exception-path decision. Same contract/return shape as the former
        UnityBridge.should_retry() -- (should_retry, delay_s, reason)."""
        from .bridge_socket import DomainReloadError  # local import avoids cycle

        if isinstance(error, DomainReloadError):
            self.probe.mark_recompile_issued()
            self.reload.mark()

        if attempt >= self.max_retries:
            return False, 0.0, "max_retries"
        if time.monotonic() >= session_deadline:
            return False, 0.0, "deadline"

        if isinstance(error, (TimeoutError, asyncio.TimeoutError)) and not self.is_retry_safe(cmd):
            return False, 0.0, "unsafe_to_retry"

        if isinstance(error, DomainReloadError):
            delay = min(2 ** (attempt + 1), 8.0)
            return True, delay, "domain_reload"

        busy = self.reload.is_active() or self.probe_busy()
        if busy:
            delay = min(2 ** (attempt + 1), 8.0)
            return True, delay, "busy"

        # ConnectionRefused during domain reload: Unity TCP server is briefly down.
        # Treat like a busy state with exponential backoff.
        # Early bail: if the process is confirmed dead, retrying 14s is wasteful.
        if isinstance(error, ConnectionRefusedError):
            if self.probe.is_process_dead():
                return False, 0.0, "process_dead"
            delay = min(2 ** (attempt + 1), 8.0)  # 2 / 4 / 8s
            return True, delay, "connection_refused"

        if attempt < 1:
            return True, 1.0, "transient"

        return False, 0.0, "grace_expired"

    def allow_hint_retry(self, cmd: str) -> bool:
        """Hint-path decision (Unity's post-dispatch 'retry' JSON sentinel,
        ClientConnectionHandler.cs:180-187). Same is_retry_safe gate as
        decide() -- this identity is the whole point of the extraction."""
        return self.is_retry_safe(cmd)

    def probe_busy(self) -> bool:
        try:
            return self.probe.has_strong_busy_signal()
        except Exception:
            return False
