"""T2 (P2a): Dependabot PRs receive no Actions secrets, so
`buildalon/activate-unity-license` fails with "Username is required for Unity
License Activation!" and the job goes red instead of skipping cleanly (the
Dependabot secrets store is empty by GitHub design -- this is not a
misconfiguration to "fix" by adding secrets).

Guard: each job that activates a Unity license declares a job-level
`HAS_UNITY_SECRETS` env (computed from `secrets`, since a job's own top-level
`if:` cannot see `secrets`/`env` -- the guard has to live on each step's own
`if:` instead), gates every secret-dependent step on it, and posts a
`::notice` when secrets are unavailable so the run explains itself instead of
going silently green having done nothing.

Tier A (generic, discovered): every job in every tracked workflow that uses
`buildalon/activate-unity-license` must satisfy the invariant. T2 fixes 3 of
the 6 such jobs; the other 3 (T3 scope: ci-csharp-inspect.yml's `inspect`,
ci-sonar.yml's `csharp-inspect`, unity-compat.yml's `compat`) are still red
and marked xfail(strict=True) until T3 lands -- T3's whole job against this
file is extending T2_FIXED_JOBS, no other edit needed here.

Tier B (T2-specific, hardcoded): `buildalon/unity-setup` and the actual
Unity-run step (EditMode/PlayMode/hosted-conformance) don't reference
`secrets.UNITY_*` directly -- they only need an activated license
transitively -- so Tier A's generic scan can't see them. Also guards against
silently dropping A14's pre-existing PR/Linux-only gate (see
test_ci_yaml_pr_gating.py) while adding the secrets gate alongside it.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
from pathlib import Path

import pytest
import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"

GATE_CLAUSE = "env.HAS_UNITY_SECRETS == 'true'"
UNGATE_CLAUSE = "env.HAS_UNITY_SECRETS != 'true'"
LICENSE_ACTION_PREFIX = "buildalon/activate-unity-license"
PR_LINUX_ONLY_CLAUSE = "github.event_name != 'pull_request' || matrix.name == 'Linux'"

# T2 fixes these three jobs now. T3 (Plans/ci-hygiene/PLAN.md step 3) extends
# this set with the remaining 3 jobs it guards -- that's the entire required
# change to this file for T3; the xfail marks below disappear on their own
# once a pair is a member.
T2_FIXED_JOBS = {
    ("ci-conformance.yml", "hosted-disposable-unity"),
    ("unity-tests.yml", "test"),
    ("unity-tests.yml", "playmode-test"),
}

# T3 (Plans/ci-hygiene/PLAN.md step 3) explicitly scopes to these 3 jobs.
T3_SCOPE_JOBS = {
    ("ci-csharp-inspect.yml", "inspect"),
    ("ci-sonar.yml", "csharp-inspect"),
    ("unity-compat.yml", "compat"),
}


def _load_workflow(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def _triggers_on_pull_request(data: dict) -> bool:
    # PyYAML's YAML-1.1 boolean resolver parses the bare `on:` key as `True`,
    # not the string "on" -- fall back to the string key in case it is ever
    # quoted in a workflow file.
    triggers = data[True] if True in data else data.get("on")
    if isinstance(triggers, dict):
        return "pull_request" in triggers
    if isinstance(triggers, list):
        return "pull_request" in triggers
    return triggers == "pull_request"


def _discover_license_jobs() -> list[tuple[str, str, dict]]:
    """Every (workflow filename, job name, job dict) using activate-unity-license,
    scoped to workflows that actually run on `pull_request` -- schedule/
    workflow_dispatch/workflow_call-only workflows (nightly.yml,
    mutation-regression.yml, fsr-qualification.yml [explicitly frozen/legacy],
    unity-player-playtest.yml) never see a Dependabot PR event, so P2 (this
    guard) doesn't apply to them.
    """
    found = []
    for path in sorted(WORKFLOWS_DIR.glob("*.yml")):
        data = _load_workflow(path)
        if not _triggers_on_pull_request(data):
            continue
        for job_name, job in (data.get("jobs") or {}).items():
            steps = job.get("steps") or []
            if any(str(s.get("uses", "")).startswith(LICENSE_ACTION_PREFIX) for s in steps):
                found.append((path.name, job_name, job))
    return found


def _refs_unity_secret(value) -> bool:
    if isinstance(value, str):
        return "secrets.UNITY_" in value
    if isinstance(value, dict):
        return any(_refs_unity_secret(v) for v in value.values())
    return False


def _is_secret_dependent_step(step: dict) -> bool:
    if str(step.get("uses", "")).startswith(LICENSE_ACTION_PREFIX):
        return True
    return _refs_unity_secret(step.get("with")) or _refs_unity_secret(step.get("env"))


def _tier_a_params():
    params = []
    for filename, job_name, job in _discover_license_jobs():
        pair = (filename, job_name)
        marks = ()
        if pair in T3_SCOPE_JOBS:
            marks = pytest.mark.xfail(
                strict=True,
                reason="T3: Unity-secrets guard not yet applied (Plans/ci-hygiene/PLAN.md T3)",
            )
        elif pair not in T2_FIXED_JOBS:
            # Discovered by the generic pull_request scan but not named by
            # either T2 or T3 in Plans/ci-hygiene/PLAN.md (e.g.
            # unity-player-playtest.yml's player-playtest job, which the
            # architect synthesis missed) -- a real plan gap, not T3's job.
            # xfail(strict) so it fails loudly (not silently green) both if
            # left broken and if fixed without updating this set.
            marks = pytest.mark.xfail(
                strict=True,
                reason=(
                    "plan gap: unguarded activate-unity-license on a "
                    "pull_request-triggered job outside T2/T3 scope in "
                    "Plans/ci-hygiene/PLAN.md -- needs a follow-up task"
                ),
            )
        params.append(
            pytest.param(filename, job_name, job, marks=marks, id=f"{filename}::{job_name}")
        )
    return params


_LICENSE_JOB_PARAMS = _tier_a_params()


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_job_declares_has_unity_secrets_env(filename, job_name, job):
    env = job.get("env") or {}
    expr = str(env.get("HAS_UNITY_SECRETS", ""))
    assert "secrets.UNITY_EMAIL != ''" in expr, f"{filename}::{job_name}: env expr {expr!r}"
    assert "secrets.UNITY_PASSWORD != ''" in expr, f"{filename}::{job_name}: env expr {expr!r}"


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_secret_dependent_steps_are_gated(filename, job_name, job):
    steps = job.get("steps") or []
    offenders = [
        step.get("name") or step.get("uses")
        for step in steps
        if _is_secret_dependent_step(step) and GATE_CLAUSE not in str(step.get("if", ""))
    ]
    assert not offenders, f"{filename}::{job_name}: ungated secret-dependent steps: {offenders}"


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_job_has_secrets_unavailable_notice_step(filename, job_name, job):
    steps = job.get("steps") or []
    notices = [
        step
        for step in steps
        if UNGATE_CLAUSE in str(step.get("if", "")) and "::notice" in str(step.get("run", ""))
    ]
    assert notices, f"{filename}::{job_name}: no step gated on {UNGATE_CLAUSE!r} emits ::notice"


# Tier B: (filename, job_name) -> (step identifiers, whether A14's pre-existing
# PR/Linux-only clause must still be present verbatim in the same `if:`).
# unity-tests.yml's playmode-test has no strategy.matrix and never had that
# clause, so it has nothing to preserve there.
LICENSE_GATED_RUN_STEPS = {
    ("ci-conformance.yml", "hosted-disposable-unity"): (
        (
            "buildalon/unity-setup",
            "buildalon/activate-unity-license",
            "Run hosted disposable Unity conformance",
        ),
        True,
    ),
    ("unity-tests.yml", "test"): (
        ("buildalon/unity-setup", "buildalon/activate-unity-license", "Run EditMode Tests"),
        True,
    ),
    ("unity-tests.yml", "playmode-test"): (
        ("buildalon/unity-setup", "buildalon/activate-unity-license", "Run PlayMode Tests"),
        False,
    ),
}


def _find_step(steps: list[dict], identifier: str) -> dict | None:
    for step in steps:
        if step.get("name") == identifier or str(step.get("uses", "")).startswith(identifier):
            return step
    return None


@pytest.mark.parametrize("filename,job_name", sorted(LICENSE_GATED_RUN_STEPS))
def test_t2_unity_run_steps_gated_by_has_unity_secrets(filename, job_name):
    identifiers, expect_pr_linux_clause = LICENSE_GATED_RUN_STEPS[(filename, job_name)]
    steps = _load_workflow(WORKFLOWS_DIR / filename)["jobs"][job_name]["steps"]
    for identifier in identifiers:
        step = _find_step(steps, identifier)
        assert step is not None, f"{filename}::{job_name}: step {identifier!r} not found"
        condition = str(step.get("if", ""))
        assert GATE_CLAUSE in condition, (
            f"{filename}::{job_name}: {identifier!r} if: {condition!r} missing {GATE_CLAUSE!r}"
        )
        if expect_pr_linux_clause:
            assert PR_LINUX_ONLY_CLAUSE in condition, (
                f"{filename}::{job_name}: {identifier!r} lost the A14 PR/Linux gate: {condition!r}"
            )
