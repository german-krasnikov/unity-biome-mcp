# Runtime & Playtest Tools

Play Mode runtime operations: reflection-based method/field mutation, state queries, movement, and structured playtest DSL execution.

**Guard:** All tools in this doc require Play Mode active. Tools reject outside Play Mode.

## invoke_method(path, component, method, args="")

**Purpose:** Call public method on component via reflection at runtime.

**Args:** Comma-separated values matching method parameters (parsed as string, int, float, bool, Vector3, etc.).

**Errors:**
- Method not found → component error
- Argument count/type mismatch → reflection error
- Outside Play Mode → rejected by guard

**Example:**
```python
await invoke_method("/Player", "PlayerController", "MoveTo", "10,0,5")
await invoke_method("/UI/HealthBar", "Slider", "SetValue", "0.5")
```

**RW Annotation:** Mutating (increments operation count).

## set_runtime_property(path, component, field, value)

**Purpose:** Set field/property via reflection (no SerializedObject). Safe; read-back verification required.

**Value Format:** Plain text parsed as string, int, float, bool, or GameObject path (field type determines coercion).

**Idempotent:** Calling twice with same value is safe; no side effects.

**Verification:** Use get_component to read back and confirm value set.

**Example:**
```python
await set_runtime_property("/Player", "PlayerController", "Health", "50")
# Verify:
await get_component("/Player", "PlayerController", query="Health")  # → Health: 50
```

**RW_IDEM Annotation:** Idempotent write; safe to retry.

## wait_until(path, component, field, value, timeout=5.0, negate=False, abort_on_fail=False)

**Purpose:** Poll field until it matches value or timeout (Play Mode only).

**Timeout Semantics:**
- Python timeout = Unity timeout + 5s buffer (prevents Python hanging if Unity lags)
- Returns after Unity completes or timeout

**Negate:** If True, waits for field ≠ value.

**abort_on_fail:** If True, Unity stops Play Mode on timeout instead of returning an error.

**Errors:** Timeout → "timeout waiting for X" message.

**Example:**
```python
await wait_until("/Projectile", "Projectile", "Arrived", "true", timeout=3.0)
await wait_until("/Enemy", "AI", "IsDead", "true", timeout=10.0, abort_on_fail=True)
```

**RW_IDEM Annotation:** Idempotent; safe to call multiple times.

## move_to(path, position, timeout=15.0)

**Purpose:** High-level movement: command character to position, wait for arrival, detect blockage.

**Position Format:** "x,y,z" (e.g., "5,0,-3").

**Returns:**
- "arrived" → destination reached
- "blocked" → obstacle prevented arrival

**Timeout:** 15s default; increase for long distances or slow characters.

**Example:**
```python
await move_to("/Player", "10,0,5", timeout=20.0)
# → "arrived" or "blocked"
```

**RW Annotation:** Mutating (modifies object position/state).

## query_state(queries)

**Purpose:** Snapshot multiple game values in one call (efficient batch).

**Query Format:** Comma-separated 'path|component|field_or_method' triplets.

**$alias expansion (v0.78.11):** `$alias` sigils in the queries string are expanded by `AliasExpander.ExpandJson` on the C# side before execution. ValPath aliases now resolve to the full `path|component|field` pipe string (fix in v0.78.11 — before that, aliases with component+field set would expand to path-only). Aliases are auto-warmed from `PlaytestConfig` assets on connect.

**Returns:** Structured text with one line per query (field value or method return).

**Example:**
```python
await query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
# → Score: 150
#   PosX: 5.2

# With $alias (requires PlaytestConfig with alias "score" → /GridPlayer|GridPlayer|Score):
await query_state("$score,$posX")
# → Score: 150
#   PosX: 5.2
```

**RO Annotation:** Read-only; no mutation.

## test_step(path, position, checks_before="", checks_after="", wait_after=0.5, timeout=15.0)

**Purpose:** Atomic test: move character, snapshot state before/after, check console for errors.

**Checks Format:** Comma-separated 'path|component|field' triplets (same as query_state).

**Flow:**
1. BEFORE: snapshot checks_before fields
2. MOVE: move_to(path, position)
3. WAIT: sleep wait_after seconds
4. AFTER: snapshot checks_after fields
5. CONSOLE: grep for errors/warnings

**Returns:** Structured report:
```
[BEFORE] Score: 100
[MOVE] arrived
[AFTER] Score: 105
[CONSOLE] clean
```

**RW Annotation:** Mutating (movement + snapshots).

## run_playtest(script=None, timeout=120.0, abort_on_fail=False, defs=None, path=None)

**Purpose:** Execute playtest DSL script (fire-and-forget fire-and-poll pattern).

**Mutually exclusive:** `path` XOR `script` — exactly one must be provided.

**path:** Assets-relative path to a `.playtest` file on disk (e.g. `"Playtests/farm.playtest"`). C# reads and executes the file directly — costs ~15 tokens instead of 300–800 for an inline script. Use when the script lives in the project's `Playtests/` folder. Internally sends `_explicit_path=true` to bypass middleware path resolution.

**script:** Inline DSL text. Use for ad-hoc or generated scripts.

**abort_on_fail:** If True, stops Play Mode when any WAIT_UNTIL times out. Equivalent to placing `ABORT_ON_FAIL` as first line of the script.

**defs:** Optional inline VAL definitions. Works with both `script` and `path`. Format: one `VAL $name path|comp|field` line per entry. Note: `PlaytestConfig.aliases` are auto-injected by `PlaytestRunner` (v0.78.9) — no need to pass them via `defs`.

```python
aliases = await get_aliases()   # → "$hp=/Player|Health|hp\n$pos=..."
await run_playtest(script, defs=aliases)
# or from file:
await run_playtest(path="Playtests/smoke.playtest")
```

**DSL Directives (processed before steps):**
- `VAL $name value` — parse-time static substitution; `$name` sigil replaced in all following lines
- `VAR $name @path|Comp|field` — runtime-resolved sigil; value read from Unity at each step that uses it
- `INCLUDE filename` — inline-expand `Assets/PlaytestDefs/filename` at parse time (max depth 5)
- `ALIAS name value` — deprecated (whole-word substitution, no sigil); use `VAL` instead
- `MACRO name $p1 ... END_MACRO` / `CALL name arg1 ...` — reusable command blocks (Phase 0)
- `ABORT_ON_FAIL` — global directive: stop Play Mode when any WAIT_UNTIL times out

**DSL Commands:**
- `MOVE [path] TO x,y,z` — move character
- `MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...]` — multi-waypoint, expands to N MOVE steps
- `WAIT n` — sleep n seconds
- `WAIT_UNTIL q op v [AND|OR q op v...] [TIMEOUT n] [ABORT]` — poll until condition; AND/OR compound; ABORT stops Play Mode on timeout
- `ASSERT query op value [AS "label"]` — fail if condition false; AS adds inline report label
- `ASSERT_CONSOLE_CLEAN [IGNORE "pat1","pat2"]` — verify no console errors (ignore patterns)
- `ASSERT_BATCH...END` — multi-line assert block
- `ASSERT_NEAR pathA pathB dist` — spatial proximity check
- `ASSERT_CONSERVED SUM a+b OVER t` — physics invariant (e.g., energy conservation)
- `ASSERT_CTA VISIBLE|CLICKABLE` — check UI reachability
- `ASSERT_CAPTURED label INCREASED|DECREASED` — verify delta
- `SECTION "title"` — group header always shown in report
- `DESC "text"` — label next step in report (no step emitted)
- `SNAPSHOT queries` — capture state (comma-separated paths|component|field)
- `INVOKE path comp method args` — call method
- `SET path comp field value` — set field
- `LOG msg` — log message
- `TIMESCALE n` — set Time.timeScale
- `TELEPORT path x,y,z` — instant move (no movement logic)
- `CAPTURE label query` — save value for later comparison
- `INVARIANT query op value` — always-true check (not per-step)
- `SIMULATE name [DURATION n] [TIMESCALE n]` — run named scenario
- `MONITOR name` — observe state continuously
- `TRACE_FLOW FROM a TO b FIELD f` — path tracing

**Queries:** Use aliases from PlaytestConfig.asset or pipe format.

**Compression:** Long reports (>300 chars) summarized by Haiku: line 1 = result (X/Y), line 2+ = failures only.

**Timeout:** 120s default; increase for complex scenarios.

**Returns:** Compressed report (failures only) or full report if short.

**Examples:**
```python
# inline script
script = """MOVE TO 5,0,0
WAIT 1
ASSERT /Player|PlayerController|Health < 100
ASSERT_CONSOLE_CLEAN"""
await run_playtest(script, timeout=30.0)

# file path — ~15 tokens vs 300-800 inline
await run_playtest(path="Playtests/farm.playtest", timeout=60.0)

# file path + runtime defs
await run_playtest(path="Playtests/smoke.playtest", defs=aliases)
```

**RW Annotation:** Mutating (movement, state changes, assertions).

**Timeout buffers (internal constants):**
- `wait_until`/`move_to`: `_TCP_POLL_BUFFER = 5.0` added to Unity timeout
- `test_step`: `_TCP_STEP_BUFFER = 10.0` added
- `run_playtest`: `_TCP_PLAYTEST_BUFFER = 20.0` added

**Notes:**
- Fire-and-forget: run_playtest sends script, returns immediately with status
- Polling: Caller must poll get_test_results every 5s for up to 2min (see CLAUDE.md § run_tests)
- Domain reload: Transparently reconnects mid-script if compilation detected

## Common Patterns

| Pattern | Tool | Why |
|---------|------|-----|
| Set field once | set_runtime_property | Direct reflection; no polling |
| Wait for event | wait_until | Avoids sleep; true blocking |
| Multi-field snapshot | query_state | Batch; one TCP call instead of N |
| Move + validate state | test_step | Atomic before/after with console check |
| Ad-hoc script | run_playtest(script=...) | DSL readable; compression saves tokens |
| Saved script (token-efficient) | run_playtest(path="Playtests/x.playtest") | ~15 tokens; C# reads file directly |
| Multi-phase fail-fast | run_playtest(abort_on_fail=True) | Stop Play Mode immediately on timeout |
| Compound wait | WAIT_UNTIL … AND/OR … | Single poll for multi-condition gate |

## Errors & Recovery

| Error | Cause | Fix |
|-------|-------|-----|
| "not in Play Mode" | Tool called outside Play | Start play session first (scene_view + scene.play) |
| "timeout waiting for X" | wait_until deadline exceeded | Increase timeout; check game logic |
| "blocked" | move_to collision | Choose different destination or clear obstacles |
| "[Haiku timeout]" | Playtest report too long | Simplify assertions or split into sub-tests |
| "reflection error: method not found" | Typo in method name | Verify via get_component inspect |

## Verification Gates

After each operation:
1. set_runtime_property → get_component (confirm value written)
2. move_to → get_spatial_context (confirm position)
3. invoke_method → query_state (confirm side effect)
4. run_playtest → grep report for failures (no hallucinations)

---

**Related:** `AI/batch.md` (batch DSL), `.claude/skills/playmode-verification.md` (validation patterns), `CLAUDE.md` § verification-gates.
