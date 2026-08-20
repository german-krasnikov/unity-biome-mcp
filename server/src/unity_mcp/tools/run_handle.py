"""Durable in-memory handles for Unity test runs.

Handles persist within a server session so run metadata survives timeouts
and transport disconnects. Completed handles expire after TTL to prevent
unbounded memory growth.
"""

import time
from dataclasses import dataclass, field

_DEFAULT_TTL = 600.0  # 10 minutes


@dataclass
class TestRunHandle:
    __test__ = False  # prevent pytest from treating this as a test class
    run_id: str
    request_id: str
    state: str = "dispatched"
    expected_count: int | None = None
    started_at: float = field(default_factory=time.monotonic)
    result: str | None = None
    _completed_at: float | None = field(default=None, repr=False)

    def update(
        self,
        state: str,
        *,
        result: str | None = None,
        expected_count: int | None = None,
    ) -> None:
        """Update handle state; record terminal timestamp for TTL eviction."""
        self.state = state
        if result is not None:
            self.result = result
        if expected_count is not None:
            self.expected_count = expected_count
        if state in ("completed", "passed", "failed", "cancelled") and self._completed_at is None:
            self._completed_at = time.monotonic()


class TestRunRegistry:
    __test__ = False  # prevent pytest collection
    """In-memory store for active and recently-completed test run handles."""

    def __init__(self, ttl: float = _DEFAULT_TTL) -> None:
        self._handles: dict[str, TestRunHandle] = {}
        self._ttl = ttl

    def register(self, run_id: str, request_id: str) -> TestRunHandle:
        """Create and store a new handle; returns it for immediate use."""
        handle = TestRunHandle(run_id=run_id, request_id=request_id)
        self._handles[run_id] = handle
        return handle

    def get(self, run_id: str) -> TestRunHandle | None:
        """Return the handle or None; evicts expired completed handles first."""
        self._evict_expired()
        return self._handles.get(run_id)

    def _evict_expired(self) -> None:
        now = time.monotonic()
        expired = [
            k for k, h in self._handles.items()
            if h._completed_at is not None and now - h._completed_at > self._ttl
        ]
        for k in expired:
            del self._handles[k]
