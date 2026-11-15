# Generated from dsl/data/timing.data-contract, dsl/data/timing.core-data
# source-sha256=d3a3d94fe5d0ddff17637b5e72c889662f2660f28a528afc234a8d131ec7794b
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
    hardDeadlineS: float
    reinstallTimeoutS: float
    updateCacheTtlS: float
    defaultCommandTimeoutS: float
    playModeCacheTtlS: float
    loginPathRetryTtlS: float
    focusHintAfterS: float
    aliveCheckTtlS: float
    retryDedupTtlS: float
    reloadTier1PollS: float
    reloadTier4PollS: float
    reloadPollIntervalS: float
    reloadTier2SleepS: float


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
        hardDeadlineS=450.0,
        reinstallTimeoutS=300.0,
        updateCacheTtlS=86400.0,
        defaultCommandTimeoutS=30.0,
        playModeCacheTtlS=5.0,
        loginPathRetryTtlS=30.0,
        focusHintAfterS=15.0,
        aliveCheckTtlS=30.0,
        retryDedupTtlS=5.0,
        reloadTier1PollS=40.0,
        reloadTier4PollS=45.0,
        reloadPollIntervalS=1.0,
        reloadTier2SleepS=8.0,
    ),
)
