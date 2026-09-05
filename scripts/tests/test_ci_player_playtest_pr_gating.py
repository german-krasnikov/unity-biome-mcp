"""E06: `unity-player-playtest.yml` must gate every costly/work-producing step
in the `player-playtest` matrix job off non-Linux `pull_request` legs — the
same reviewed step-level clause A14 proved for unity-tests.yml/ci-conformance.yml
(job-level `if:` cannot reference `matrix`, confirmed via actionlint).

On `pull_request`, only the Linux leg installs Unity, builds, or runs the
Player; macOS/Windows legs become no-op skips for those steps. On
push/schedule/workflow_dispatch the clause evaluates true for every leg, so
all 3 OS keep doing full Player work (unchanged from before this item).

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow file only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unity-player-playtest.yml"

PR_LINUX_ONLY_CLAUSE = "github.event_name != 'pull_request' || matrix.name == 'Linux'"

# Every step that installs Unity, spends a license seat, builds/runs the
# Player, or depends on files only those steps produce.
GATED_STEP_NAMES = frozenset(
    {
        "Install VC++ 2010 runtime (Unity 6 dependency)",
        "Build Standalone Player Smoke",
        "Run Player PlayTest Smoke",
        "Validate Player PlayTest Receipts",
        "Run Player PlayTest Expected Failure Smoke",
        "Validate Player PlayTest Expected Failure Receipts",
        "Run Player Fan-Out (.playtest @needs player)",
        "Write Player PlayTest Evidence",
    }
)
GATED_STEP_USES_PREFIXES = ("buildalon/unity-setup", "buildalon/activate-unity-license")

# Steps deliberately NOT in the gated set, with the reason each is safe:
# - "Install Linux virtual display": already runner.os == 'Linux' only, cheap apt/xvfb bootstrap.
# - "Run/Validate Player Graphics Smoke": already gated on the opt-in `inputs.run_graphics_smoke`
#   input, which the `pull_request` trigger never carries — already off on every PR leg.
# - upload-artifact / "Check player JUnit exists" / dorny/test-reporter: `if: always()` or
#   self-checking, tolerant of missing files (if-no-files-found: warn / exists-check).


def _player_playtest_steps() -> list[dict]:
    data = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))
    return data["jobs"]["player-playtest"]["steps"]


def _gated_steps(steps: list[dict]) -> list[dict]:
    matches = []
    for step in steps:
        uses = step.get("uses", "")
        name = step.get("name", "")
        if name in GATED_STEP_NAMES or uses.startswith(GATED_STEP_USES_PREFIXES):
            matches.append(step)
    return matches


def test_player_playtest_job_gates_every_costly_step_off_non_linux_pr():
    steps = _player_playtest_steps()
    gated = _gated_steps(steps)
    assert len(gated) == len(GATED_STEP_NAMES) + len(GATED_STEP_USES_PREFIXES), (
        f"expected {len(GATED_STEP_NAMES) + len(GATED_STEP_USES_PREFIXES)} gated steps, "
        f"found {len(gated)}: {[s.get('name') or s.get('uses') for s in gated]}"
    )
    for step in gated:
        condition = step.get("if", "")
        label = step.get("name") or step.get("uses")
        assert PR_LINUX_ONLY_CLAUSE in condition, (
            f"step {label!r} missing '{PR_LINUX_ONLY_CLAUSE}' in its if:, got: {condition!r}"
        )


def test_windows_vcpp_step_keeps_its_own_os_condition_alongside_the_pr_gate():
    """The one gated step that already had an `if:` (runner.os == 'Windows')
    must keep BOTH conditions — losing the OS check would install vcredist on
    every OS, losing the PR gate would defeat this item's whole point."""
    steps = _player_playtest_steps()
    step = next(s for s in steps if s.get("name") == "Install VC++ 2010 runtime (Unity 6 dependency)")
    condition = step.get("if", "")
    assert "runner.os == 'Windows'" in condition
    assert PR_LINUX_ONLY_CLAUSE in condition


def test_fan_out_step_invokes_run_player_playtests_against_the_tagged_glob():
    steps = _player_playtest_steps()
    step = next(s for s in steps if s.get("name") == "Run Player Fan-Out (.playtest @needs player)")
    run = step.get("run", "")
    assert "scripts/run_player_playtests.py" in run
    assert "StreamingAssets/Playtests" in run


def test_no_job_level_if_references_matrix():
    """A14's own finding, re-verified here: GitHub Actions forbids `matrix` in
    a job-level `if:` — the gate must stay step-level."""
    data = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))
    job = data["jobs"]["player-playtest"]
    assert "matrix" not in job.get("if", "")
