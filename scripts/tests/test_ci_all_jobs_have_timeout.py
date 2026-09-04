"""A15: every job in every tracked workflow must carry an explicit
`timeout-minutes`, so a hung runner fails fast instead of burning the
default 6-hour GitHub Actions ceiling.

Exception: a job that calls a reusable workflow via `uses:` cannot carry
`timeout-minutes` at all — GitHub Actions rejects it (confirmed via
actionlint: "when a reusable workflow is called with 'uses',
'timeout-minutes' is not available"). Its effective timeout comes from the
called workflow's own job-level `timeout-minutes` instead, so such jobs are
excluded from this sweep rather than left unguarded.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"


def _workflow_jobs(workflow_path: Path) -> dict:
    data = yaml.safe_load(workflow_path.read_text(encoding="utf-8"))
    return data.get("jobs", {}) or {}


def _jobs_missing_timeout(jobs: dict) -> list[str]:
    return [
        name
        for name, job in jobs.items()
        if "uses" not in job and "timeout-minutes" not in job
    ]


def test_every_job_in_every_workflow_has_timeout_minutes():
    workflow_paths = sorted(WORKFLOWS_DIR.glob("*.yml"))
    assert workflow_paths, f"expected workflow files under {WORKFLOWS_DIR}"

    offenders = {}
    for path in workflow_paths:
        missing = _jobs_missing_timeout(_workflow_jobs(path))
        if missing:
            offenders[path.name] = missing

    assert not offenders, f"jobs missing timeout-minutes: {offenders}"


def test_reusable_workflow_caller_job_is_excluded_not_unguarded():
    # unity-tests.yml's player-playtest job calls unity-player-playtest.yml
    # via `uses:` and cannot carry timeout-minutes itself (GH Actions
    # rejects it) — its own job must exist and carry the exclusion-eligible
    # `uses` key, and the CALLED workflow must guard its own job instead.
    jobs = _workflow_jobs(WORKFLOWS_DIR / "unity-tests.yml")
    assert "uses" in jobs["player-playtest"]
    assert "timeout-minutes" not in jobs["player-playtest"]

    called_jobs = _workflow_jobs(WORKFLOWS_DIR / "unity-player-playtest.yml")
    assert all("timeout-minutes" in job for job in called_jobs.values())
