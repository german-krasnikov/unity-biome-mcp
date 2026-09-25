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


def _normalize_ws(text: str) -> str:
    return " ".join(text.split())


def _strip_wrapping_parens(clause: str) -> str:
    """Strip one layer of `(...)` if it wraps the whole clause (balanced)."""
    clause = clause.strip()
    if not (clause.startswith("(") and clause.endswith(")")):
        return clause
    depth = 0
    for i, ch in enumerate(clause):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0 and i != len(clause) - 1:
                return clause  # closes before the end -- not a single wrapping pair
    return clause[1:-1].strip()


def _and_clauses(condition: str) -> list[str]:
    """Split a GHA `if:` expression on its top-level `&&` connectives.

    Each returned clause has whitespace normalized and one layer of wrapping
    parens stripped, so `(A || B) && C` yields `["A || B", "C"]` regardless
    of formatting. A mutation that turns the connective joining two clauses
    into `||` merges them into a single clause here instead of splitting
    them, which callers assert against directly.
    """
    return [_strip_wrapping_parens(_normalize_ws(part)) for part in condition.split("&&")]


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
    expr = _normalize_ws(str(env.get("HAS_UNITY_SECRETS", "")))
    # Asserted as one joined substring, not two independent membership checks:
    # a `&&` -> `||` mutation here would make HAS_UNITY_SECRETS true when only
    # one of the two secrets is set, and two separate substring checks can't
    # tell the difference since both secret references stay present either way.
    assert "secrets.UNITY_EMAIL != '' && secrets.UNITY_PASSWORD != ''" in expr, (
        f"{filename}::{job_name}: env expr not joined by &&: {expr!r}"
    )


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_secret_dependent_steps_are_gated(filename, job_name, job):
    steps = job.get("steps") or []
    offenders = [
        step.get("name") or step.get("uses")
        for step in steps
        if _is_secret_dependent_step(step) and GATE_CLAUSE not in str(step.get("if", ""))
    ]
    assert not offenders, f"{filename}::{job_name}: ungated secret-dependent steps: {offenders}"


def _notice_steps(steps: list[dict]) -> list[dict]:
    return [
        step
        for step in steps
        if UNGATE_CLAUSE in str(step.get("if", "")) and "::notice" in str(step.get("run", ""))
    ]


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_job_has_secrets_unavailable_notice_step(filename, job_name, job):
    steps = job.get("steps") or []
    notices = _notice_steps(steps)
    assert notices, f"{filename}::{job_name}: no step gated on {UNGATE_CLAUSE!r} emits ::notice"


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_notice_step_declares_shell_bash(filename, job_name, job):
    # None of these workflows sets defaults.run.shell, so a run: step on a
    # windows-2022 leg without an explicit shell: silently gets pwsh instead
    # of bash. Regression coverage for that -- dropping shell: bash here would
    # only surface on a Windows Dependabot PR, the one scenario this step
    # exists for.
    steps = job.get("steps") or []
    for step in _notice_steps(steps):
        assert step.get("shell") == "bash", (
            f"{filename}::{job_name}: notice step {step.get('name')!r} missing shell: bash"
        )


@pytest.mark.parametrize("filename,job_name,job", _LICENSE_JOB_PARAMS)
def test_notice_step_errors_on_non_pull_request_event(filename, job_name, job):
    # A PR from a fork/Dependabot legitimately has no secrets -- that stays a
    # soft ::notice. Any other event (push/schedule/workflow_dispatch) is
    # same-repo and is expected to have secrets, so missing ones there mean a
    # lost/misconfigured repository secret; that must fail the run (::error +
    # exit 1) instead of silently skipping to a green job.
    steps = job.get("steps") or []
    notices = _notice_steps(steps)
    assert notices, f"{filename}::{job_name}: no notice step found"
    run = str(notices[0].get("run", ""))
    assert 'if [ "$GITHUB_EVENT_NAME" = "pull_request" ]' in run, (
        f"{filename}::{job_name}: notice step does not branch on $GITHUB_EVENT_NAME: {run!r}"
    )
    pr_branch, _, other_branch = run.partition("else")
    assert other_branch, f"{filename}::{job_name}: notice step has no else branch: {run!r}"
    assert "::notice" in pr_branch and "::error" not in pr_branch, (
        f"{filename}::{job_name}: pull_request branch must emit ::notice only: {pr_branch!r}"
    )
    assert "::error" in other_branch and "exit 1" in other_branch, (
        f"{filename}::{job_name}: non-pull_request branch must emit ::error and exit 1: {other_branch!r}"
    )
    assert "::notice" not in other_branch, (
        f"{filename}::{job_name}: non-pull_request branch must not also emit ::notice: {other_branch!r}"
    )


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


def test_compat_job_summary_compile_line_gated_on_has_unity_secrets():
    """unity-compat.yml's 'Write job summary' step runs unconditionally
    (if: always()), so its compile-mode branch must not print "Compile check
    passed" when the actual Compile check step was skipped for missing
    secrets -- otherwise a Dependabot-triggered run's summary claims success
    for Unity work that never ran.
    """
    steps = _load_workflow(WORKFLOWS_DIR / "unity-compat.yml")["jobs"]["compat"]["steps"]
    step = _find_step(steps, "Write job summary")
    assert step is not None, "unity-compat.yml::compat: 'Write job summary' step not found"
    run = _normalize_ws(str(step.get("run", "")))
    assert (
        'if [ "$MODE" = "compile" ]; then if [ "$HAS_UNITY_SECRETS" = "true" ]; then '
        'echo "✅ Compile check passed" >> "$GITHUB_STEP_SUMMARY" else '
        'echo "⏭️ Compile check skipped (no Unity secrets)" >> "$GITHUB_STEP_SUMMARY" fi else'
    ) in run, f"unity-compat.yml::compat: summary compile branch not gated on HAS_UNITY_SECRETS: {run!r}"


@pytest.mark.parametrize("filename,job_name", sorted(LICENSE_GATED_RUN_STEPS))
def test_unity_run_steps_gated_by_has_unity_secrets(filename, job_name):
    step_clauses = LICENSE_GATED_RUN_STEPS[(filename, job_name)]
    steps = _load_workflow(WORKFLOWS_DIR / filename)["jobs"][job_name]["steps"]
    for identifier, preserve_clause in step_clauses.items():
        step = _find_step(steps, identifier)
        assert step is not None, f"{filename}::{job_name}: step {identifier!r} not found"
        condition = str(step.get("if", ""))
        # Split into top-level `&&` clauses rather than doing two independent
        # substring checks: a `&&` -> `||` mutation joining preserve_clause and
        # GATE_CLAUSE merges them into one clause here instead of two, so
        # neither exact-match assertion below can pass.
        clauses = _and_clauses(condition)
        assert GATE_CLAUSE in clauses, (
            f"{filename}::{job_name}: {identifier!r} if: {condition!r} not &&-joined with {GATE_CLAUSE!r}"
        )
        if preserve_clause is not None:
            assert preserve_clause in clauses, (
                f"{filename}::{job_name}: {identifier!r} lost pre-existing clause "
                f"{preserve_clause!r}, or it is no longer &&-joined: {condition!r}"
            )
        else:
            assert clauses == [GATE_CLAUSE], (
                f"{filename}::{job_name}: {identifier!r} expected if: to be exactly "
                f"{GATE_CLAUSE!r}, got {condition!r}"
            )
