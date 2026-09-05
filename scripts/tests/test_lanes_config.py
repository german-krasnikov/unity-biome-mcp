"""C15: Tests/biome-test-lanes.json — 4 lanes matching today's real CI.

Reuses C13's `known_dimension_names()` (scripts/tests/test_taxonomy_map.py)
so a lane can never reference a taxonomy dimension the map doesn't know
about. `EXPECTED_LANE_NAMES` stands in for C17's wiring (not yet
implemented in this plan) -- it is the exact 4 lane names C17 will consume,
so a silent rename here would already be caught before C17 lands.

Runs in the standard scripts/tests lane: no Unity, no network, reads two
tracked JSON files only.
"""
import json
import sys
from pathlib import Path

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS))

from test_taxonomy_map import known_dimension_names  # noqa: E402

REPO_ROOT = TESTS.parent.parent
LANES_PATH = REPO_ROOT / "Tests" / "biome-test-lanes.json"

# The 4 lane names C17 (not yet implemented) will wire into ci-python.yml /
# ci-sonar.yml / nightly.yml -- see plan item C15.
EXPECTED_LANE_NAMES = frozenset({
    "pr-python-core",
    "pr-unity-core",
    "master-conformance",
    "nightly-full",
})

# `layers`/`environments` are source-bucket labels, not taxonomy dimensions
# (see Tests/taxonomy-map.json's $note) -- deliberately excluded from the
# taxonomy cross-check below.
_TAXONOMY_VALIDATED_FIELDS = (
    "modes",
    "speeds",
    "include_tags",
    "exclude_tags",
    "exclude_capabilities",
)


def load_lanes() -> dict:
    return json.loads(LANES_PATH.read_text(encoding="utf-8"))["lanes"]


def test_lanes_json_parses_with_exactly_the_expected_lane_names():
    lanes = load_lanes()
    assert set(lanes) == EXPECTED_LANE_NAMES


def test_every_lane_filter_value_is_a_known_taxonomy_dimension():
    dimensions = known_dimension_names()
    lanes = load_lanes()
    for lane_name, lane in lanes.items():
        for field_name in _TAXONOMY_VALIDATED_FIELDS:
            for value in lane["filter"][field_name]:
                assert value in dimensions, (
                    f"lane {lane_name!r} field {field_name!r} references "
                    f"unknown taxonomy dimension {value!r}"
                )


def test_nightly_full_excludes_only_live_not_monkey_or_slow():
    lanes = load_lanes()
    excluded = lanes["nightly-full"]["filter"]["exclude_capabilities"]
    assert "live" in excluded
    assert "monkey" not in excluded
    assert "slow" not in excluded
