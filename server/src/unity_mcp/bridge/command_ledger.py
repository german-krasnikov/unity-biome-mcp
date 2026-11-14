"""Command ledger — tracks per-op_id delivery/command lifecycle (MCP-TRANS-008)."""
import enum
import time
from dataclasses import dataclass


class DeliveryState(enum.Enum):
    """Tracks per-attempt write/read lifecycle for idempotent retry (P-322)."""
    UNSENT = "unsent"       # not yet written to socket
    SENT = "sent"           # accepted by writer; response not yet received
    DELIVERED = "delivered" # response received
    FAILED = "failed"       # error before or after write


class CommandStatus(enum.Enum):
    """Persistent lifecycle state for a single op_id in the CommandLedger."""
    NOT_FOUND = "not_found"    # op_id unknown (never sent or TTL expired)
    SENT = "sent"              # queued but not yet written to TCP
    ACCEPTED = "accepted"      # writer accepted frame; Unity may have received it
    COMPLETED = "completed"    # response received (success or Unity-level error)
    FAILED = "failed"          # terminal transport error; no response


@dataclass
class _LedgerEntry:
    status: CommandStatus
    result: dict | None = None
    ts: float | None = None

    def __post_init__(self):
        if self.ts is None:
            self.ts = time.monotonic()


_STATUS_ORDER = {
    CommandStatus.NOT_FOUND: 0,
    CommandStatus.SENT: 1,
    CommandStatus.ACCEPTED: 2,
    CommandStatus.COMPLETED: 3,
    CommandStatus.FAILED: 3,  # same level as COMPLETED (both terminal)
}


class CommandLedger:
    """Asyncio-safe (single event-loop thread) dict-backed ledger tracking op_id → (status, result).

    TTL-based cleanup prevents unbounded memory growth.
    Lives on the bridge instance; survives close/reconnect cycles.
    """
    TTL = 300.0  # seconds; matches DedupRegistry.TtlSeconds on C# side

    def __init__(self) -> None:
        self._store: dict[str, _LedgerEntry] = {}

    def record(self, op_id: str, status: CommandStatus,
               result: dict | None = None) -> None:
        """Upsert entry; never regresses status (forward-only)."""
        if not op_id:
            return
        existing = self._store.get(op_id)
        if existing and _STATUS_ORDER.get(status, 0) < _STATUS_ORDER.get(existing.status, 0):
            return  # never regress
        self._store[op_id] = _LedgerEntry(
            status=status,
            result=result if result is not None else (existing.result if existing else None),
            ts=existing.ts if existing else time.monotonic(),
        )
        self._evict()

    def get(self, op_id: str) -> tuple[CommandStatus, dict | None]:
        entry = self._store.get(op_id)
        if entry is None:
            return CommandStatus.NOT_FOUND, None
        if time.monotonic() - entry.ts > self.TTL:
            del self._store[op_id]
            return CommandStatus.NOT_FOUND, None
        return entry.status, entry.result

    def _evict(self) -> None:
        cutoff = time.monotonic() - self.TTL
        stale = [k for k, v in self._store.items() if v.ts < cutoff]
        for k in stale:
            del self._store[k]
