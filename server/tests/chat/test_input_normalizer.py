"""TDD tests for InputNormalizer (Cycle 6b). Scenarios 10-19."""
import pytest
from unittest.mock import AsyncMock

from unity_mcp.input_normalizer import normalize_value


def test_normalize_bool_python_true():
    assert normalize_value(True) == "true"


def test_normalize_bool_python_false():
    assert normalize_value(False) == "false"


@pytest.mark.parametrize("v", ["True", "TRUE", "yes", "1", "on", " True ", " YES "])
def test_normalize_bool_string_variants(v):
    assert normalize_value(v) == "true"


@pytest.mark.parametrize("v", ["False", "FALSE", "no", "0", "off", " False ", " NO "])
def test_normalize_bool_false_variants(v):
    assert normalize_value(v) == "false"


def test_normalize_none_for_objref():
    assert normalize_value(None) == "null"


def test_normalize_list_to_csv():
    assert normalize_value([1, 2, 3]) == "1,2,3"


def test_normalize_int_passes_through():
    assert normalize_value(42) == "42"


def test_normalize_string_passes_through():
    assert normalize_value("hello") == "hello"


def test_normalize_already_normalized_idempotent():
    # Round-trip stability: canonical forms must not drift on second pass.
    # Note: "null" passes through as-is (string branch); only Python None coerces to "null".
    assert normalize_value("true") == "true"
    assert normalize_value("false") == "false"
    assert normalize_value("null") == "null"
