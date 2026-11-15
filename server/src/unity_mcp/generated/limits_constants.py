# Generated from dsl/data/limits.data-contract, dsl/data/limits.core-data
# source-sha256=eaa68383b969da7b59321a17e3702b4f7a24db99c46c873ba8439937dae6e9f0
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class Limits:
    retryMax: int
    maxPaths: int
    maxWrites: int
    maxComponents: int
    maxDistillCache: int
    maxMetricsKeys: int


LIMITS_ROWS: tuple[Limits, ...] = (
    Limits(
        retryMax=32,
        maxPaths=256,
        maxWrites=128,
        maxComponents=256,
        maxDistillCache=64,
        maxMetricsKeys=512,
    ),
)
