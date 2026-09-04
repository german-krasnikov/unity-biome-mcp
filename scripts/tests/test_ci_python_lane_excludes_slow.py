"""A07a: the PR-lane `ci-python.yml` pytest steps must route slow
real-wallclock tests to nightly instead of paying for them on every PR.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow file only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "ci-python.yml"


def _pytest_run_steps() -> list[str]:
    data = yaml.safe_load(WORKFLOW_PATH.read_text(encoding="utf-8"))
    steps = []
    for job in data["jobs"].values():
        for step in job.get("steps", []):
            run = step.get("run", "")
            if "pytest tests/" in run:
                steps.append(run)
    return steps


def test_ci_python_lane_excludes_slow():
    steps = _pytest_run_steps()
    assert steps, "expected at least one 'pytest tests/' run step in ci-python.yml"
    for run in steps:
        marker_expr = run.split('-m "', 1)[1].split('"', 1)[0]
        assert "not slow" in marker_expr, f"missing 'not slow' in: {marker_expr}"
        assert "not live" in marker_expr, f"missing 'not live' in: {marker_expr}"
        assert "not monkey" in marker_expr, f"missing 'not monkey' in: {marker_expr}"
