# Playtest DSL

The parser and `lint_playtest` are authoritative. Lint every maintained file
after changing it.

## Core Forms

```text
VAL $position /Subject|Transform|position
CAPTURE before $position

TELEPORT /Subject 1,0,0
WAIT_UNTIL /Subject|Transform|position == (1,0,0) TIMEOUT 3

ASSERT_CHANGED before
ASSERT /Subject|Transform|position == (1,0,0)
ASSERT_CONSOLE_CLEAN
```

For numeric deltas:

```text
VAL $value /Subject|Counter|Value
CAPTURE before $value
INVOKE /Subject Counter Advance 1
WAIT_CAPTURED before INCREASED_BY == 1 TIMEOUT 3
ASSERT_CAPTURED before INCREASED_BY == 1
ASSERT_CONSOLE_CLEAN
```

Use names and paths that exist in the current project. The example is a pattern,
not a universal component contract.

## Frame Evidence

```text
CAPTURE_FRAMES 4 INTERVAL 0.25 CAMERA game MODE list LABEL motion
ASSERT_FRAMES_DIFFER motion
```

`INTERVAL` is mandatory and the count must be at least two. A differing frame
proves pixel change only.

## UI Interaction

Use a hierarchy path for uGUI controls. Address UI Toolkit elements through the
GameObject that owns `UIDocument`:

```text
CLICK /Canvas/StartButton
CLICK /HUD|UIDocument|submit-button
FILL /HUD|UIDocument|player-name Player1
FOCUS /HUD|UIDocument|player-name
```

`CLICK` supports uGUI paths and the UI Toolkit form. `FILL` and `FOCUS` require
`GameObject|UIDocument|element-name`. Use `inspect_uitk` to confirm element
names before writing a maintained scenario.

## Control Rules

- Use `WAIT_UNTIL` or `WAIT_CAPTURED` with a timeout for state transitions.
- Use `WAIT` only for a deliberate observation duration, not synchronization.
- `FOR $i IN 0..3` is half-open and yields `0`, `1`, `2`.
- `ALIAS` is removed; use `VAL` or `VAR`.
- `defs` prepends reusable `VAL` definitions.
- `snapshot_on_failure` requests textual state evidence; it is not a screenshot.
- End maintained scenarios with `ASSERT_CONSOLE_CLEAN`.

## DRY With `INCLUDE` And `MACRO`

Put shared aliases and stable macros in `.defs` files under
`Assets/PlaytestDefs/`, then include them from scenario files.

```text
# Assets/PlaytestDefs/common.defs
VAL $subject /Subject

MACRO start_fast
  TIMESCALE 5
END_MACRO

MACRO assert_position $expected
  ASSERT $subject|Transform|position == $expected
END_MACRO

MACRO finish_clean
  TIMESCALE 1
  ASSERT_CONSOLE_CLEAN
END_MACRO
```

```text
# Assets/Playtests/subject-position.playtest
INCLUDE common.defs
CALL start_fast
TELEPORT $subject 1,0,0
WAIT_UNTIL $subject|Transform|position == (1,0,0) TIMEOUT 3
CALL assert_position (1,0,0)
CALL finish_clean
```

Use a macro only for a repeated, stable logical block. Keep scenario-specific
actions and assertions in the `.playtest` file so failures remain readable.
Macro definitions cannot nest; calls may compose up to the parser recursion
limit. `INCLUDE` is restricted to `Assets/PlaytestDefs/`.

Use `TIMESCALE 5` for ordinary state-transition tests. Keep `TIMESCALE 1` when
the claim depends on real-time duration, frame pacing, animation timing, or
physics stability. Always restore `TIMESCALE 1` in cleanup.

## Good

```text
CAPTURE before $value
INVOKE /Subject Counter Advance 1
WAIT_CAPTURED before INCREASED_BY == 1 TIMEOUT 3
ASSERT_CAPTURED before INCREASED_BY == 1
```

## Bad

```text
INVOKE /Subject Counter Advance 1
WAIT 5
CAPTURE_FRAMES 2 INTERVAL 0.25 LABEL proof
ASSERT_FRAMES_DIFFER proof
```

The bad form guesses timing and substitutes visual change for behavioral proof.
