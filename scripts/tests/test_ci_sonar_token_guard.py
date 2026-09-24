"""SonarCloud Scan needs `SONAR_TOKEN`, but Dependabot PRs (and any PR run
that receives no Actions secrets by GitHub design) never get it, so an
unguarded step fails on an empty token instead of skipping cleanly.

Guard: each job that passes `secrets.SONAR_TOKEN` to a step declares a
job-level `HAS_SONAR_TOKEN` env (a job's own top-level `if:` can't see
`secrets`/`env`, so the guard has to live on each step's own `if:` instead),
and posts a `::notice` when the token is unavailable so the run explains
itself instead of going silently green having done nothing. A step that
already tolerates failure via `continue-on-error: true` (e.g. the Codecov
uploads elsewhere in this repo) is exempt -- it never turns the job red
either way, so it needs no separate guard.

Jobs are discovered generically across every PR-triggered workflow, so a new
SONAR_TOKEN-consuming step added later fails this test by default (via
`test_discovered_sonar_token_jobs_matches_known_set`) instead of silently
slipping through unguarded.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
from pathlib import Path

import pytest
import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"

GATE_CLAUSE = "env.HAS_SONAR_TOKEN == 'true'"
UNGATE_CLAUSE = "env.HAS_SONAR_TOKEN != 'true'"


def _load_workflow(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def _triggers_on_pull_request(data: dict) -> bool:
    # PyYAML's YAML-1.1 boolean resolver parses the bare `on:` key as `True`,
    # not the string "on" -- fall back to the string key in case it is ever
    # quoted in a workflow file.
    triggers = data[True] if True in data else data.get("on")
    if isinstance(triggers, (dict, list)):
        return "pull_request" in triggers
    return triggers == "pull_request"


def _refs_sonar_token(value) -> bool:
    if isinstance(value, str):
        return "secrets.SONAR_TOKEN" in value
    if isinstance(value, dict):
        return any(_refs_sonar_token(v) for v in value.values())
    return False


def _is_sonar_token_step(step: dict) -> bool:
    if step.get("continue-on-error"):
        return False
    return _refs_sonar_token(step.get("with")) or _refs_sonar_token(step.get("env"))


def _discover_sonar_token_jobs() -> list[tuple[str, str, dict]]:
    found = []
    for path in sorted(WORKFLOWS_DIR.glob("*.yml")):
        data = _load_workflow(path)
        if not _triggers_on_pull_request(data):
            continue
        for job_name, job in (data.get("jobs") or {}).items():
            steps = job.get("steps") or []
            if any(_is_sonar_token_step(s) for s in steps):
                found.append((path.name, job_name, job))
    return found


KNOWN_SONAR_TOKEN_JOBS = {
    ("ci-sonar.yml", "sonar-scan"),
}

_SONAR_TOKEN_JOB_PARAMS = [
    pytest.param(filename, job_name, job, id=f"{filename}::{job_name}")
    for filename, job_name, job in _discover_sonar_token_jobs()
]


def test_discovered_sonar_token_jobs_matches_known_set():
    # A brand-new job that starts passing secrets.SONAR_TOKEN to a step (or
    # one that stops) must be named here explicitly -- this fails loudly and
    # by name instead of the discovery-driven parametrized tests below
    # silently growing/shrinking their param list.
    discovered = {(filename, job_name) for filename, job_name, _ in _discover_sonar_token_jobs()}
    assert discovered == KNOWN_SONAR_TOKEN_JOBS, (
        f"new: {discovered - KNOWN_SONAR_TOKEN_JOBS} removed: {KNOWN_SONAR_TOKEN_JOBS - discovered}"
    )


@pytest.mark.parametrize("filename,job_name,job", _SONAR_TOKEN_JOB_PARAMS)
def test_job_declares_has_sonar_token_env(filename, job_name, job):
    env = job.get("env") or {}
    expr = str(env.get("HAS_SONAR_TOKEN", ""))
    assert "secrets.SONAR_TOKEN != ''" in expr, f"{filename}::{job_name}: env expr {expr!r}"


@pytest.mark.parametrize("filename,job_name,job", _SONAR_TOKEN_JOB_PARAMS)
def test_sonar_token_steps_are_gated(filename, job_name, job):
    steps = job.get("steps") or []
    offenders = [
        step.get("name") or step.get("uses")
        for step in steps
        if _is_sonar_token_step(step) and GATE_CLAUSE not in str(step.get("if", ""))
    ]
    assert not offenders, f"{filename}::{job_name}: ungated SONAR_TOKEN steps: {offenders}"


@pytest.mark.parametrize("filename,job_name,job", _SONAR_TOKEN_JOB_PARAMS)
def test_job_has_sonar_token_unavailable_notice_step(filename, job_name, job):
    steps = job.get("steps") or []
    notices = [
        step
        for step in steps
        if UNGATE_CLAUSE in str(step.get("if", "")) and "::notice" in str(step.get("run", ""))
    ]
    assert notices, f"{filename}::{job_name}: no step gated on {UNGATE_CLAUSE!r} emits ::notice"
