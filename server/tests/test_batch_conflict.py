"""Tests for batch conflict detection in middleware."""
import pytest
from unity_mcp.middleware import Middleware


@pytest.fixture
def mw():
    return Middleware()


# ─── scan_batch_conflicts ─────────────────────────────────────────────────────

def test_no_conflict_clean_batch(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/B component=T prop=x value=2"
    assert mw.scan_batch_conflicts(commands) is None


def test_duplicate_write_detected(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/A component=T prop=x value=2"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "duplicate write" in warn.lower()
    assert "x" in warn


def test_create_then_delete_detected(mw):
    commands = "create_object name=Foo\ndelete_object path=/Foo"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "no-op" in warn.lower() and "create+delete" in warn.lower(), warn


def test_delete_then_reference_detected(mw):
    commands = "delete_object path=/Enemy\nset_property path=/Enemy component=T prop=x value=1"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "deleted" in warn.lower() and "referencing" in warn.lower(), warn


def test_empty_commands_no_warn(mw):
    assert mw.scan_batch_conflicts("") is None


def test_single_command_no_warn(mw):
    assert mw.scan_batch_conflicts("set_property path=/A component=T prop=x value=1") is None


def test_duplicate_different_props_no_warn(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/A component=T prop=y value=2"
    assert mw.scan_batch_conflicts(commands) is None


# ─── delete-chain conflict cases ─────────────────────────────────────────────

def test_delete_then_manage_component_detected(mw):
    """manage_component on a deleted path is use-after-delete — must warn."""
    commands = "delete_object path=/Enemy\nmanage_component path=/Enemy type=Rigidbody action=add"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "deleted" in warn.lower(), warn


def test_double_delete_detected(mw):
    """Deleting the same path twice is always a bug — must warn."""
    commands = "delete_object path=/Enemy\ndelete_object path=/Enemy"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "delete" in warn.lower(), warn


def test_delete_then_manage_different_path_no_warn(mw):
    """manage_component on a different path after delete should not warn."""
    commands = "delete_object path=/Enemy\nmanage_component path=/Player type=Rigidbody action=add"
    assert mw.scan_batch_conflicts(commands) is None


def test_double_delete_different_paths_no_warn(mw):
    """Deleting two different objects is fine."""
    commands = "delete_object path=/A\ndelete_object path=/B"
    assert mw.scan_batch_conflicts(commands) is None


# ─── ID-only delete cases (P-12440-032) ──────────────────────────────────────

def test_id_only_deletes_no_false_warning(mw):
    """Four ID-only deletes (no path) must produce no warnings — empty string is not a conflict key."""
    commands = "\n".join([
        "delete_object id=abc1",
        "delete_object id=abc2",
        "delete_object id=abc3",
        "delete_object id=abc4",
    ])
    assert mw.scan_batch_conflicts(commands) is None


def test_id_double_delete_warns(mw):
    """Same ID deleted twice must produce a double-delete warning."""
    commands = "delete_object id=abc123\ndelete_object id=abc123"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "delete" in warn.lower(), warn


def test_id_only_delete_skips_empty_key(mw):
    """delete_object with neither path nor id must not crash and must not warn."""
    commands = "delete_object\ndelete_object"
    assert mw.scan_batch_conflicts(commands) is None
