# Generated from dsl/data/testrun.data-contract, dsl/data/testrun.core-data
# source-sha256=31d30200cae9a76f1caa5ada846e683a23cdd94b9b0abe9014f7c66fd73c4112
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


class EventType(StrEnum):
    RUN_STARTED = "run_started"
    MANIFEST_SEALED = "manifest_sealed"
    TEST_STARTED = "test_started"
    TEST_FINISHED = "test_finished"
    RUN_FINISHED = "run_finished"
    INFRASTRUCTURE_ERROR = "infrastructure_error"
    DISPATCH_FAILED = "dispatch_failed"
    CANCEL_REQUESTED = "cancel_requested"
    ABANDONED = "abandoned"
    CANCELLED = "cancelled"
    DOMAIN_RELOADING = "domain_reloading"
    RUN_FINALIZED = "run_finalized"


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
