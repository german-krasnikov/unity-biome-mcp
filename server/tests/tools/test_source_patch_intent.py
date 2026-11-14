"""P0-70: local Source Patch mutation-intent cache. Pure process-local state —
no Unity, no network. See tools/_source_patch_intent.py's own docstring for
why this must never become a query (frozen "exactly one send" invariant in
test_server_asset.py)."""
import pytest

from unity_mcp.tools import _source_patch_intent as intent


@pytest.fixture(autouse=True)
def _reset_cached_intent():
    """Structural isolation: reset the module-level cache before AND after
    every test, so no test can leak its intent value into the next one
    regardless of how the test body exits."""
    intent.set_cached_intent(False)
    yield
    intent.set_cached_intent(False)


def test_default_intent_is_off():
    assert intent.get_cached_intent() is False


def test_set_cached_intent_true_then_read():
    intent.set_cached_intent(True)
    assert intent.get_cached_intent() is True


def test_set_cached_intent_false_then_read():
    intent.set_cached_intent(True)
    intent.set_cached_intent(False)
    assert intent.get_cached_intent() is False
