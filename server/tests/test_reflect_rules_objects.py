"""TDD tests for reflect/rules_objects.py — bugs #1-#4."""
import pytest
from unity_mcp.reflect import reflect, Mismatch


async def _r(cmd, args, response):
    return await reflect(cmd, args, response, None)


# ── Bug #1: manage_component — Error in component name must not skip ──────────

async def test_manage_component_add_errorlogger_no_false_skip():
    """'ErrorLogger' contains 'Error' but is a valid name — must NOT return None early."""
    result = await _r(
        "manage_component",
        {"action": "add", "type": "ErrorLogger"},
        "Added: ErrorLogger. Components: ErrorLogger",
    )
    assert result is None  # confirmation present → no mismatch


async def test_manage_component_add_error_response_returns_none():
    """Error response for add → return None (can't verify), not a false Mismatch about missing confirmation."""
    result = await _r(
        "manage_component",
        {"action": "add", "type": "Rigidbody"},
        "Error: component not found",
    )
    assert result is None


# ── Bug #2: manage_component — leaf substring match ───────────────────────────

async def test_manage_component_add_leaf_substring_mismatch():
    """type=Health but response confirms 'HealthSystem' — should Mismatch (Health != HealthSystem)."""
    result = await _r(
        "manage_component",
        {"action": "add", "type": "Health"},
        "Added: HealthSystem. Components: HealthSystem",
    )
    assert isinstance(result, Mismatch)
    assert "Health" in result.msg


async def test_manage_component_add_exact_match_passes():
    """Exact leaf match in response → no Mismatch."""
    result = await _r(
        "manage_component",
        {"action": "add", "type": "Health"},
        "Added: Health. Components: Health",
    )
    assert result is None


async def test_manage_component_add_namespace_match_passes():
    """Namespaced type — leaf 'Health' in 'health' → pass."""
    result = await _r(
        "manage_component",
        {"action": "add", "type": "Game.Health"},
        "Added: Health. Components: Health",
    )
    assert result is None


# ── Bug #3: create_object — path with spaces ──────────────────────────────────

async def test_create_object_path_with_spaces_captured():
    """Path '/World/Zone A' must be captured fully (space in name)."""
    result = await _r(
        "create_object",
        {"name": "Zone A", "parent": "/World"},
        "Created Zone A at /World/Zone A",
    )
    assert result is None


async def test_create_object_path_with_spaces_mismatch():
    """Wrong name detected even with spaces in path."""
    result = await _r(
        "create_object",
        {"name": "Zone B"},
        "Created Zone A at /World/Zone A",
    )
    assert isinstance(result, Mismatch)


# ── Bug #4/#7: delete_object and set_active error responses ──────────────────

async def test_delete_object_error_response_mismatch():
    """Error response for delete_object → Mismatch (not None)."""
    result = await _r(
        "delete_object",
        {"path": "/World/Enemy"},
        "Error: object not found",
    )
    assert isinstance(result, Mismatch)


async def test_set_active_error_response_mismatch():
    """Error response for set_active → Mismatch (not None — operation failed)."""
    result = await _r(
        "set_active",
        {"active": "true"},
        "Error: object not found",
    )
    assert isinstance(result, Mismatch)


async def test_set_active_happy():
    """Normal set_active response → no Mismatch."""
    result = await _r(
        "set_active",
        {"active": "true"},
        "Set /Player active=true",
    )
    assert result is None


async def test_delete_object_happy():
    """Normal delete response → no Mismatch."""
    result = await _r(
        "delete_object",
        {},
        "Deleted #12345",
    )
    assert result is None
