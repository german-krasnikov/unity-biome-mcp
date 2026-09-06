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

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
import re
import sys
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
UNITY_TESTS_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unity-tests.yml"
CI_CONFORMANCE_WORKFLOW = REPO_ROOT / ".github" / "workflows" / "ci-conformance.yml"

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
