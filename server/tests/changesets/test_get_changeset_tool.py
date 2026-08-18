"""T16: get_changeset MCP tool unit tests (5 tests)."""

from unittest.mock import MagicMock, patch

import pytest


def _make_coordinator_with_op():
    """Helper: coordinator with one property operation."""
    from unity_mcp.changeset_coordinator import ChangeSetCoordinator
    coord = ChangeSetCoordinator(get_session_id=lambda: "s1", _no_journal=True)
    receipt = {
        "path": "/Player", "op": "modify", "t": "property",
        "prop": "Health", "before": "100", "after": "50", "rev": True,
    }
    coord.append("set_property", receipt)
    return coord


async def _call_get_changeset():
    from unity_mcp.tools.changeset_tool import get_changeset
    return await get_changeset()


_COORD_PATH = "unity_mcp.changeset_coordinator.get_coordinator"


@pytest.mark.asyncio
async def test_no_coordinator_returns_no_changeset():
    with patch(_COORD_PATH, return_value=None):
        result = await _call_get_changeset()
    assert result == "no_changeset"


@pytest.mark.asyncio
async def test_empty_coordinator_returns_no_changeset():
    coord = MagicMock()
    coord.get_current.return_value = None
    with patch(_COORD_PATH, return_value=coord):
        result = await _call_get_changeset()
    assert result == "no_changeset"


@pytest.mark.asyncio
async def test_returns_header_line():
    coord = _make_coordinator_with_op()
    with patch(_COORD_PATH, return_value=coord):
        result = await _call_get_changeset()

    lines = result.splitlines()
    header = lines[0]
    assert "cs:" in header
    assert "status:" in header
    assert "ops:1" in header


@pytest.mark.asyncio
async def test_returns_op_lines():
    coord = _make_coordinator_with_op()
    with patch(_COORD_PATH, return_value=coord):
        result = await _call_get_changeset()

    lines = result.splitlines()
    assert len(lines) == 2  # header + 1 op
    op_line = lines[1]
    assert "modify" in op_line
    assert "property" in op_line
    assert "/Player" in op_line
    assert "bh:" in op_line
    assert "ah:" in op_line


@pytest.mark.asyncio
async def test_text_parseable_by_csharp_format():
    """Output format matches what ChangeSetParser.Parse() expects."""
    coord = _make_coordinator_with_op()
    with patch(_COORD_PATH, return_value=coord):
        result = await _call_get_changeset()

    header = result.splitlines()[0]
    # Header must contain cs:<id> and status:<value>
    assert header.startswith("cs:")
    parts = {p.split(":")[0]: p.split(":", 1)[1]
             for p in header.split() if ":" in p}
    assert "cs" in parts
    assert "status" in parts
    assert "ops" in parts

    op_line = result.splitlines()[1]
    op_parts = op_line.split()
    assert op_parts[0] in ("create", "modify", "delete")
    assert "rev:" in op_line
