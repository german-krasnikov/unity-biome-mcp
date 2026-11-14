# Generated from dsl/data/blastpolicy.data-contract, dsl/data/blastpolicy.core-data
# source-sha256=8b534a0770d4b2824362b0b6f76caa0d85c8536a8350c7824e096c993ea51482
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class BlastPolicy:
    blastHigh: int
    blastDangerous: int


BLAST_POLICY_ROWS: tuple[BlastPolicy, ...] = (
    BlastPolicy(blastHigh=3, blastDangerous=4),
)
