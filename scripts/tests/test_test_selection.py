"""C14: thin, frozen `TestSelectionFilter` DTO (Python only, no C# mirror).

Scope cut (plan R-08): the C# `sealed record` mirror and the Python<->C#
key-parity test are dropped until the first real C# consumer (Wave F
"first external extension"), per `api-design-standards.md`.

Round-trips a filter to/from the exact JSON shape a `biome-test-lanes.json`
entry (C15) uses -- the 8 snake_case keys are frozen now because a future
C# mirror will be held to them.

Runs in the standard scripts/tests lane: no Unity, no network, pure data.
"""
import sys
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))

from test_selection import TestSelectionFilter  # noqa: E402

# Mirrors pr-python-core's real exclude_capabilities (C15) -- not aspirational data.
_SAMPLE = {
    "layers": [],
    "modes": ["editmode"],
    "environments": [],
    "speeds": [],
    "include_tags": [],
    "exclude_tags": [],
    "exclude_capabilities": ["live", "monkey", "slow"],
    "allow_empty": False,
}


def test_round_trip_preserves_exact_snake_case_keys():
    restored = TestSelectionFilter.from_dict(_SAMPLE)
    round_tripped = restored.to_dict()
    assert set(round_tripped) == set(_SAMPLE)
    assert round_tripped == _SAMPLE


def test_unknown_key_is_rejected_not_silently_dropped():
    bad = dict(_SAMPLE)
    bad["typo_field"] = ["oops"]
    with pytest.raises(ValueError, match="typo_field"):
        TestSelectionFilter.from_dict(bad)
