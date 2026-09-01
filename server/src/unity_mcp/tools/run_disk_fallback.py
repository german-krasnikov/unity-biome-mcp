"""Last-resort disk read of a durable test-run summary.

Unity's TestRunFinalizationCoordinator writes summary.json at both of its
finalization exits, independent of the TCP command queue a starved Editor
can stall (see ARC-2). This module is a single-shot fallback read for
run_tests_wait's TIMEOUT path — pure I/O, no JSON parsing or terminal-state
validation. testing.py reuses _decode_snapshot/_terminal_snapshot_error
against the text this returns, so old-schema and non-terminal snapshots are
handled there, not here.
"""

from pathlib import Path


def summary_path(project_path: Path, run_id: str) -> Path:
    """Return the durable summary.json path TestRunStore.Reconcile writes."""
    return Path(project_path) / "Library" / "UnityMCP" / "TestRuns" / "runs" / run_id / "summary.json"


def read_terminal_summary(project_path: Path, run_id: str) -> str | None:
    """Read raw summary.json text; None if missing, unreadable, or empty."""
    try:
        text = summary_path(project_path, run_id).read_text(encoding="utf-8")
    except OSError:
        return None
    return text or None
