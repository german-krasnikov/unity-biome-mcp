# Runtime & Playtest Tools

Play Mode runtime operations: reflection-based method/field mutation, state queries, movement, and structured playtest DSL execution.

**Guard:** Runtime mutation, movement, and playtest execution require Play Mode. Static tools such as `lint_playtest`, `lint_playtest_suite`, `validate_playtest_aliases`, `resolve_scene_refs`, and `lint_scene_refs` do not.

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
await get_component("/Player", "PlayerController", fields="Health")  # → Health: 50
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

## run_playtest(script=None, timeout=120.0, abort_on_fail=False, defs=None, path=None, snapshot_on_failure=False, fresh=False)

**Purpose:** Execute a playtest DSL script and wait for its final report.

**Mutually exclusive:** `path` XOR `script` — exactly one must be provided.

**path:** Assets-relative or project-root-relative path to a `.playtest` file on
disk (for example, `"Playtests/smoke.playtest"`). C# reads and executes the file
directly, avoiding an inline DSL payload. Internally sends `_explicit_path=true`
to bypass middleware path resolution.

**script:** Inline DSL text. Use for ad-hoc or generated scripts.

**abort_on_fail:** If True, stops Play Mode when any WAIT_UNTIL times out. Equivalent to placing `ABORT_ON_FAIL` as first line of the script.

**defs:** Optional inline VAL definitions. Works with both `script` and `path`. Format: one `VAL $name path|comp|field` line per entry. Note: `PlaytestConfig.aliases` are auto-injected by `PlaytestRunner` (v0.78.9) — no need to pass them via `defs`.

**snapshot_on_failure:** If True, appends current `$sigil` values and recent console errors to each FAIL/ERR line in the report. Costs extra reads per failure but makes root-cause obvious without a follow-up `query_state`.

**fresh:** If True, reload the active scene before the first playtest step.

```python
defs = "VAL $hp /Player|Health|hp\nVAL $pos /Player|Transform|position"
await run_playtest(script, defs=defs)
# or from file:
await run_playtest(path="Playtests/smoke.playtest")
```

**DSL Directives (processed before steps):**
- `VAL $name value` — parse-time static substitution; `$name` sigil replaced in all following lines
- `VAR $name @path|Comp|field` — runtime-resolved sigil; value read from Unity at each step that uses it
- `INCLUDE filename` — inline-expand `Assets/PlaytestDefs/filename` at parse time (max depth 5)
- `ALIAS name value` — **removed (v0.92.x)**: parser no longer handles ALIAS; linter emits ERROR. Use `VAL $name value` instead.
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
- `ASSERT_ONE_ACTIVE pathA pathB [pathC...]` — exactly one listed object must be active
- `ASSERT_CONSERVED SUM a+b OVER t` — physics invariant (e.g., energy conservation)
- `ASSERT_CTA VISIBLE|CLICKABLE` — check UI reachability
- `ASSERT_CAPTURED label INCREASED|DECREASED` — verify delta
- `ASSERT_CHANGED label` — verify the raw value stored by `CAPTURE` changed
- `CAPTURE_FRAMES n INTERVAL s [CAMERA name] [MODE strip|list] [LABEL name]` — timed frame sequence
- `ASSERT_FRAMES_DIFFER label` / `ASSERT_FRAMES_STATIC label` — verify motion or stability in a captured frame set
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
- `TRACE_FLOW FROM a TO b FIELD f` — path tracing (parsed but not executed)
- `SWEEP_PATH <path> DWELL <n>\n  x,y,z > ...\n[UNTIL q op v TIMEOUT n]` — multi-waypoint with dwell + optional stop condition
- `WAIT_CAPTURED <label> INCREASED|DECREASED|UNCHANGED|INCREASED_BY|DECREASED_BY [subOp val] [TIMEOUT n] [OVER n]` — poll captured delta
- `ASSERT $sigil` / `ASSERT !$sigil` / `ASSERT ($a,$b)` — bool sugar; expands to Assert/AssertBatch
- `COMPLETE_PURCHASE <path> EXPECT\n  query...\nTIMEOUT n` — invoke CompletePurchase + compound WaitUntil
- `INVOKE_REPEAT <count> <path> <comp> <method> [args]\n[EXPECT q op v TIMEOUT n]` — N identical invokes + optional WaitUntil

**Queries:** Use aliases from PlaytestConfig.asset or pipe format.

**Compression:** Long reports (>300 chars) summarized by Haiku: line 1 = result (X/Y), line 2+ = failures only.

**Timeout:** 120s operation default. Unity's outer `run_playtest` request
deadline is 130s, so values above that cannot extend one request beyond the
outer deadline.

**Returns:** The final report. Long reports may be compressed to the result and failures.

**Examples:**
```python
# inline script
script = """MOVE TO 5,0,0
WAIT 1
ASSERT /Player|PlayerController|Health < 100
ASSERT_CONSOLE_CLEAN"""
await run_playtest(script, timeout=30.0)

# file path — ~15 tokens vs 300-800 inline
await run_playtest(path="Playtests/smoke.playtest", timeout=60.0)

# file path + runtime defs
await run_playtest(
    path="Playtests/smoke.playtest",
    defs="VAL $hp /Player|Health|hp"
)
```

**RW Annotation:** Mutating (movement, state changes, assertions).

**Timeout buffers (internal constants):**
- `wait_until`/`move_to`: `_TCP_POLL_BUFFER = 5.0` added to Unity timeout
- `test_step`: `_TCP_STEP_BUFFER = 10.0` added
- `run_playtest`: `_TCP_PLAYTEST_BUFFER = 20.0` added

**Notes:**
- The call blocks until Unity returns the playtest report or the timeout expires.
- Do not poll `get_test_results`; that polling workflow belongs to `run_tests`, not `run_playtest`.
- Domain reload: Transparently reconnects mid-script if compilation detected

## run_playtest_suite(paths=None, suite_path=None, timeout_per_test=120.0, stop_on_fail=False, stop_after=True, auto_play=False, restart_between=False)

**Purpose:** Run multiple `.playtest` files sequentially; return a compact pass/fail matrix.

**paths:**
- Glob pattern: `"Playtests/*.playtest"` — Unity resolves via `list_playtest_files`
- Comma-separated: `"Playtests/a.playtest,Playtests/b.playtest"`
- Newline-separated list of project-relative paths

**suite_path:** Absolute path to a `.suite` file containing project-relative `.playtest` paths, one per line. Exactly one of `paths` or `suite_path` is required.

**stop_on_fail:** Abort after first failure.

**stop_after:** Exit Play Mode when suite finishes (default True).

**auto_play:** Enter Play Mode before running when needed.

**restart_between:** Stop and re-enter Play Mode between files to reset runtime state.

**Output format:**
```
SUITE: 3/4 passed (45.2s)
OK    10.1s  smoke.playtest  5/5
FAIL  12.3s  combat.playtest  2/5
  [3] ASSERT ... — FAIL (...)
OK     8.9s  ui.playtest  4/4
```

**RW Annotation:** Mutating.

## lint_playtest(path=None, script=None)

**Purpose:** Static preflight check on a `.playtest` file or inline DSL. Read-only — no Play Mode required.

**Checks:**
- `$sigil` unresolved (not defined in VAL/VAR or PlaytestConfig)
- Removed `ALIAS` keyword (emits ERROR — migrate to `VAL`)
- `TRACE_FLOW` (parsed but not executed)
- `CALL` referencing unknown MACRO
- Mixed `AND`/`OR` in `WAIT_UNTIL`
- No evidence commands (ASSERT/WAIT_UNTIL/ASSERT_CONSOLE_CLEAN/ASSERT_BATCH/ASSERT_CAPTURED/ASSERT_CHANGED/ASSERT_ONE_ACTIVE/ASSERT_FRAMES_DIFFER/ASSERT_FRAMES_STATIC)
- Missing `ASSERT_CONSOLE_CLEAN` at end

**path / script:** Mutually exclusive; one required.

**Returns:** `"OK  <file>  no issues"` or severity-tagged lines `ERROR/WARN/INFO  file:line  message`.

**Example:**
```python
await lint_playtest(path="Playtests/smoke.playtest")
# → "OK  Playtests/smoke.playtest  no issues"
await lint_playtest(script="INVOKE /Player PC Heal\nASSERT /Player|PC|Health == 100")
```

**RO Annotation:** Read-only.

## lint_playtest_suite(paths=None, suite_path=None)

**Purpose:** Batch preflight check across multiple `.playtest` files. Read-only — no Play Mode required.

**paths:** Glob pattern (`"Playtests/*.playtest"`) or comma-separated list of project-relative paths.

**suite_path:** Absolute path to a `.suite` file. Provide either `paths` or `suite_path`.

**Returns:** Aggregated report, one block per file. Summary line: `LINT: X/Y OK`.

**Example:**
```python
await lint_playtest_suite("Playtests/*.playtest")
# → LINT: 3/4 OK
#   OK  Playtests/smoke.playtest  no issues
#   ERROR  Playtests/combat.playtest:5  deprecated ALIAS keyword — use VAL instead
```

**RO Annotation:** Read-only.

## validate_playtest_aliases(defs, asset)

**Purpose:** Diff alias `.defs` file vs `PlaytestConfig.asset`. Reports missing/extra/changed entries.

**defs:** Project-relative path to a `.defs` file. Pass it explicitly in agent
workflows.

**asset:** Asset path to `PlaytestConfig`.

**Returns:** `"ok: N aliases in sync"` or a structured diff report with `missing:`, `extra:`, `changed:` sections.

**RO Annotation:** Read-only.

## sync_playtest_aliases_from_defs(defs, asset)

**Purpose:** Overwrite `PlaytestConfig.asset` aliases from a `.defs` text file (bidirectional sync, defs → asset direction).

**Returns:** `"synced: N aliases -> path/to/PlaytestConfig.asset"`

**Effect:** Clears existing aliases, writes parsed `.defs` aliases, calls `SetDirty` + `SaveAssets`, invalidates `AliasExpander` cache.

**RW Annotation:** Mutating (writes Unity asset).

## export_playtest_aliases_to_defs(asset, defs)

**Purpose:** Export `PlaytestConfig.asset` aliases to a `.defs` text file (bidirectional sync, asset → defs direction).

**Returns:** `"exported: N aliases -> path/to/output.defs"`

**Effect:** Writes `FormatVALBlock(aliases)` to the specified file; creates directory if missing; calls `AssetDatabase.Refresh`.

**RW Annotation:** Mutating (writes file).

## resolve_scene_refs(refs, fields=None)

**Purpose:** Resolve `$alias`, `/path`, or `t:Type` tokens to scene paths in one batch call.

**refs:** Comma-separated list of tokens to resolve.

**fields:** Optional comma-separated field names to verify existence on the matched component.

**Returns:** Tab-aligned lines per ref: `OK <path> <detail>`, `MISS <ref>`, or `AMB <ref> <matches>`.

**RO Annotation:** Read-only.

## lint_scene_refs(path=None, snippet=None)

**Purpose:** 3-pass linter for scene references embedded in DSL scripts or batch commands.

**path:** Project-relative path to `.playtest` file.

**snippet:** Inline DSL or batch commands (mutually exclusive with `path`).

**Checks:** Unresolved aliases, embedded alias paths, missing scene objects, ambiguous GameObject names.

**Returns:** `"OK: no issues"` or severity-tagged issues `ERROR`/`WARN` with `file:line:token`.

**RO Annotation:** Read-only.

## run_tests_wait(mode="EditMode", filter="", timeout=180.0, poll_interval=5.0)

**Purpose:** Synchronous NUnit test runner. Fires `run_tests` then polls `get_test_results` internally until done or timeout. Avoids the manual poll loop.

**mode:** `"EditMode"` or `"PlayMode"`.

**filter:** Pipe-separated test class names.

**timeout:** Max wait in seconds (default 180).

**poll_interval:** Seconds between polls (default 5).

**Returns:** Final test result string, `"TIMEOUT: <last>"`, or `"BLOCKED: <reason>"`.

**RW_IDEM Annotation:** Idempotent mutating.

## console_mark(label="")

**Purpose:** Create a timestamp watermark. Pure Python — no TCP call. Returns `mark_id` string encoding `time.time()`.

Pass the returned `mark_id` to `get_console_since()` to retrieve only logs produced after this point.

**Returns:** `"mark:<timestamp>"` or `"mark:<timestamp>:<label>"` if label provided.

**RO Annotation:** Read-only.

## get_console_since(mark_id, level=None, count=500)

**Purpose:** Console entries produced after a watermark created by `console_mark()`.

**mark_id:** String from `console_mark()`.

**level:** Optional filter, e.g. `"error,exception,assert"`.

**count:** Max entries to return (default 500).

**Returns:** Same format as `get_console()` but scoped to the time window after the mark.

**RO Annotation:** Read-only.

## verify_after_change(changed_files="", test_filter="", run_tests_mode="", playtests="", mark_id="", timeout=300.0)

**Purpose:** Single call verification pipeline after any code or scene change. Additive gates — only the ones you enable run.

**Gates (always):**
1. `await_compile` — wait for compilation to finish
2. `get_compile_errors` — confirm zero errors

**Gates (optional):**
3. `get_console_since mark_id` — if `mark_id` provided
4. `run_tests_wait mode filter` — if `run_tests_mode` provided
5. `run_playtest_suite playtests` — if `playtests` provided

**Returns:** `"PASS: gate1 + gate2 + ..."` or `"FAIL: <gate> gate failed\n  <detail>\nnext gates skipped: ..."`.

**RW Annotation:** Mutating (runs tests/playtests may alter state).

## mcp_status()

**Purpose:** Compact snapshot of the current MCP/Unity connection state. One TCP call to `get_status`.

**Returns:** Scene name, dirty flag, play/compile state, port, alias count.

**RO Annotation:** Read-only.

## scene_change_plan(goal, targets="", dry_run=True)

**Purpose:** Pre-flight gate before a batch of scene mutations. Runs compile check, console error check, resolves target refs, takes a checkpoint. Returns a `plan_id` valid for 600s.

**goal:** Human-readable intent string (stored in plan).

**targets:** Comma-separated `$alias`/`/path`/`t:Type` tokens to pre-resolve. Returns `FAIL` immediately if any miss.

**Returns:** `plan_id=<id>\ngoal=...\ncompile=clean\nconsole_errors=N` or `"FAIL: ..."`.

**RW Annotation:** Mutating (takes checkpoint).

## apply_scene_change(plan_id, commands, verify=True, save=True)

**Purpose:** Execute a planned batch of scene mutations with built-in post-verification and optional save.

**plan_id:** String from `scene_change_plan()`. Expires after 600s.

**commands:** JSON batch commands (same format as `batch`).

**verify:** If True, runs `validate_references` + console error check after mutations.

**save:** If True, saves the scene after mutations.

**Returns:** `"mutations=ok (...)\nrefs=ok (0 broken)\nconsole=clean\nsaved=true"` or error detail.

**RW Annotation:** Mutating.

## Common Patterns

| Pattern | Tool | Why |
|---------|------|-----|
| Set field once | set_runtime_property | Direct reflection; no polling |
| Wait for event | wait_until | Avoids sleep; true blocking |
| Multi-field snapshot | query_state | Batch; one TCP call instead of N |
| Move + validate state | test_step | Atomic before/after with console check |
| Ad-hoc script | run_playtest(script=...) | DSL readable; compression saves tokens |
| Saved script | run_playtest(path="Playtests/x.playtest") | C# reads the file directly |
| Single file explicit | run_playtest(path="Playtests/x.playtest") | Same execution path |
| Multi-phase fail-fast | run_playtest(abort_on_fail=True) | Stop Play Mode immediately on timeout |
| Suite of files | run_playtest_suite("Playtests/*.playtest") | One call; compact pass/fail matrix |
| Lint before run | lint_playtest_suite("Playtests/*.playtest") | Catch errors without entering Play Mode |
| Compound wait | WAIT_UNTIL … AND/OR … | Single poll for multi-condition gate |
| Delta wait | WAIT_CAPTURED label DECREASED TIMEOUT 5 | Poll captured baseline vs live value |
| Dwell patrol sweep | SWEEP_PATH /Player DWELL 1.0 | Move+wait at each waypoint |
| Repeat invoke | INVOKE_REPEAT 3 /P PC Heal | N identical calls, one step sequence |
| Preflight check | lint_playtest(path="Playtests/x.playtest") | Catch unresolved $sigil before Play |
| Sync .defs → .asset | sync_playtest_aliases_from_defs(...) | Bidirectional alias sync |
| Failure root cause | run_playtest(path="...", snapshot_on_failure=True) | Inline values + console at each FAIL |
| Log-window slice | console_mark() → ... mutations ... → get_console_since(mark_id) | Only see errors from this change |
| Post-change gate | verify_after_change(run_tests_mode="EditMode") | One call replaces compile+test loop |
| Safe scene edit | scene_change_plan → apply_scene_change | Pre-flight + checkpoint + post-verify |
| Ref preflight | resolve_scene_refs("$player,$enemy") | Confirm all targets exist before batch |
| NUnit sync | run_tests_wait(mode="EditMode", filter="MyTest") | No manual poll loop |

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
