"""Dependabot PRs receive no Actions secrets, so
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
`buildalon/activate-unity-license` must satisfy the invariant, including
unity-player-playtest.yml's `player-playtest` job -- easy to miss because it
triggers directly on `pull_request`, not only via `workflow_call` from
another workflow. All known jobs are guarded, so
`test_discovered_license_jobs_matches_known_set` below is the safety net: a
brand-new unguarded job fails that test directly and by name instead of
silently slipping through.

Tier B (hardcoded): `buildalon/unity-setup` and the actual Unity-dependent
step(s) (EditMode/PlayMode/hosted-conformance run, `.sln` generation,
InspectCode, compat-matrix test steps, the player build/run chain) don't
reference `secrets.UNITY_*` directly -- they only need an activated license
transitively -- so Tier A's generic scan can't see them. Each job may also
carry a pre-existing `if:` clause (A14's PR/Linux-only gate, a cache-hit
short-circuit, an `unity-setup.outcome` check) that the secrets gate must be
appended to, not replace; LICENSE_GATED_RUN_STEPS records, per step, the
exact clause (if any) that must still be present verbatim.

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

# Every (workflow, job) the discovery scan is known to find, all guarded.
# Kept as one flat set (not tiered) since no test behavior depends on
# tiering now that nothing is xfailed.
KNOWN_LICENSE_JOBS = {
    ("ci-conformance.yml", "hosted-disposable-unity"),
    ("unity-tests.yml", "test"),
    ("unity-tests.yml", "playmode-test"),
    ("ci-csharp-inspect.yml", "inspect"),
    ("ci-sonar.yml", "csharp-inspect"),
    ("unity-compat.yml", "compat"),
    ("unity-player-playtest.yml", "player-playtest"),
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
    mutation-regression.yml, fsr-qualification.yml [explicitly frozen/legacy])
    never see a Dependabot PR event, so P2 (this guard) doesn't apply to them.
    unity-player-playtest.yml DOES trigger directly on `pull_request` (in
    addition to being called via `workflow_call` from unity-tests.yml), which
    is exactly why its `player-playtest` job needed the same guard as the
    others.
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
    return [
        pytest.param(filename, job_name, job, id=f"{filename}::{job_name}")
        for filename, job_name, job in _discover_license_jobs()
    ]


_LICENSE_JOB_PARAMS = _tier_a_params()


def test_discovered_license_jobs_matches_known_set():
    # A brand-new job that starts using activate-unity-license (or one that
    # stops) must be named here explicitly -- this fails loudly and by name
    # instead of the discovery-driven parametrized tests below silently
    # growing/shrinking their param list.
    discovered = {(filename, job_name) for filename, job_name, _ in _discover_license_jobs()}
    assert discovered == KNOWN_LICENSE_JOBS, (
        f"new: {discovered - KNOWN_LICENSE_JOBS} removed: {KNOWN_LICENSE_JOBS - discovered}"
    )


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


# Tier B: (filename, job_name) -> {step identifier: pre-existing clause that
# must still be present verbatim in the same `if:`, or None if the step had
# no `if:` at all before its secrets gate was added}. Tier A's generic scan
# only catches steps that reference `secrets.UNITY_*` directly -- unity-setup
# and the actual Unity-dependent work step(s) don't, so each job's chain is
# listed here by hand.
CACHE_HIT_CLAUSE = "steps.ic-cache.outputs.cache-hit != 'true'"
UNITY_SETUP_OK_CLAUSE = "steps.unity-setup.outcome != 'failure'"

LICENSE_GATED_RUN_STEPS = {
    ("ci-conformance.yml", "hosted-disposable-unity"): {
        "buildalon/unity-setup": PR_LINUX_ONLY_CLAUSE,
        "buildalon/activate-unity-license": PR_LINUX_ONLY_CLAUSE,
        "Run hosted disposable Unity conformance": PR_LINUX_ONLY_CLAUSE,
    },
    ("unity-tests.yml", "test"): {
        "buildalon/unity-setup": PR_LINUX_ONLY_CLAUSE,
        "buildalon/activate-unity-license": PR_LINUX_ONLY_CLAUSE,
        "Run EditMode Tests": PR_LINUX_ONLY_CLAUSE,
    },
    ("unity-tests.yml", "playmode-test"): {
        # No strategy.matrix on this job, so it never had the PR/Linux clause.
        "buildalon/unity-setup": None,
        "buildalon/activate-unity-license": None,
        "Run PlayMode Tests": None,
    },
    ("ci-csharp-inspect.yml", "inspect"): {
        # No pre-existing if: on any of these before this guard was added.
        "buildalon/unity-setup": None,
        "buildalon/activate-unity-license": None,
        "Generate .sln and .csproj": None,
        "Run InspectCode": None,
    },
    ("ci-sonar.yml", "csharp-inspect"): {
        "buildalon/unity-setup": CACHE_HIT_CLAUSE,
        "buildalon/activate-unity-license": CACHE_HIT_CLAUSE,
        "Generate .sln": CACHE_HIT_CLAUSE,
        "Run InspectCode and convert to SonarQube format": CACHE_HIT_CLAUSE,
    },
    ("unity-compat.yml", "compat"): {
        # unity-setup itself never had an if: (only continue-on-error); every
        # step downstream of it already gated on its outcome.
        "buildalon/unity-setup": None,
        "buildalon/activate-unity-license": UNITY_SETUP_OK_CLAUSE,
        "Compile check": UNITY_SETUP_OK_CLAUSE,
        "Targeted compat tests": UNITY_SETUP_OK_CLAUSE,
        "Full EditMode tests": UNITY_SETUP_OK_CLAUSE,
    },
    ("unity-player-playtest.yml", "player-playtest"): {
        "buildalon/unity-setup": PR_LINUX_ONLY_CLAUSE,
        "buildalon/activate-unity-license": PR_LINUX_ONLY_CLAUSE,
        "Build Standalone Player Smoke": PR_LINUX_ONLY_CLAUSE,
        "Run Player PlayTest Smoke": PR_LINUX_ONLY_CLAUSE,
        "Validate Player PlayTest Receipts": PR_LINUX_ONLY_CLAUSE,
        "Run Player PlayTest Expected Failure Smoke": PR_LINUX_ONLY_CLAUSE,
        "Validate Player PlayTest Expected Failure Receipts": PR_LINUX_ONLY_CLAUSE,
        "Run Player PlayTest Preflight Reject Smoke": PR_LINUX_ONLY_CLAUSE,
        "Validate Player PlayTest Preflight Reject Receipts": PR_LINUX_ONLY_CLAUSE,
        "Run Player Fan-Out (.playtest @needs player)": PR_LINUX_ONLY_CLAUSE,
        "Write Player PlayTest Evidence": PR_LINUX_ONLY_CLAUSE,
    },
}


def _find_step(steps: list[dict], identifier: str) -> dict | None:
    for step in steps:
        if step.get("name") == identifier or str(step.get("uses", "")).startswith(identifier):
            return step
    return None


@pytest.mark.parametrize("filename,job_name", sorted(LICENSE_GATED_RUN_STEPS))
def test_unity_run_steps_gated_by_has_unity_secrets(filename, job_name):
    step_clauses = LICENSE_GATED_RUN_STEPS[(filename, job_name)]
    steps = _load_workflow(WORKFLOWS_DIR / filename)["jobs"][job_name]["steps"]
    for identifier, preserve_clause in step_clauses.items():
        step = _find_step(steps, identifier)
        assert step is not None, f"{filename}::{job_name}: step {identifier!r} not found"
        condition = str(step.get("if", ""))
        assert GATE_CLAUSE in condition, (
            f"{filename}::{job_name}: {identifier!r} if: {condition!r} missing {GATE_CLAUSE!r}"
        )
        if preserve_clause is not None:
            assert preserve_clause in condition, (
                f"{filename}::{job_name}: {identifier!r} lost pre-existing clause "
                f"{preserve_clause!r}: {condition!r}"
            )
