# GridTest Scene — MCP Test Polygon

Test sandbox for all Play Mode MCP tools. No game logic beyond the grid.

## Scene Structure

```
GridTest (scene)
├── GridPlayer          — MonoBehaviour: GridPlayer.cs
├── Collectible_1       — MonoBehaviour: Collectible.cs  (pos 3,0.5,3)
├── Collectible_2       — MonoBehaviour: Collectible.cs  (pos 7,0.5,7)
├── Collectible_3       — MonoBehaviour: Collectible.cs  (pos 5,0.5,0)
├── Plane               — 10x10 ground
├── Directional Light
└── Main Camera         — orthographic, size 8, position (14,12,14)
```

GridPlayer starts at grid position (0,0). GridSize=10, so valid range is [0..9].

## GridPlayer API

### Public Fields (readable via query_state / set_runtime_property)

| Field | Type | Description |
|-------|------|-------------|
| MoveSpeed | float | Units/sec (default 5) |
| GridSize | int | Grid dimension (default 10) |
| PosX | int | Current X position |
| PosZ | int | Current Z position |
| Score | int | Collectibles picked up |
| IsMoving | bool | True while animation runs |
| MoveCount | int | Total moves made |

### Public Methods (invokable via invoke_method)

#### Move(string direction) -> string
Move one step in cardinal direction.
- Args: `"north"` | `"south"` | `"east"` | `"west"`
- Returns: `"ok"` on success
- Returns: `"error:already_moving"` if IsMoving is true
- Returns: `"error:invalid_direction:<dir>"` for unknown direction
- Returns: `"error:out_of_bounds:(<x>,<z>)"` if target outside [0..GridSize)

#### MoveTo(int x, int z) -> string
Teleport-style move to grid cell (x,z).
- Args: `"3,4"` (comma-separated)
- Returns: `"ok"` on success
- Returns: `"error:already_moving"` if already in motion
- Returns: `"error:out_of_bounds:(<x>,<z>)"` if out of range

#### ResetState() -> void
Reset player to (0,0), Score=0, MoveCount=0, IsMoving=false.

## MCP Tool Coverage

### invoke_method
```python
bridge.send("invoke_method", {
    "path": "/GridPlayer", "component": "GridPlayer",
    "method": "Move", "args": "north"
})
```

### query_state
```python
bridge.send("query_state", {
    "queries": "/GridPlayer|GridPlayer|PosX,/GridPlayer|GridPlayer|Score"
})
```

### set_runtime_property
```python
bridge.send("set_runtime_property", {
    "path": "/GridPlayer", "component": "GridPlayer",
    "field": "MoveSpeed", "value": "20"
})
```

### batch
```python
bridge.send("batch", {
    "commands": (
        "set_runtime_property path=/GridPlayer component=GridPlayer field=MoveSpeed value=50\n"
        "query_state queries=/GridPlayer|GridPlayer|MoveSpeed"
    )
})
```

### run_playtest DSL
```
TIMESCALE 10
SET /GridPlayer GridPlayer MoveSpeed 50
INVOKE /GridPlayer GridPlayer ResetState
WAIT 0.1
INVOKE /GridPlayer GridPlayer Move north
WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 5
ASSERT /GridPlayer|GridPlayer|PosZ == 1
ASSERT /GridPlayer|GridPlayer|MoveCount >= 1
SNAPSHOT /GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX
TIMESCALE 1
ASSERT_CONSOLE_CLEAN
LOG Test complete
```

### wait_until
```python
bridge.send("wait_until", {
    "path": "/GridPlayer", "component": "GridPlayer",
    "field": "IsMoving", "value": "False", "timeout": 10
})
```

### screenshot
```python
bridge.send("screenshot", {})
bridge.send("screenshot", {"camera": "overview"})
```

## Running Tests

```bash
# All live tests (requires Unity + GridTest scene open)
cd server && pytest tests/live/test_gridtest_playmode.py -v -m live

# Single test
pytest tests/live/test_gridtest_playmode.py::test_invoke_move_north_returns_ok -v -m live

# All unit tests (no Unity required)
pytest tests/ -m "not live" -q
```

## Expected Behavior

| Test | Setup | Expected |
|------|-------|----------|
| Move north | Reset (0,0) | ok, PosZ=1 |
| Move west at (0,0) | Reset | error:out_of_bounds |
| Move with bad dir | any | error:invalid_direction |
| Second move while moving | low speed | error:already_moving |
| MoveTo(-1,0) | any | error:out_of_bounds |
| ResetState after moves | any | PosX=PosZ=Score=MoveCount=0 |
| MoveSpeed=50 | Play Mode | IsMoving=False within 0.5s for 1 cell |
| TIMESCALE 10 | DSL | all timers run 10x faster |
| Collectible at (3,3) | MoveTo 3,3 | Score increments by 1 |
| Collectible at (7,7) | MoveTo 7,7 | Score increments by 1 |
| Collectible at (5,0) | MoveTo 5,0 | Score increments by 1 |

## Notes

- `Application.runInBackground = true` is set in `Start()` — animation runs even without Game View focus.
- `TIMESCALE` + high `MoveSpeed` (50) = fast tests with no sleeps.
- Collectibles at fixed positions: (3,3), (7,7), (5,0). Pickup radius: 0.4 units (OverlapSphere).
- All runtime changes (MoveSpeed, etc.) are discarded when Play Mode stops.
