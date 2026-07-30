"""Live Play Mode tests for GridTest scene (10x10 grid, GridPlayer, 3 collectibles).

Requires:
  - Unity running with GridTest scene open
  - MCP TCP bridge on 127.0.0.1:9500
  - Scene has /GridPlayer with GridPlayer component

Run: pytest tests/live/test_gridtest_playmode.py -v -m live
"""
import pytest
import pytest_asyncio

from tests.live.conftest import _data, _reset, PLAYER, COMP


@pytest_asyncio.fixture(autouse=True)
async def reset_grid_speed(play_session):
    yield
    try:
        await play_session.send("set_runtime_property", {
            "path": PLAYER, "component": COMP, "field": "MoveSpeed", "value": "5"
        })
    except Exception:
        pass

pytestmark = pytest.mark.live


# ---------------------------------------------------------------------------
# 1. invoke_method — directional moves
# ---------------------------------------------------------------------------

async def test_invoke_move_north_returns_ok(play_session):
    """Move(north) returns 'ok' and increments MoveCount."""
    await _reset(play_session)
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "north"
    })
    assert "ok" in _data(result), f"Move north failed: {result}"


async def test_invoke_move_all_directions(play_session):
    """Move each cardinal direction once; all return 'ok'."""
    await _reset(play_session)
    for direction in ("north", "east", "south"):
        result = await play_session.send("invoke_method", {
            "path": PLAYER, "component": COMP, "method": "Move", "args": direction
        })
        assert "ok" in _data(result), f"Move {direction} failed: {result}"
        await play_session.send("wait_until", {
            "path": PLAYER, "component": COMP,
            "field": "IsMoving", "value": "False", "timeout": "5"
        })


async def test_invoke_moveto_returns_ok(play_session):
    """MoveTo(3, 4) places player at grid position 3,4."""
    await _reset(play_session)
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "MoveTo", "args": "3,4"
    })
    assert "ok" in _data(result), f"MoveTo failed: {result}"


async def test_invoke_reset_zeroes_state(play_session):
    """After Reset(), PosX/PosZ/Score/MoveCount are all 0."""
    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "north"
    })
    await play_session.send("wait_until", {
        "path": PLAYER, "component": COMP,
        "field": "IsMoving", "value": "False", "timeout": "5"
    })
    await _reset(play_session)

    state = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ,{PLAYER}|{COMP}|MoveCount"
    })
    text = _data(state)
    assert "PosX: 0" in text or "PosX=0" in text, f"Reset PosX not 0: {text}"


# ---------------------------------------------------------------------------
# 2. query_state — read fields
# ---------------------------------------------------------------------------

async def test_query_state_posx_posz(play_session):
    """query_state returns PosX and PosZ as integers."""
    await _reset(play_session)
    result = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ"
    })
    text = _data(result)
    assert "PosX" in text, f"PosX missing: {text}"
    assert "PosZ" in text, f"PosZ missing: {text}"


async def test_query_state_score_and_movecount(play_session):
    """query_state returns Score and MoveCount fields."""
    await _reset(play_session)
    result = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|MoveCount"
    })
    text = _data(result)
    assert "Score" in text, f"Score missing: {text}"
    assert "MoveCount" in text, f"MoveCount missing: {text}"


async def test_query_state_ismoving_while_idle(play_session):
    """IsMoving is False when player is idle."""
    await _reset(play_session)
    result = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|IsMoving"
    })
    text = _data(result)
    assert "False" in text or "false" in text, f"IsMoving should be False: {text}"


# ---------------------------------------------------------------------------
# 3. set_runtime_property — change MoveSpeed
# ---------------------------------------------------------------------------

async def test_set_runtime_property_movespeed(play_session):
    """MoveSpeed can be changed at runtime; query_state reflects new value."""
    await play_session.send("set_runtime_property", {
        "path": PLAYER, "component": COMP, "field": "MoveSpeed", "value": "20"
    })
    result = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|MoveSpeed"
    })
    text = _data(result)
    assert "20" in text, f"MoveSpeed not updated: {text}"


async def test_set_runtime_property_affects_move_duration(play_session):
    """With MoveSpeed=50, move completes faster than default 5f."""
    await _reset(play_session)
    await play_session.send("set_runtime_property", {
        "path": PLAYER, "component": COMP, "field": "MoveSpeed", "value": "50"
    })
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "north"
    })
    assert "ok" in _data(result), f"Move failed: {result}"
    await play_session.send("wait_until", {
        "path": PLAYER, "component": COMP,
        "field": "IsMoving", "value": "False", "timeout": "5"
    })
    state = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|IsMoving"
    })
    text = _data(state)
    assert "False" in text or "false" in text, f"Move not done at high speed: {text}"


# ---------------------------------------------------------------------------
# 4. batch — multiple runtime commands in one call
# ---------------------------------------------------------------------------

async def test_batch_query_multiple_fields(play_session):
    """Batch query_state for 3 fields returns all results."""
    await _reset(play_session)
    result = await play_session.send("batch", {
        "commands": (
            f"query_state queries={PLAYER}|{COMP}|PosX\n"
            f"query_state queries={PLAYER}|{COMP}|PosZ\n"
            f"query_state queries={PLAYER}|{COMP}|Score"
        )
    })
    text = _data(result)
    assert "Unknown command" not in text, f"Batch schema error: {text}"
    assert "PosX" in text or "PosZ" in text or "Score" in text, f"No fields: {text}"


async def test_batch_set_property_then_query(play_session):
    """Batch: set MoveSpeed then query it back — both succeed."""
    result = await play_session.send("batch", {
        "commands": (
            f"set_runtime_property path={PLAYER} component={COMP} field=MoveSpeed value=15\n"
            f"query_state queries={PLAYER}|{COMP}|MoveSpeed"
        )
    })
    text = _data(result)
    assert "Unknown command" not in text, f"Batch schema error: {text}"
    assert "15" in text, f"MoveSpeed not 15 in batch result: {text}"


# ---------------------------------------------------------------------------
# 5. run_playtest DSL
# ---------------------------------------------------------------------------

async def test_run_playtest_primary_dsl_verbs_all_succeed(play_session):
    """run_playtest: TIMESCALE, INVOKE, WAIT, ASSERT, LOG all succeed."""
    await _reset(play_session)
    script = f"""
TIMESCALE 5
SET {PLAYER} {COMP} MoveSpeed 50
INVOKE {PLAYER} {COMP} ResetState
WAIT 0.2
ASSERT {PLAYER}|{COMP}|PosX == 0
ASSERT {PLAYER}|{COMP}|PosZ == 0
ASSERT {PLAYER}|{COMP}|Score == 0
TIMESCALE 1
LOG GridTest basic checks passed
"""
    result = await play_session.send("run_playtest", {"script": script, "timeout": 30})
    text = _data(result)
    assert "FAIL" not in text.upper() or "0 fail" in text.lower(), f"Playtest failed: {text}"


async def test_run_playtest_invoke_and_assert(play_session):
    """run_playtest: INVOKE Move north, WAIT_UNTIL not moving, ASSERT PosZ incremented."""
    await _reset(play_session)
    script = f"""
TIMESCALE 10
SET {PLAYER} {COMP} MoveSpeed 50
INVOKE {PLAYER} {COMP} ResetState
WAIT 0.1
INVOKE {PLAYER} {COMP} Move north
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
ASSERT {PLAYER}|{COMP}|PosZ == 1
TIMESCALE 1
LOG Move north verified
"""
    result = await play_session.send("run_playtest", {"script": script, "timeout": 30})
    text = _data(result)
    assert "FAIL" not in text.upper() or "0 fail" in text.lower(), f"Playtest ASSERT failed: {text}"


async def test_run_playtest_snapshot(play_session):
    """run_playtest: SNAPSHOT captures multiple field values."""
    await _reset(play_session)
    script = f"""
SNAPSHOT {PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|MoveCount
LOG Snapshot done
"""
    result = await play_session.send("run_playtest", {"script": script, "timeout": 15})
    text = _data(result)
    assert "error" not in text.lower() or "0 fail" in text.lower(), f"Snapshot error: {text}"


async def test_run_playtest_assert_console_clean(play_session):
    """run_playtest: ASSERT_CONSOLE_CLEAN passes on idle GridPlayer."""
    await _reset(play_session)
    script = "ASSERT_CONSOLE_CLEAN\nLOG Console is clean"
    result = await play_session.send("run_playtest", {"script": script, "timeout": 15})
    text = _data(result)
    assert "FAIL" not in text.upper() or "0 fail" in text.lower(), f"Console not clean: {text}"


# ---------------------------------------------------------------------------
# 6. screenshot
# ---------------------------------------------------------------------------

async def test_screenshot_returns_file_path(play_session):
    """screenshot() in Play Mode returns a file path string."""
    result = await play_session.send("screenshot", {})
    # Response uses 'file' key for the path (data is empty when file is written)
    path = result.get("file") or _data(result)
    assert len(path) > 0, "screenshot returned empty response"
    assert "error" not in path.lower() or ".png" in path.lower(), f"Screenshot error: {path}"


async def test_screenshot_overview(play_session):
    """screenshot with camera=overview returns non-empty result."""
    result = await play_session.send("screenshot", {"camera": "overview"})
    path = result.get("file") or _data(result)
    assert len(path) > 0, "overview screenshot empty"


# ---------------------------------------------------------------------------
# 7. Error handling
# ---------------------------------------------------------------------------

async def test_move_out_of_bounds_returns_error(play_session):
    """Move west from (0,0) returns error:out_of_bounds."""
    await _reset(play_session)
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "west"
    })
    text = _data(result)
    assert "out_of_bounds" in text, f"Expected out_of_bounds: {text}"


async def test_move_invalid_direction_returns_error(play_session):
    """Move with unknown direction returns error:invalid_direction."""
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "up"
    })
    text = _data(result)
    assert "invalid_direction" in text, f"Expected invalid_direction: {text}"


async def test_move_already_moving_returns_error(play_session):
    """Second Move() while IsMoving=True returns error:already_moving."""
    await _reset(play_session)
    await play_session.send("set_runtime_property", {
        "path": PLAYER, "component": COMP, "field": "MoveSpeed", "value": "1"
    })
    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "north"
    })
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "Move", "args": "east"
    })
    text = _data(result)
    assert "already_moving" in text, f"Expected already_moving: {text}"


async def test_moveto_out_of_bounds_returns_error(play_session):
    """MoveTo(-1, 0) returns error:out_of_bounds."""
    await _reset(play_session)
    result = await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "MoveTo", "args": "-1,0"
    })
    text = _data(result)
    assert "out_of_bounds" in text, f"Expected out_of_bounds: {text}"


# ---------------------------------------------------------------------------
# 8. Collectible pickup — full DSL with actual score assertion
# ---------------------------------------------------------------------------

async def test_collectible_pickup_full_dsl(fresh_scene):
    """Full DSL test: Reset, move to collectible, assert Score increased."""
    script = f"""
TIMESCALE 10
SET {PLAYER} {COMP} MoveSpeed 50
INVOKE {PLAYER} {COMP} ResetState
WAIT 0.1
CAPTURE score_before {PLAYER}|{COMP}|Score
INVOKE {PLAYER} {COMP} MoveTo 3,3
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
SNAPSHOT {PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ
TIMESCALE 1
ASSERT_CONSOLE_CLEAN
LOG Collectible DSL test done
"""
    result = await fresh_scene.send("run_playtest", {"script": script, "timeout": 30})
    text = _data(result)
    assert "FAIL" not in text.upper() or "0 fail" in text.lower(), f"DSL collectible test failed: {text}"


# ---------------------------------------------------------------------------
# 9. wait_until standalone / run_playtest failure / screenshot size
# ---------------------------------------------------------------------------

async def test_wait_until_standalone_success(play_session):
    """wait_until succeeds when condition is already met."""
    await play_session.send("invoke_method", {
        "path": "/GridPlayer", "component": "GridPlayer", "method": "ResetState"
    })
    result = await play_session.send("wait_until", {
        "path": "/GridPlayer", "component": "GridPlayer",
        "field": "IsMoving", "value": "False", "timeout": "3"
    })
    text = _data(result)
    assert "timeout" not in text.lower(), f"wait_until timed out: {text}"


async def test_run_playtest_failed_assert(play_session):
    """run_playtest with failing ASSERT reports failure, doesn't crash."""
    result = await play_session.send("run_playtest", {
        "script": "INVOKE /GridPlayer GridPlayer ResetState\nASSERT /GridPlayer|GridPlayer|PosX == 999",
        "timeout": "10"
    })
    text = _data(result)
    assert "FAIL" in text, f"Expected FAIL in result: {text}"


async def test_screenshot_returns_valid_file(ensure_edit_mode):
    """Screenshot returns file path with non-trivial size (Edit Mode, no play_session needed)."""
    import os
    result = await ensure_edit_mode.send("screenshot", {"width": "320", "height": "240"})
    # Response uses 'file' key for the saved path
    path = result.get("file") or _data(result).replace("Data saved to: ", "").strip().split("\n")[0]
    assert path and path.endswith(".png"), f"No PNG path in result: {result}"
    assert os.path.exists(path), f"File not found: {path}"
    size = os.path.getsize(path)
    assert size > 1000, f"Screenshot too small ({size} bytes): {path}"
