"""Reconnect-safe ring-buffer with long-poll event delivery."""
import asyncio
from collections import deque

from .cli_session import BufLine

MAX_BUF = 500  # deque maxlen — ~30s of dense streaming at 15 lines/s

# DSL gap analysis #7: the relay v2 wire frame is "{seq}\n{text}\n" repeated,
# parsed on the other side by unity-plugin/Editor/Chat/CLI/RelayChatProcess.cs
# (ParseV2Events): data.Split('\n') consumed as (seq, text) pairs, no unescape.
# That split is only safe because enqueue() below guarantees text never
# contains a raw newline/CR. Covered by server/tests/test_relay_frame_contract.py
# (Python side — C# has no separate copy of this encoding to drift from, it
# only relies on the invariant, so the test pins both the format and the
# invariant against a mirror of the C# decode loop).
FRAME_SEP = "\n"


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
        # Frame-safety net: the wire frame below splits on FRAME_SEP, so text
        # must never contain a raw newline/CR (see module-level contract note).
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
        return "".join(f"{b.seq}{FRAME_SEP}{b.text}{FRAME_SEP}" for b in lines)

    def clear(self) -> None:
        """Clear on session switch — seq stays monotonic."""
        self._buf.clear()

    def status_tail(self) -> str:
        """Return '|seq=N|buf=N|dropped=N' for status command."""
        return f"|seq={self._next_seq - 1}|buf={len(self._buf)}|dropped={self._dropped}"
