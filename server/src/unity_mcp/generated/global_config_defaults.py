# Generated from dsl/data/globalconfig.data-contract, dsl/data/globalconfig.core-data
# source-sha256=f56445d7d4e87e42366dada229f1b9ef14e56447235afad2893ab951740a6350
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class GlobalConfigDefaults:
    idleTimeoutMin: int
    idleAutoSuspend: bool
    bridgeTerminateOrphan: bool
    bridgeOrphanGraceMin: int


GLOBAL_CONFIG_DEFAULTS_ROWS: tuple[GlobalConfigDefaults, ...] = (
    GlobalConfigDefaults(
        idleTimeoutMin=30,
        idleAutoSuspend=True,
        bridgeTerminateOrphan=True,
        bridgeOrphanGraceMin=2,
    ),
)
