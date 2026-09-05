"""E06a: unity-tests.yml gains a filtered PlayMode job so the 3 Play-bound
`.playtest` fixtures (B22's PlaytestCorpusPlayModeTests) finally run on PR —
today unity-tests.yml runs EditMode only.

Deviation from the plan's literal "mirroring A14's `matrix.name == 'Linux'`
gating" wording, verified with actionlint rather than assumed: this job has
no `strategy.matrix` (it always runs on a single ubuntu-latest runner — there
is no reason to ever attempt PlayMode work on macOS/Windows for a narrowly
filtered corpus, per this item's own plan text). actionlint rejects any
reference to the `matrix` context on a job without a matrix strategy
("context 'matrix' is not allowed here") — copying A14's literal clause here
would be an actionlint failure AND a correctness bug (with `matrix.name`
always undefined, `github.event_name != 'pull_request' || matrix.name ==
'Linux'` collapses to `github.event_name != 'pull_request'`, which would
skip the job on every PR — exactly backwards from this item's purpose). The
job instead mirrors the existing `test` job's own gate (skip only the
special workflow_dispatch player-playtest trigger mode), which the existing
`test` job already carries at push/pull_request/schedule.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow file only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unity-tests.yml"
JOB_NAME = "playmode-test"


def _job() -> dict:
    data = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))
    return data["jobs"][JOB_NAME]


def _run_args(job: dict) -> str:
    for step in job["steps"]:
        with_block = step.get("with", {})
        if "args" in with_block:
            return with_block["args"]
    return ""


def test_playmode_job_exists_with_explicit_timeout():
    job = _job()
    assert isinstance(job.get("timeout-minutes"), int) and job["timeout-minutes"] > 0


def test_playmode_job_has_no_matrix_context_reference():
    """The concrete reason this job cannot use A14's literal clause: it has
    no strategy.matrix at all (single ubuntu-latest runner, by design)."""
    job = _job()
    assert "strategy" not in job
    assert "matrix" not in str(job.get("if", ""))


def test_playmode_job_mirrors_test_jobs_dispatch_gate():
    data = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))
    test_job_if = data["jobs"]["test"]["if"]
    playmode_job_if = data["jobs"][JOB_NAME]["if"]
    assert playmode_job_if == test_job_if


def test_playmode_job_runs_filtered_playmode_corpus():
    job = _job()
    args = _run_args(job)
    assert "-testPlatform PlayMode" in args
    assert "-testFilter PlaytestCorpusPlayModeTests" in args


def test_playmode_job_runs_on_ubuntu_only():
    job = _job()
    assert job["runs-on"] == "ubuntu-latest"
