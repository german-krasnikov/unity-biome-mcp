"""Live complex movement tests with screenshot checkpoints for GridTest scene.

Requires:
  - Unity running with GridTest scene open
  - MCP TCP bridge on 127.0.0.1:9500
  - Scene: /GridPlayer with GridPlayer component
  - Collectibles at (3,3), (7,7), (5,0)

Run: pytest tests/live/test_gridtest_movement.py -v -m live
"""
import asyncio
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

SCREENSHOTS_DIR = "unity-test-project/ScreenShots"


def _shot_ok(result) -> bool:
    """Screenshot returns file key (not data)."""
    return bool(result.get("file") or result.get("data"))


def _assert_score(text: str, expected: int, label: str) -> None:
    assert f"Score: {expected}" in text or f"Score={expected}" in text, (
        f"{label}: expected Score=={expected}, got: {text}"
    )


async def _wait_idle(bridge, timeout: float = 5.0) -> None:
    await bridge.send("wait_until", {
        "path": PLAYER, "component": COMP,
        "field": "IsMoving", "value": "False", "timeout": str(int(timeout))
    })


# ---------------------------------------------------------------------------
# Test 1: Sequential waypoints with DSL + screenshot checkpoints
# ---------------------------------------------------------------------------

async def test_sequential_waypoints_with_snapshots(play_session):
    """Complex path: Reset → 3 collectibles → SNAPSHOT at each stage."""
    script = f"""
TIMESCALE 10
SET {PLAYER} {COMP} MoveSpeed 50
INVOKE {PLAYER} {COMP} ResetState
WAIT 0.1

INVOKE {PLAYER} {COMP} MoveTo 5,0
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
ASSERT {PLAYER}|{COMP}|Score == 1
LOG Checkpoint 1: reached (5,0), Score=1 (Collectible_3 picked up)

INVOKE {PLAYER} {COMP} MoveTo 3,3
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
ASSERT {PLAYER}|{COMP}|Score == 2
LOG Checkpoint 2: reached (3,3), Score=2 (Collectible_1 picked up)

INVOKE {PLAYER} {COMP} Move north
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move north
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move north
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move north
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
ASSERT {PLAYER}|{COMP}|PosZ == 7
LOG Checkpoint 3: PosZ=7 after 4x north

INVOKE {PLAYER} {COMP} Move east
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move east
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move east
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
INVOKE {PLAYER} {COMP} Move east
WAIT_UNTIL {PLAYER}|{COMP}|IsMoving == False TIMEOUT 5
ASSERT {PLAYER}|{COMP}|PosX == 7
ASSERT {PLAYER}|{COMP}|Score == 3
LOG Checkpoint 4: reached (7,7), Score=3 (Collectible_2 picked up)

SNAPSHOT {PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ,{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|MoveCount
ASSERT {PLAYER}|{COMP}|MoveCount >= 10
ASSERT_CONSOLE_CLEAN
LOG Test complete — all 3 collectibles collected
TIMESCALE 1
"""
    result = await play_session.send("run_playtest", {"script": script, "timeout": 60})
    text = _data(result)
    assert "FAIL" not in text.upper() or "0 fail" in text.lower(), (
        f"Playtest had failures:\n{text}"
    )


# ---------------------------------------------------------------------------
# Test 2: Batch operations with screenshot verification
# ---------------------------------------------------------------------------

async def test_batch_movement_with_screenshots(play_session):
    """Batch-driven path with screenshot at each stage.

    Score progression: 0 → 1 → 3.
    Uses _reset() to restore fresh collectible state instead of restarting Play Mode.
    """
    # Step 1: Reset state (re-enables collectibles), then set fast speed
    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "ResetState", "args": ""
    })
    await asyncio.sleep(0.5)  # let ResetState propagate before changing MoveSpeed
    await play_session.send("set_runtime_property", {
        "path": PLAYER, "component": COMP, "field": "MoveSpeed", "value": "50"
    })

    # Step 2: Verify start state + screenshot #1
    state0 = await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ,{PLAYER}|{COMP}|Score"
    })
    assert "Score" in _data(state0), f"Score field missing at start: {_data(state0)}"

    shot1 = await play_session.send("screenshot", {"camera": "overview"})
    assert _shot_ok(shot1), "screenshot #1 returned empty"

    # Step 3: Move to (5,0) — picks up Collectible_3 (Score→1)
    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "MoveTo", "args": "5,0"
    })
    await _wait_idle(play_session, timeout=5)

    text1 = _data(await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ"
    }))
    _assert_score(text1, 1, "After MoveTo(5,0)")

    shot2 = await play_session.send("screenshot", {"camera": "overview"})
    assert _shot_ok(shot2), "screenshot #2 returned empty"

    # Step 4: (3,3) → Collectible_1 (Score→2), then (7,7) → Collectible_2 (Score→3)
    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "MoveTo", "args": "3,3"
    })
    await _wait_idle(play_session, timeout=5)

    await play_session.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "MoveTo", "args": "7,7"
    })
    await _wait_idle(play_session, timeout=5)

    text2 = _data(await play_session.send("query_state", {
        "queries": f"{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ"
    }))
    _assert_score(text2, 3, "After MoveTo(7,7)")

    shot3 = await play_session.send("screenshot", {"camera": "overview"})
    assert _shot_ok(shot3), "screenshot #3 returned empty"

    # Step 5: Final state + overview screenshot #4
    text_final = _data(await play_session.send("query_state", {
        "queries": (
            f"{PLAYER}|{COMP}|Score,{PLAYER}|{COMP}|MoveCount,"
            f"{PLAYER}|{COMP}|PosX,{PLAYER}|{COMP}|PosZ"
        )
    }))
    _assert_score(text_final, 3, "Final state")
    assert "MoveCount" in text_final, f"MoveCount missing: {text_final}"

    shot4 = await play_session.send("screenshot", {"camera": "overview"})
    assert _shot_ok(shot4), "screenshot #4 returned empty"
