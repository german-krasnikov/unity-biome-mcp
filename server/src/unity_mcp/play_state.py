"""Play Mode readiness tracking — epoch + world_ready handshake (MCP-LIFE-004).

PlayReadinessTracker consumes editor state strings and exposes wait_for_ready()
so playtest execution can wait for actual world readiness, not just playing=True.

Backward compat: if Unity doesn't send play_epoch/world_ready, ready = playing.
"""
import asyncio
import time
from dataclasses import dataclass
from collections.abc import Callable, Awaitable

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
        playing_raw = (parse_editor_field(state_str, "playing") or "").lower()
        playing = playing_raw == "true"

        epoch = parse_play_epoch(state_str)
        world_ready = parse_world_ready(state_str)

        # Detect if this Unity sends world_ready at all
        if parse_editor_field(state_str, "world_ready") is not None:
            self._has_world_ready_field = True

        new_epoch = epoch if epoch is not None else self._state.epoch

        if self._has_world_ready_field:
            # Authoritative: ready only when playing + world first frame done
            ready = playing and world_ready
        else:
            # Fallback for old Unity: ready = playing (no world_ready protocol)
            ready = playing

        self._state = PlayState(playing=playing, epoch=new_epoch, ready=ready)

    async def wait_for_ready(
        self,
        timeout: float,
        poll: Callable[[], Awaitable[None]],
        interval: float = 0.1,
    ) -> None:
        """Wait until ready=True, calling poll() between state checks.

        Raises TimeoutError("play readiness timeout after Xs") if not ready in time.
        Returns immediately if already ready.

        poll: async callable that should trigger a state update (e.g. fetch editor state)
        interval: sleep between polls (seconds)
        """
        if self._state.ready:
            return

        deadline = time.monotonic() + timeout
        while True:
            await poll()
            if self._state.ready:
                return
            if time.monotonic() >= deadline:
                raise TimeoutError(
                    f"play readiness timeout after {timeout}s "
                    f"(playing={self._state.playing}, epoch={self._state.epoch})"
                )
            if interval > 0:
                await asyncio.sleep(interval)
