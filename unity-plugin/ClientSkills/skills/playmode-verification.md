---
name: playmode-verification
description: Play Mode verification protocol for Unity gameplay testing. Load when asserting gameplay state during or after Play Mode: cargo counts, currency values, event outcomes. Enforces data-only assertions — no screenshot hallucination, no visual guessing. Defines CLAIM/EVIDENCE/VERDICT format, before/after query_state pattern, and run_playtest DSL for scripted multi-step sequences.
user-invocable: false
---

# Play Mode Verification Protocol

**USE run_playtest DSL FOR REPEATABLE TEST SEQUENCES**

## HARD RULES — VIOLATION = INVALID TEST

1. NEVER say "I see X happening" about gameplay state
2. NEVER say "it looks like X is working"
3. ONLY say "GridPlayer.Score = 5 (expected 5) — OK"
4. ONLY say "GridPlayer.MoveCount = 10 (was 5, delta +5) — OK"
5. Screenshot = layout check ONLY (objects visible? position OK?)
6. Screenshot NEVER proves: item transfer, production, selling, money collection
7. If data contradicts visual → DATA WINS
8. NEVER infer gameplay state from screenshot descriptions — always use `query_state` to read actual component values, `inspect` for multi-object field reads
9. NEVER `sleep` to wait for game events — use `wait_until` or `run_playtest` DSL

## Mandatory Verification Format

For EVERY gameplay assertion:

```
CLAIM: "Player scored after moving"
EVIDENCE:
  GridPlayer/Score: 0 → 3 (delta: +3)
  GridPlayer/MoveCount: 0 → 5 (delta: +5)
VERDICT: CONFIRMED (score increased after movement)
```

If no EVIDENCE block → CLAIM is REJECTED.

## Tools

| Tool | Use For | NOT For |
|------|---------|---------|
| `query_state` | State values (hp, cargo, money) | Visual layout |
| `test_step` | Move + before/after state diff | Standalone movement |
| `move_to` | Move to position + wait for arrival | Field checks (use test_step) |
| `invoke_method` | Call public method via reflection | Property reads (use query_state) |
| `screenshot` | Object positions, UI visibility | Gameplay state verification |
| `get_console` | Error detection | Success verification |
| `run_playtest` | Scripted multi-step sequences | One-off reads |
| `run_playtest(path=...)` | Run a saved `.playtest` file (regression) | One-off scripts |
| `verify_after_change` | **CORE** — 5-gate one-call verification: compile→refs→console→scene→screenshot | Individual gate checks |
| `runtime_snapshot` | Full object state snapshot (RUNTIME category; NOT Play Mode gated) | Individual field reads (use `query_state`) |
| `get_test_results` | Poll NUnit results after run_tests | Play Mode assertions (use run_playtest) |
| `get_test_progress` | Live NUnit progress while run_tests runs | Play Mode assertions (use run_playtest) |
| `watch` / `get_watches` | Continuous field monitoring in Play Mode | One-shot reads |
| `debug_animator` | Runtime Animator state (layers, params) | Edit Mode inspection |
| `debug_physics` | Runtime Rigidbody, contacts, nearby | Edit Mode inspection |

## Before/After Pattern

```python
query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
# → Score=0, PosX=0

# ... action happens ...

query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
# → Score=3 (collected), PosX=5 (moved)
```

## run_playtest DSL

```
run_playtest script="
INVOKE /GridPlayer GridPlayer MoveTo 3,3
WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == False TIMEOUT 5
ASSERT /GridPlayer|GridPlayer|Score >= 1
ASSERT_CONSOLE_CLEAN
"
```

`run_playtest(script=None, path=None, timeout=300, abort_on_fail=False, defs=None, snapshot_on_failure=False)` — `path`: project-relative `.playtest` file; `script`: inline DSL text (one is required). `defs`: inline `VAL` lines prepended to `script`. `snapshot_on_failure`: save screenshot on ASSERT failure (v0.81.4). Use `defs` to pass session-level aliases without editing the script body:

```
run_playtest(
    defs="score /GridPlayer|GridPlayer|Score",
    script="ASSERT $score >= 1\nASSERT_CONSOLE_CLEAN"
)
```

Note: `run_playtest` requires Play Mode (runtime-only tool). Use `editor(action='play')` before calling it. There are no PLAY/STOP DSL commands — the tool manages Play Mode state externally.

`run_playtest(path="Playtests/farm_pipeline_early.playtest")` is the persistent form — loads a project-relative `.playtest` file from disk and runs it. Use for regression testing across sessions.

For continuous monitoring during Play Mode, combine `watch` (field polling every Nms with conditions) with `query_state` (one-shot snapshot). Use `debug_animator(path)` / `debug_physics(path)` for runtime component diagnostics.

### CAPTURE_FRAMES Visual Pattern (v0.90)

For verifying that visual change occurred (animation, particles, movement) — not gameplay state:

```
run_playtest script="
CAPTURE_FRAMES 10
# ... trigger action ...
ASSERT_FRAMES_DIFFER   # visual change happened (animation, movement, particles)
# OR
ASSERT_FRAMES_STATIC   # nothing changed (idle state check)
"
```

`ASSERT_FRAMES_DIFFER` — confirms visual change occurred. `ASSERT_FRAMES_STATIC` — confirms scene is visually stable. Data still wins over frames for gameplay state: only use CAPTURE_FRAMES for visual-change confirmation, not for item transfer, currency, or production state.

### Aliases: VAL / VAR / INCLUDE

`ALIAS` is deprecated — use `VAL $name /path|Comp|field` instead (`VAL $name literal` for a constant). `$sigil` triggers expansion at parse time; unknown sigils are left intact (no throw) but surface as a parse warning.

- `VAL $name /path|Comp|field` — expands to the path string at parse time
- `VAL $name literal` — expands to the literal value
- `VAR $name @/path|Comp|field` — resolves the LIVE value on every step (runtime alias, not parse-time)
- `INCLUDE path/to/file.defs` — imports VAL/VAR/MACRO definitions from an external file (max include depth 5)

Full reference: `.claude/skills/playtest-dsl.md`.

## See Also

- `.claude/skills/testing-tdd.md` — TDD workflow, pytest/NUnit patterns, live integration tests
- `.claude/skills/playtest-dsl.md` — full run_playtest DSL command reference
- `.claude/skills/unity-testing.md` — run_tests, verification workflows, pre-build checklist
