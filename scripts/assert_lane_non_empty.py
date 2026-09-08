"""Gap 2b (PR-07): fail closed when a named CI lane's filter selected zero
tests. TestRunReconciler's ZERO_TEST_MATCH handling is deliberately lenient
for ad hoc developer filters (must not change) -- this script is the
named-lane-level guard one layer up: `allow_empty` in
ci/biome-test-lanes.json is enforced here, not decorative.

Usage: assert_lane_non_empty.py <lane-name> <collected-count>
"""

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LANES_PATH = REPO_ROOT / "ci" / "biome-test-lanes.json"


def load_lanes(path: Path = LANES_PATH) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))["lanes"]


def check(lane_name: str, count: int, lanes: dict) -> str | None:
    """Returns an error message if the lane must fail closed, else None."""
    if lane_name not in lanes:
        return f"unknown lane {lane_name!r}; known lanes: {sorted(lanes)}"
    allow_empty = lanes[lane_name]["filter"]["allow_empty"]
    if count == 0 and not allow_empty:
        return f"lane {lane_name!r} selected zero tests and allow_empty is false"
    return None


def main(argv: list[str] | None = None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    if len(argv) != 2:
        print("usage: assert_lane_non_empty.py <lane-name> <collected-count>", file=sys.stderr)
        return 2
    lane_name, count_raw = argv
    try:
        count = int(count_raw)
    except ValueError:
        print(f"collected-count must be an integer, got {count_raw!r}", file=sys.stderr)
        return 2

    error = check(lane_name, count, load_lanes())
    if error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
