"""Tests for verify.py helper functions (no Unity required)."""
from unity_mcp.tools.verify import _extract_ratio


def test_extract_ratio_fraction():
    assert _extract_ratio("3/3 passed") == "3/3"


def test_extract_ratio_no_fraction_all_green():
    assert _extract_ratio("All green") == "ok"


def test_extract_ratio_no_fraction_passed_count():
    assert _extract_ratio("23 tests passed") == "23 passed"


def test_extract_ratio_passed_singular():
    assert _extract_ratio("1 test passed") == "1 passed"


def test_extract_ratio_prefers_fraction_over_passed():
    # When both patterns present, fraction wins (first branch)
    assert _extract_ratio("5/5 (5 tests passed)") == "5/5"
