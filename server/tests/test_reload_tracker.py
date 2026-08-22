"""Tracker state tests — fresh, touch, multi-touch, reset, alias."""
import pytest
from unity_mcp import reload_risk


@pytest.fixture(autouse=True)
def _reset():
    reload_risk.reset()
    yield
    reload_risk.reset()


def test_initial_state():
    assert not reload_risk.has_touches()
    assert reload_risk.current_count() == 0


def test_touch_increments():
    reload_risk.touch()
    assert reload_risk.has_touches()
    assert reload_risk.current_count() == 1


def test_five_touches():
    for _ in range(5):
        reload_risk.touch()
    assert reload_risk.current_count() == 5


def test_reset_clears():
    reload_risk.touch()
    reload_risk.reset()
    assert not reload_risk.has_touches()
    assert reload_risk.current_count() == 0


def test_on_compile_clean_is_reset_alias():
    reload_risk.touch()
    reload_risk.on_compile_clean()
    assert not reload_risk.has_touches()
