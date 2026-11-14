# Generated from dsl/data/timing.data-contract, dsl/data/timing.core-data
# source-sha256=bd4d0b1e81c0176b3c5f637f0d5cb6777ff739585a2f7e557db8045ed3c8a8ef
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class Timing:
    startupGraceS: float
    domainReloadExpiryS: float
    backoffMinS: float
    backoffMaxS: float
    reloadBackoffS: float
    orphanCheckIntervalS: float
    busyTickS: float
    idleTickS: float
    probeGraceS: float


TIMING_ROWS: tuple[Timing, ...] = (
    Timing(
        startupGraceS=90.0,
        domainReloadExpiryS=90.0,
        backoffMinS=5.0,
        backoffMaxS=60.0,
        reloadBackoffS=1.0,
        orphanCheckIntervalS=1.0,
        busyTickS=5.0,
        idleTickS=2.0,
        probeGraceS=30.0,
    ),
)
