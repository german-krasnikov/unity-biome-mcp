# Generated from dsl/data/testrun.data-contract, dsl/data/testrun.core-data
# source-sha256=beb4218ac3b7e10cf98fb1ad2136e2d904046a4e3d5aad2969b27760aa13ff5f
# Edit the source, then run biome generate.

from dataclasses import dataclass
from enum import StrEnum


class Lifecycle(StrEnum):
    PREPARED = "prepared"
    DISPATCHED = "dispatched"
    RUNNING = "running"
    FINALIZING = "finalizing"
    TERMINAL = "terminal"


class RunOutcome(StrEnum):
    PASSED = "passed"
    FAILED = "failed"
    CANCELLED = "cancelled"
    INCOMPLETE = "incomplete"
    INVALID = "invalid"
    DISPATCH_FAILED = "dispatch_failed"


class LeafOutcome(StrEnum):
    PASSED = "passed"
    FAILED = "failed"
    SKIPPED = "skipped"
    INCONCLUSIVE = "inconclusive"
    CANCELLED = "cancelled"
    INVALID = "invalid"


class Health(StrEnum):
    HEALTHY = "healthy"
    RELOADING = "reloading"
    NO_TEST_PROGRESS = "no_test_progress"
    EDITOR_UNRESPONSIVE = "editor_unresponsive"
    SUSPECTED_STALL = "suspected_stall"


@dataclass(frozen=True, slots=True, kw_only=True)
class TerminalOutcome:
    outcome: str
    isFailure: bool


TERMINAL_OUTCOME_ROWS: tuple[TerminalOutcome, ...] = (
    TerminalOutcome(outcome="passed", isFailure=False),
    TerminalOutcome(outcome="failed", isFailure=True),
    TerminalOutcome(outcome="cancelled", isFailure=True),
    TerminalOutcome(outcome="incomplete", isFailure=True),
    TerminalOutcome(outcome="invalid", isFailure=True),
    TerminalOutcome(outcome="dispatch_failed", isFailure=True),
)
