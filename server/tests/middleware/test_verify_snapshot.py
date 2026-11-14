"""Tests for post-mutation snapshot verification in middleware."""
import pytest
from unity_mcp.middleware import Middleware


@pytest.fixture
def mw():
    return Middleware()


# ─── parse_set_property_snapshot ─────────────────────────────────────────────

def test_verify_snapshot_match_appends_verified(mw):
    # set_property response includes a component snapshot with the written value
    response = "ok\n[Transform]\nposition: (1, 2, 3)\nrotation: (0, 0, 0, 1)\n"
    result = mw.verify_snapshot(response, prop="position", value="(1, 2, 3)")
    assert "[VERIFIED: position=(1, 2, 3)]" in result


def test_verify_snapshot_mismatch_appends_fail(mw):
    response = "ok\n[Transform]\nposition: (0, 0, 0)\n"
    result = mw.verify_snapshot(response, prop="position", value="(1, 2, 3)")
    assert "[VERIFY FAIL:" in result
    assert "expected (1, 2, 3)" in result
    assert "got (0, 0, 0)" in result


def test_verify_snapshot_no_snapshot_returns_unchanged(mw):
    # Response has no component snapshot (no [...] header)
    response = "ok set property"
    result = mw.verify_snapshot(response, prop="speed", value="5")
    assert result == "ok set property"


def test_verify_snapshot_prop_not_in_snapshot_returns_unchanged(mw):
    # Snapshot exists but the prop isn't there
    response = "ok\n[Rigidbody]\nmass: 1\n"
    result = mw.verify_snapshot(response, prop="speed", value="5")
    assert "[VERIFIED" not in result
    assert "[VERIFY FAIL" not in result


def test_verify_snapshot_preserves_original_text(mw):
    response = "ok\n[Transform]\nposition: (5, 0, 0)\n"
    result = mw.verify_snapshot(response, prop="position", value="(5, 0, 0)")
    assert "ok" in result
    assert "[Transform]" in result


def test_verify_snapshot_case_insensitive_prop(mw):
    response = "ok\n[Transform]\nPosition: (3, 0, 0)\n"
    result = mw.verify_snapshot(response, prop="position", value="(3, 0, 0)")
    assert "[VERIFIED" in result
