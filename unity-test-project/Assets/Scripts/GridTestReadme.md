# GridTest scene

`GridTest` is the small Play Mode fixture used to exercise public runtime and
playtest tools. It contains only deterministic grid movement and collectibles.

## Scene layout

```text
GridTest
├── GridPlayer          GridPlayer.cs; starts at (0, 0)
├── Collectible_1       (3, 0.5, 3)
├── Collectible_2       (7, 0.5, 7)
├── Collectible_3       (5, 0.5, 0)
├── Plane               10 × 10 ground
├── Directional Light
└── Main Camera         orthographic overview
```

Valid grid coordinates are `0` through `9` on each axis.

## GridPlayer contract

`query_state` can read these public fields:

| Field | Type | Initial value or meaning |
|---|---|---|
| `MoveSpeed` | `float` | Movement speed; default `5` |
| `GridSize` | `int` | Grid width and height; default `10` |
| `PosX`, `PosZ` | `int` | Current grid cell |
| `Score` | `int` | Collected item count |
| `IsMoving` | `bool` | Whether movement is in progress |
| `MoveCount` | `int` | Accepted and started move count |

`invoke_method` can call:

- `Move(string direction)`: accepts `north`, `south`, `east`, or `west` and
  returns `ok`, `error:already_moving`, `error:invalid_direction:<value>`, or
  `error:out_of_bounds:(x,z)`.
- `MoveTo(int x, int z)`: moves to a valid cell and returns `ok`,
  `error:already_moving`, or `error:out_of_bounds:(x,z)`.
- `ResetState()`: restores position, score, movement, and move count.

## Public-tool examples

Read two fields in one request:

```python
await query_state(
    queries="/GridPlayer|GridPlayer|PosX,/GridPlayer|GridPlayer|Score"
)
```

Call a public method:

```python
await invoke_method(
    path="/GridPlayer",
    component="GridPlayer",
    method="Move",
    args="north",
)
```

Wait for movement to finish:

```python
await wait_until(
    path="/GridPlayer",
    component="GridPlayer",
    field="IsMoving",
    value="False",
    timeout=10,
)
```

Runtime property writes are part of the playtest DSL, not a public standalone
MCP tool. For example:

```text
TIMESCALE 10
SET /GridPlayer GridPlayer MoveSpeed 50
INVOKE /GridPlayer GridPlayer ResetState
INVOKE /GridPlayer GridPlayer Move north
WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 5
ASSERT /GridPlayer|GridPlayer|PosZ == 1
ASSERT /GridPlayer|GridPlayer|MoveCount >= 1
SNAPSHOT /GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX
TIMESCALE 1
ASSERT_CONSOLE_CLEAN
```

`MoveCount` increments when a valid move starts, before its movement coroutine
finishes. Use `IsMoving == False` or the final position as completion evidence.

The fixture's in-memory fields, transforms, and collectible state normally reset
when Play Mode stops. File, asset, and project-setting side effects do not.

## Run the fixture tests

Open `unity-test-project` in Unity with the `GridTest` scene loaded, then run
from the repository root:

```bash
export UNITY_MCP_PROJECT_PATH="/absolute/path/to/unity-test-project"
cd server
python -m pytest tests/live/test_gridtest_playmode.py -m live -v
```

For one case, append its node ID, for example
`::test_invoke_move_north_returns_ok`. The live harness rejects a missing or
invalid `UNITY_MCP_PROJECT_PATH`; it does not silently choose a project.

Expected fixture behavior includes out-of-bounds rejection, deterministic
reset, collection at `(3,3)`, `(7,7)`, and `(5,0)`, and one-cell movement
completing within 0.5 seconds when `MoveSpeed` is `50`.
