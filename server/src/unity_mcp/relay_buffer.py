"""Reconnect-safe ring-buffer with long-poll event delivery."""
import asyncio
from collections import deque

from .cli_session import BufLine

MAX_BUF = 500  # deque maxlen — ~30s of dense streaming at 15 lines/s


class RelayBuffer:
    """Append-only log with monotonic seq IDs and long-poll support."""

    def __init__(self) -> None:
        self._buf:      deque[BufLine] = deque(maxlen=MAX_BUF)
        self._next_seq: int            = 0
        self._dropped:  int            = 0
        self._new_data: asyncio.Event  = asyncio.Event()

    def enqueue(self, line: str) -> None:
        if len(self._buf) == self._buf.maxlen:
            self._dropped += 1  # B2: track silent eviction
        line = line.replace("\n", "\\n").replace("\r", "\\r")
        self._buf.append(BufLine(seq=self._next_seq, text=line))
        self._next_seq += 1
        self._new_data.set()  # unblock long-poll waiters

    async def cmd_events(self, after_seq: int, timeout_ms: int) -> str:
        """Return buffered lines after after_seq, waiting up to timeout_ms."""
        lines = [b for b in self._buf if b.seq > after_seq]
        if not lines and timeout_ms > 0:
            self._new_data.clear()
            try:
                await asyncio.wait_for(self._new_data.wait(),
                                       timeout=timeout_ms / 1000)
                lines = [b for b in self._buf if b.seq > after_seq]
            except TimeoutError:
                pass
        return "".join(f"{b.seq}\n{b.text}\n" for b in lines)

    def clear(self) -> None:
        """Clear on session switch — seq stays monotonic."""
        self._buf.clear()

    def status_tail(self) -> str:
        """Return '|seq=N|buf=N|dropped=N' for status command."""
        return f"|seq={self._next_seq - 1}|buf={len(self._buf)}|dropped={self._dropped}"
