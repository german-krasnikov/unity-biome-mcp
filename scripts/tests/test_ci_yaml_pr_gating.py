"""A14: PR runs must gate the Unity macOS/Windows matrix legs' expensive
steps off, leaving only Linux to do real work on `pull_request` events
(macOS/Windows still run in full on push/workflow_dispatch/schedule, where
the gating `if:` evaluates true).

Deviation from the plan's literal "job-level if" wording: GitHub Actions
disallows the `matrix` context in a job's own top-level `if:` (confirmed via
actionlint: "context 'matrix' is not allowed here. available contexts are
'github', 'inputs', 'needs', 'vars'" — matrix IS allowed in step-level
`if:`). So the gate is applied per-step, to the steps that actually cost
CI minutes/license seats: the VC++ runtime install, Unity Editor setup,
license activation, and the actual test/conformance run.

Two workflow-wide security invariants live here rather than in
test_ci_unity_secrets_guard.py, since they are general PR-gating hygiene, not
specific to the Unity-secrets guard:
- No workflow anywhere uses `pull_request_target` (that trigger runs with the
  base branch's secrets against untrusted PR head content -- a standing
  supply-chain risk this repo has simply never opted into).
- The workflow files that gained a Unity-secrets guard (ci-csharp-inspect.yml,
  ci-sonar.yml, unity-compat.yml, unity-player-playtest.yml) must not gain a
  wider top-level `permissions` scope than they already had. This is a
  snapshot of what was already there and already justified (`checks: write`
  for dorny/test-reporter), not a retroactive policy -- it exists to stop
  this change, or a future one, from silently widening these jobs'
  permissions while adding a secrets guard.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
import itertools
import re
import sys
from pathlib import Path

import pytest
import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
UNITY_TESTS_WORKFLOW = WORKFLOWS_DIR / "unity-tests.yml"
CI_CONFORMANCE_WORKFLOW = WORKFLOWS_DIR / "ci-conformance.yml"

SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))
import gen_lane_args  # noqa: E402

_GENERATED_LANE_RE = re.compile(r"gen_lane_args\.py pytest ([\w-]+)")
REQUIRED_CONFORMANCE_EXCLUDES = ("not live", "not monkey")

PR_LINUX_ONLY_CLAUSE = "github.event_name != 'pull_request' || matrix.name == 'Linux'"

# Step names/uses-prefixes expensive enough (runner minutes or a Unity
# license seat) to skip on non-Linux PR legs.
GATED_STEP_NAMES = frozenset(
    {
        "Install VC++ 2010 runtime (Unity 6 dependency)",
        "Run EditMode Tests",
        "Run hosted disposable Unity conformance",
    }
)
GATED_STEP_USES_PREFIXES = ("buildalon/unity-setup", "buildalon/activate-unity-license")


def _job_steps(workflow_path: Path, job_name: str) -> list[dict]:
    data = yaml.safe_load(workflow_path.read_text(encoding="utf-8"))
    return data["jobs"][job_name]["steps"]


def _gated_steps(steps: list[dict]) -> list[dict]:
    matches = []
    for step in steps:
        uses = step.get("uses", "")
        name = step.get("name", "")
        if name in GATED_STEP_NAMES or uses.startswith(GATED_STEP_USES_PREFIXES):
            matches.append(step)
    return matches


def _assert_all_gated(steps: list[dict], *, expected_count: int, context: str) -> None:
    gated = _gated_steps(steps)
    assert len(gated) == expected_count, (
        f"{context}: expected {expected_count} expensive steps, found "
        f"{len(gated)}: {[s.get('name') or s.get('uses') for s in gated]}"
    )
    for step in gated:
        condition = step.get("if", "")
        label = step.get("name") or step.get("uses")
        assert PR_LINUX_ONLY_CLAUSE in condition, (
            f"{context}: step {label!r} missing '{PR_LINUX_ONLY_CLAUSE}' in "
            f"its if:, got: {condition!r}"
        )


def test_unity_tests_test_job_gates_expensive_steps_off_non_linux_pr():
    steps = _job_steps(UNITY_TESTS_WORKFLOW, "test")
    _assert_all_gated(steps, expected_count=4, context="unity-tests.yml jobs.test")


def test_ci_conformance_hosted_disposable_unity_gates_expensive_steps_off_non_linux_pr():
    steps = _job_steps(CI_CONFORMANCE_WORKFLOW, "hosted-disposable-unity")
    _assert_all_gated(
        steps, expected_count=4, context="ci-conformance.yml jobs.hosted-disposable-unity"
    )


def test_ci_conformance_unit_gate_step_uses_generated_lane():
    # A14 (this file's own job-gating concern) must not touch ci-conformance.yml's
    # unit-gate pytest step. PR-07 legitimately owns this step's `-m` value: it
    # replaced the hand-typed literal "not live and not monkey" with a generated
    # $(gen_lane_args.py pytest master-conformance) expression, matching
    # ci-python.yml/ci-sonar.yml/nightly.yml's own generated pattern (C16/C17) --
    # closing the drift test_gen_lane_args.py used to call "the deferred
    # ci-conformance.yml".
    data = yaml.safe_load(CI_CONFORMANCE_WORKFLOW.read_text(encoding="utf-8"))
    steps = data["jobs"]["unit-gate"]["steps"]
    run_steps = [s["run"] for s in steps if "run" in s]
    assert any("pytest tests/" in run for run in run_steps)
    for run in run_steps:
        if "pytest tests/" not in run:
            continue
        lane_match = _GENERATED_LANE_RE.search(run)
        assert lane_match, f"expected a gen_lane_args.py pytest <lane> reference in: {run!r}"
        assert lane_match.group(1) == "master-conformance"
        generated = gen_lane_args.render_lane(
            "pytest", "master-conformance", gen_lane_args.load_lanes(), gen_lane_args.load_dimensions()
        )
        for required in REQUIRED_CONFORMANCE_EXCLUDES:
            assert required in generated, f"missing '{required}' in generated expression: {generated}"


def _load_workflow(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def _workflow_triggers(data: dict):
    # PyYAML's YAML-1.1 boolean resolver parses the bare `on:` key as `True`,
    # not the string "on" -- fall back to the string key in case it is ever
    # quoted in a workflow file.
    return data[True] if True in data else data.get("on")


def test_no_workflow_uses_pull_request_target():
    # pull_request_target runs with the base branch's workflow file and full
    # secrets access, but can be pointed at untrusted PR head content by the
    # steps a workflow author writes -- a well-known supply-chain footgun.
    # This repo has never needed it; keep it that way explicitly.
    offenders = []
    for path in sorted(WORKFLOWS_DIR.glob("*.yml")):
        triggers = _workflow_triggers(_load_workflow(path))
        names = triggers if isinstance(triggers, (dict, list)) else [triggers]
        if "pull_request_target" in names:
            offenders.append(path.name)
    assert not offenders, f"pull_request_target trigger found in: {offenders}"


# Workflows whose jobs gained a Unity-secrets guard must not gain a wider
# top-level `permissions` scope in the process. `checks: write` is a
# pre-existing, justified exception (dorny/test-reporter needs it to publish
# a check run) in unity-compat.yml and unity-player-playtest.yml -- this is a
# snapshot of that fact, not a rule that write scopes are always fine.
UNITY_LICENSE_GUARD_WORKFLOWS = (
    "ci-csharp-inspect.yml",
    "ci-sonar.yml",
    "unity-compat.yml",
    "unity-player-playtest.yml",
)
PRE_EXISTING_WRITE_SCOPES = {
    ("unity-compat.yml", "checks"): "write",
    ("unity-player-playtest.yml", "checks"): "write",
}


@pytest.mark.parametrize("filename", UNITY_LICENSE_GUARD_WORKFLOWS)
def test_unity_license_workflow_permissions_not_widened(filename):
    data = _load_workflow(WORKFLOWS_DIR / filename)
    permissions = data.get("permissions") or {}
    assert isinstance(permissions, dict), f"{filename}: permissions must be a mapping, got {permissions!r}"
    for scope, level in permissions.items():
        if level in ("read", "none"):
            continue
        allowed = PRE_EXISTING_WRITE_SCOPES.get((filename, scope))
        assert level == allowed, (
            f"{filename}: permissions.{scope}={level!r} grants more than read and is not "
            "a pre-existing allowlisted exception"
        )


# A GitHub required status check only ever reports on a PR if the
# workflow that defines it actually runs for that PR. A `pull_request.paths`
# (or `paths-ignore`) filter makes the whole workflow -- and every required
# check job inside it -- skip entirely for PRs that never touch a listed
# path, which leaves the check permanently "pending" and blocks merge
# forever once a ruleset requires it. Single source of truth for the check
# names a future ruleset will require: keep this in sync with any rename of
# the `name:` (or matrix-templated `name:`) fields below.
REQUIRED_CHECK_CONTEXTS = (
    "Lint",
    "Test (py3.14, ubuntu-latest)",
    "README check",
)


def _rendered_job_names(job: dict) -> set[str]:
    """A job's actual GitHub check-run name(s): the literal `name:` string,
    or -- for a matrix job whose `name:` template references `matrix.*` --
    one rendered name per matrix leg actually scheduled."""
    name_template = job.get("name")
    if not name_template:
        return set()
    matrix = ((job.get("strategy") or {}).get("matrix")) or {}
    list_dims = {k: v for k, v in matrix.items() if isinstance(v, list)}
    if not list_dims or "${{ matrix." not in name_template:
        return {name_template}
    keys = list(list_dims.keys())
    rendered = set()
    for combo in itertools.product(*(list_dims[k] for k in keys)):
        text = name_template
        for key, value in zip(keys, combo, strict=True):
            text = text.replace(f"${{{{ matrix.{key} }}}}", str(value))
        rendered.add(text)
    return rendered


def test_required_check_workflows_have_no_pull_request_paths_filter():
    matched_contexts = set()
    offenders = []
    for path in sorted(WORKFLOWS_DIR.glob("*.yml")):
        data = _load_workflow(path)
        job_names = set()
        for job in (data.get("jobs") or {}).values():
            job_names |= _rendered_job_names(job)
        hits = job_names & set(REQUIRED_CHECK_CONTEXTS)
        if not hits:
            continue
        matched_contexts |= hits
        triggers = _workflow_triggers(data) or {}
        pr_trigger = triggers.get("pull_request") if isinstance(triggers, dict) else None
        if pr_trigger is None:
            offenders.append((path.name, "no pull_request trigger at all", hits))
            continue
        if isinstance(pr_trigger, dict) and ("paths" in pr_trigger or "paths-ignore" in pr_trigger):
            offenders.append((path.name, "pull_request has a paths filter", hits))

    assert matched_contexts == set(REQUIRED_CHECK_CONTEXTS), (
        "expected to find every required-check job name in some "
        f".github/workflows/*.yml: expected {sorted(REQUIRED_CHECK_CONTEXTS)}, "
        f"found {sorted(matched_contexts)}"
    )
    assert not offenders, f"required-check workflows must gate on every PR: {offenders}"


# pip install ".[dev]" (the Lint/Test steps below) resolves straight from
# server/pyproject.toml and never touches server/uv.lock, so a Dependabot bump
# of a pyproject.toml specifier (PR #92, grimp<3.16 -> <3.17) can land without
# the lock file being regenerated -- nothing in CI previously ran `uv lock
# --check` to catch that drift. The Lint job has no `pull_request.paths`
# filter (see REQUIRED_CHECK_CONTEXTS above), so it always runs on every PR.
def test_ci_python_lint_job_checks_uv_lock_is_in_sync():
    steps = _job_steps(WORKFLOWS_DIR / "ci-python.yml", "lint")
    matches = [s for s in steps if "uv lock --check" in s.get("run", "")]
    assert matches, (
        "ci-python.yml jobs.lint: expected a step running `uv lock --check` "
        "so a pyproject.toml/uv.lock drift fails the PR-required Lint check"
    )
    for step in matches:
        target = step.get("working-directory", "") + step["run"]
        assert "server" in target, (
            f"uv lock --check step must target the server/ project, got: {step!r}"
        )


# Dependabot's `pip` ecosystem only reads server/pyproject.toml and bumps
# specifiers there -- it has no notion of server/uv.lock, which is why PR #92
# could bump a specifier without regenerating the lock. The `uv` ecosystem
# understands uv.lock and updates both files together in the same PR.
def test_dependabot_server_python_ecosystem_is_uv():
    data = yaml.safe_load((REPO_ROOT / ".github" / "dependabot.yml").read_text(encoding="utf-8"))
    server_updates = [u for u in data["updates"] if u.get("directory") == "/server"]
    assert server_updates, "expected a dependabot.yml update entry for directory '/server'"
    for update in server_updates:
        assert update.get("package-ecosystem") == "uv", (
            "server/ Python deps must use the 'uv' ecosystem so Dependabot keeps "
            f"pyproject.toml and uv.lock in sync together, got: {update.get('package-ecosystem')!r}"
        )
