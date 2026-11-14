# Generated from dsl/data/syncstate.data-contract, dsl/data/syncstate.core-data
# source-sha256=ec4d0a7114efc8070545aeef5362ec35082f1c06efd02d55e3d6f629f98394d1
# Edit the source, then run biome generate.

from dataclasses import dataclass
from enum import StrEnum


class SyncState(StrEnum):
    IDLE = "idle"
    COMPILING = "compiling"
    READY = "ready"
    FAILED = "failed"
    RELOADING = "reloading"


@dataclass(frozen=True, slots=True, kw_only=True)
class SyncStateOrigin:
    state: SyncState
    origin: str


SYNC_STATE_ORIGIN_ROWS: tuple[SyncStateOrigin, ...] = (
    SyncStateOrigin(
        state=SyncState.IDLE,
        origin="SyncHelper.cs (default SessionState)",
    ),
    SyncStateOrigin(
        state=SyncState.COMPILING,
        origin="SyncHelper.cs (RequestScriptCompilation)",
    ),
)
