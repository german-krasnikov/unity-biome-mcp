"""TDD tests for debug/snapshots: capture, compare, diff, parse."""
import pytest
from unittest.mock import AsyncMock


# ---------------------------------------------------------------------------
# 1. parse_inspect_output
# ---------------------------------------------------------------------------

def test_parse_inspect_output_basic_fields():
    from unity_mcp.debug.snapshots import parse_inspect_output
    text = "Transform\n  position: 0,1,0\n  scale: 1,1,1"
    parsed = parse_inspect_output(text)
    assert "position" in parsed
    assert parsed["position"] == "0,1,0"


def test_parse_inspect_output_empty():
    from unity_mcp.debug.snapshots import parse_inspect_output
    assert parse_inspect_output("") == {}


def test_parse_inspect_output_no_indent():
    from unity_mcp.debug.snapshots import parse_inspect_output
    text = "field: value\nother: test"
    parsed = parse_inspect_output(text)
    assert "field" in parsed
    assert parsed["field"] == "value"


def test_parse_inspect_output_skips_component_headers():
    from unity_mcp.debug.snapshots import parse_inspect_output
    text = "NavMeshAgent\n  speed: 3.5\nRigidbody\n  mass: 1.0"
    parsed = parse_inspect_output(text)
    assert "speed" in parsed
    assert "mass" in parsed
    assert "NavMeshAgent" not in parsed


def test_parse_inspect_output_prefixes_keys_for_colon_headers():
    from unity_mcp.debug.snapshots import parse_inspect_output
    text = "Transform:\n  position: 0,0,0\nRigidbody:\n  mass: 1.0"
    parsed = parse_inspect_output(text)
    assert "Transform.position" in parsed
    assert "Rigidbody.mass" in parsed
    assert parsed["Transform.position"] == "0,0,0"


# ---------------------------------------------------------------------------
# 2. diff_snapshots
# ---------------------------------------------------------------------------

def test_diff_snapshots_shows_changed_field():
    from unity_mcp.debug.snapshots import diff_snapshots
    old = {"state": {"hp": "100", "pos": "0,0,0"}, "console": ""}
    new = {"state": {"hp": "50", "pos": "0,0,0"}, "console": ""}
    result = diff_snapshots(old, new)
    assert "hp" in result


def test_diff_snapshots_no_changes_message():
    from unity_mcp.debug.snapshots import diff_snapshots
    snap = {"state": {"hp": "100"}, "console": ""}
    result = diff_snapshots(snap, snap)
    assert "no change" in result.lower()


def test_diff_snapshots_shows_added_field():
    from unity_mcp.debug.snapshots import diff_snapshots
    old = {"state": {"hp": "100"}, "console": ""}
    new = {"state": {"hp": "100", "shield": "50"}, "console": ""}
    result = diff_snapshots(old, new)
    assert "shield" in result


def test_diff_snapshots_console_diff_included():
    from unity_mcp.debug.snapshots import diff_snapshots
    old = {"state": {}, "console": ""}
    new = {"state": {}, "console": "NullReferenceException"}
    result = diff_snapshots(old, new)
    assert "NullReferenceException" in result


# ---------------------------------------------------------------------------
# 3. snapshot() — capture and compare
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_snapshot_capture_stores_label():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0\n  rotation: 0,0,0,1")
    result = await mod.snapshot(path="/Enemy", label="before")
    assert "before" in mod._snapshots
    assert "saved" in result


@pytest.mark.asyncio
async def test_snapshot_capture_reports_field_count():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0\n  rotation: 0,0,0,1\n  scale: 1,1,1")
    result = await mod.snapshot(path="/Enemy", label="x")
    assert "fields" in result


@pytest.mark.asyncio
async def test_snapshot_missing_compare_label_returns_error():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0")
    result = await mod.snapshot(path="/Enemy", label="after", compare="before")
    assert "not found" in result


@pytest.mark.asyncio
async def test_snapshot_same_label_compare_returns_error():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._snapshots["baseline"] = {"state": {"hp": "100"}, "console": ""}
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0")
    result = await mod.snapshot(path="/Enemy", label="baseline", compare="baseline")
    assert "same" in result


@pytest.mark.asyncio
async def test_snapshot_diff_detects_changed_field():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._snapshots["before"] = {
        "state": {"position": "0,0,0", "hp": "100"}, "console": ""
    }
    mod._send = AsyncMock(return_value="Transform\n  position: 5,0,0\n  hp: 50")
    result = await mod.snapshot(path="/Enemy", label="after", compare="before")
    assert "position" in result or "hp" in result


@pytest.mark.asyncio
async def test_snapshot_diff_no_changes():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mod._snapshots["before"] = {"state": {"position": "0,0,0"}, "console": ""}
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0")
    result = await mod.snapshot(path="/Enemy", label="after", compare="before")
    assert isinstance(result, str)


@pytest.mark.asyncio
async def test_snapshot_sends_bool_full_and_no_distill():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    mock = AsyncMock(return_value="x: 1")
    mod._send = mock
    await mod.snapshot(path="/Go", label="t")
    inspect_call = mock.call_args_list[0][0][1]
    assert inspect_call["full"] is True
    assert inspect_call.get("_no_distill") is True


@pytest.mark.asyncio
async def test_snapshot_get_console_uses_count():
    import unity_mcp.debug.snapshots as mod
    mod._snapshots.clear()
    calls = []

    async def fake_send(cmd, args):
        calls.append((cmd, args))
        return "x: 1"

    mod._send = fake_send
    await mod.snapshot(path="/Go", label="t")
    console_calls = [c for c in calls if c[0] == "get_console"]
    assert console_calls
    assert console_calls[0][1].get("count") == 10
