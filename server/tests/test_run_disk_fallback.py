"""Tests for run_disk_fallback — pure I/O read of the durable summary.json.

This module does NOT parse JSON or validate terminal state — it only reads
raw text off disk. Schema tolerance and terminal-state gating are exercised
in testing.py's own tests (D3), which reuse _decode_snapshot /
_terminal_snapshot_error against this text.
"""

from pathlib import Path

from unity_mcp.tools.run_disk_fallback import read_terminal_summary, summary_path


def _run_dir(project_path: Path, run_id: str) -> Path:
    return project_path / "Library" / "UnityMCP" / "TestRuns" / "runs" / run_id


def test_summary_path_matches_durable_writer_layout(tmp_path: Path):
    """summary_path must match TestRunStore.GetSummaryPath's on-disk layout exactly."""
    result = summary_path(tmp_path, "run-1")

    assert result == tmp_path / "Library" / "UnityMCP" / "TestRuns" / "runs" / "run-1" / "summary.json"


def test_returns_file_contents_when_summary_exists(tmp_path: Path):
    """Existing summary.json text is returned verbatim, unparsed."""
    run_dir = _run_dir(tmp_path, "run-1")
    run_dir.mkdir(parents=True)
    contents = '{"run_id": "run-1", "outcome": "passed"}'
    (run_dir / "summary.json").write_text(contents, encoding="utf-8")

    result = read_terminal_summary(tmp_path, "run-1")

    assert result == contents


def test_returns_none_when_run_directory_missing(tmp_path: Path):
    """No run directory at all (never dispatched, or wrong run_id) yields None."""
    result = read_terminal_summary(tmp_path, "never-existed")

    assert result is None


def test_returns_none_when_summary_file_is_empty(tmp_path: Path):
    """Zero-byte file (half-written/crashed write) yields None, not ''."""
    run_dir = _run_dir(tmp_path, "run-2")
    run_dir.mkdir(parents=True)
    (run_dir / "summary.json").write_text("", encoding="utf-8")

    result = read_terminal_summary(tmp_path, "run-2")

    assert result is None
