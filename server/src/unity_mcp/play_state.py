"""Play Mode readiness tracking — epoch + world_ready handshake (MCP-LIFE-004).

PlayReadinessTracker consumes editor state strings and exposes wait_for_ready()
so playtest execution can wait for actual world readiness, not just playing=True.

Backward compat: if Unity doesn't send play_epoch/world_ready, ready = playing.
"""
import asyncio
import time
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Awaitable, Callable

from .tools.editor_state import parse_editor_field, parse_play_epoch, parse_world_ready


@dataclass
class PlayState:
    playing: bool = False
    epoch: int = 0
    ready: bool = False


class PlayReadinessTracker:
    """Track Play Mode readiness across epoch transitions.

    Call update(state_str) on every editor state response.
    Call wait_for_ready(timeout, poll, interval) before dispatching playtests.
    """

    def __init__(self) -> None:
        self._state = PlayState()
        self._has_world_ready_field = False  # set True on first state with world_ready

    @property
    def state(self) -> PlayState:
        return self._state

    def update(self, state_str: str | None) -> None:
        """Parse an editor state string and update internal PlayState."""
        if state_str is None:  # Bug 2: guard against TCP failures destroying state
            return

        playing_raw = (parse_editor_field(state_str, "playing") or "").lower()
        playing = playing_raw == "true"

        epoch = parse_play_epoch(state_str)
        world_ready = parse_world_ready(state_str)

        # Detect if this Unity sends world_ready at all
        if parse_editor_field(state_str, "world_ready") is not None:
            self._has_world_ready_field = True

        # Bug 3: ignore stale epoch — never allow epoch to go backward
        new_epoch = self._state.epoch
        if epoch is not None and epoch >= self._state.epoch:
            new_epoch = epoch

        # Authoritative if world_ready field present, else fallback for old Unity
        ready = playing and world_ready if self._has_world_ready_field else playing

        self._state = PlayState(playing=playing, epoch=new_epoch, ready=ready)

    async def wait_for_ready(
        self,
        timeout: float,
        poll: Callable[[], Awaitable[None]],
        interval: float = 0.1,
        expected_epoch: int | None = None,  # Bug 4: epoch guard
    ) -> None:
        """Wait until ready=True, calling poll() between state checks.

        Raises TimeoutError("play readiness timeout after Xs") if not ready in time.
        Returns immediately if already ready.

        poll: async callable that should trigger a state update (e.g. fetch editor state)
        interval: sleep between polls (seconds)
        expected_epoch: if set, only accept ready signals from this epoch
        """
        def _ready() -> bool:
            return self._state.ready and (
                expected_epoch is None or self._state.epoch == expected_epoch
            )

        def _timeout_error() -> TimeoutError:
            return TimeoutError(
                f"play readiness timeout after {timeout}s "
                f"(playing={self._state.playing}, epoch={self._state.epoch})"
            )

        if _ready():
            return

        deadline = time.monotonic() + timeout
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise _timeout_error()
            try:
                await asyncio.wait_for(poll(), timeout=remaining)  # Bug 1: bound poll()
            except TimeoutError:
                raise _timeout_error() from None
            if _ready():
                return
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise _timeout_error()
            if interval > 0:
                await asyncio.sleep(min(interval, remaining))
