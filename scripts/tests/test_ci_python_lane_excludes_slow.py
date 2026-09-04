"""A07a: the PR-lane `ci-python.yml` pytest steps must route slow
real-wallclock tests to nightly instead of paying for them on every PR.

Since C17, the `-m "..."` value in ci-python.yml is a generated expression
($(python ../scripts/gen_lane_args.py pytest <lane>)), not a literal string --
this test extracts the referenced lane name and asks C16's real generator
what it actually produces, so the guard still proves the real runtime `-m`
value survives, not just scans dead YAML surface text.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow file + Tests/biome-test-lanes.json + Tests/taxonomy-map.json.
"""
import re
import sys
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "ci-python.yml"
REQUIRED_EXCLUDES = ("not slow", "not live", "not monkey")

SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))
import gen_lane_args  # noqa: E402

_GENERATED_LANE_RE = re.compile(r"gen_lane_args\.py pytest ([\w-]+)")


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
    lanes = gen_lane_args.load_lanes()
    dimensions = gen_lane_args.load_dimensions()
    for run in steps:
        # Assumes -m "..." is double-quoted, matching ci-python.yml's convention.
        marker_expr = run.split('-m "', 1)[1].split('"', 1)[0]
        lane_match = _GENERATED_LANE_RE.search(marker_expr)
        assert lane_match, f"expected a gen_lane_args.py pytest <lane> reference in: {marker_expr}"
        generated = gen_lane_args.render_lane("pytest", lane_match.group(1), lanes, dimensions)
        for required in REQUIRED_EXCLUDES:
            assert required in generated, f"missing '{required}' in generated expression: {generated}"
