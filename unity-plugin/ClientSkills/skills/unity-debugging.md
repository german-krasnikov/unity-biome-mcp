# Unity Debugging & Diagnosis (MCP)

Диагностика проблем через MCP: compile health, console monitoring, runtime debug, scene audit.

## Tool Tiers

**TIER1 (always visible, no Play Mode):** `console_mark`, `get_console_since`, `get_console`, `get_compile_errors`, `compile_preflight`, `await_compile`

**TIER2 RUNTIME (gated):** `debug`\*, `debug_animator`†, `debug_physics`†, `get_frame_stats`†, `get_memory`, `get_watches`, `invoke_method`†, `profile`†, `runtime_snapshot`, `watch`\*, `snapshot`\*, `get_metrics`\*

\* = `direct_only` — cannot use inside `batch`
† = `runtime_only` — requires Play Mode

**TIER2 VERIFY:** `diagnose`, `scene_health`

## Diagnosis Workflow

```
1. debug path="/Enemy_01"    — runtime: inspector dump (direct_only)
2. scene_health focus="all"  — иерархия: битые ссылки, дубли?
3. diagnose                  — компиляция: домен живой? stale dll? wedge?
```

## [Play Mode] Runtime Debug

**Gated:** `discover_tools category="RUNTIME"` first. `debug` is `direct_only`.

```
debug path="/Enemy_01"           # inspector-like dump (direct_only)
debug_animator path="/Character" # Animator: layers, transitions, parameters (runtime_only)
debug_physics path="/Ball" radius=5.0  # Rigidbody, colliders, contacts, nearby (runtime_only)
runtime_snapshot path="/Enemy_01"  # snapshot of runtime object state at current frame
```

## Scene Health Audit

**Gated:** `discover_tools category="VERIFY"` first.

```
scene_health focus="all"          # полный аудит
scene_health focus="missing"      # отсутствующие ссылки
scene_health focus="duplicates"   # дубли имён
scene_health focus="origins"      # далёкие от нуля позиции
scene_health focus="disabled"     # выключенные объекты
```

Возвращает `CRITICAL` / `WARNING` / `INFO` / `OK`.

## Compile/Reload Diagnosis

**Gated:** `discover_tools category="VERIFY"` first.

```
# Standalone проверка — домен живой?
diagnose

# После sync — MVID изменился?
diagnose prev_mvid="abc123" expected_compile=true

# Без ожидания компиляции (cache-hit)
diagnose prev_mvid="abc123" expected_compile=false
```

**Вердикты:** `CLEAN-LIVE` | `FAIL:<CS>` | `FAIL:stale-dll` | `STALE-DOMAIN` | `STALE-TRANSIENT` | `WEDGE-ENGINE` | `WEDGE-STATE` | `BUILD-FAILED-WEDGE` | `STALE-CACHE` | `TESTS-INVISIBLE` | `REBUILDING` | `NO-OP` | `UNKNOWN`

## Post-Compile Check (Modern)

После изменения C# скриптов:
```
recompile                                # триггерит реимпорт (async, возвращает сразу)
await_compile timeout=60                 # блокирует до конца компиляции, возвращает ошибки или "compile clean (Xs)"
```

**Low-level (без await_compile):** `get_console level="Error"` → `editor action="state"`

**After multi-step mutations:** `verify_after_change` — one-call additive verification: compile waits/errors always run; console, NUnit, and playtest gates run when `mark_id`, `run_tests_mode`, or `playtests` are provided. Use instead of hand-rolling those gates.

## Reading Console

```
get_console                              # последние 10
get_console count=50                     # больше записей
get_console level="Error"                # только ошибки
get_console level="Warning"              # предупреждения
get_console keyword="NullRef"            # фильтр по подстроке
get_console count_only=true              # только количество
get_console since=30                     # за последние 30 секунд
get_compile_errors                       # ошибки компиляции (structured, file:line:col)
```

**Filtering pre-existing noise (v0.93):**
```
console_mark label="before_X"              # → "mark:<timestamp>:<label>" token
# ... do operation ...
get_console_since mark="mark:<ts>:<label>" # full token; also accepts bare timestamp or "ts:label"
```

## Screenshots & Editor Control

```
screenshot                               # 640x480
screenshot width=1920 height=1080        # кастомный размер
screenshot camera="scene_view"           # scene view (default)
screenshot camera="scene_view_frame"     # scene view, frame on path
screenshot camera="multi_view"           # 4 views (front/side/top/perspective)
screenshot camera="single_view" angle="front"  # front|left|top|iso|ex,ey,ez
screenshot camera="overview"             # bird's eye scene
screenshot camera="overview_game"        # game camera overview
screenshot describe="what's wrong"       # Haiku description (15-100x fewer tokens)
screenshot highlight="/Obj1,/Obj2:#FF0000"  # bbox overlay on objects
screenshot show_colliders=true           # wireframe collider overlay
screenshot zoom=2.0                      # zoom (higher=closer)
screenshot supersample=2                 # supersampling 1-4
screenshot annotation_id="ann_01"        # frame annotation by id
editor action="state"                    # isPlaying, isPaused
editor action="play" / "pause" / "stop"  # Play Mode control
editor action="select" path="/MyObject"  # выбрать в Inspector
ping_object path="/MyObject"             # подсветить + выбрать в Hierarchy
undo_last turns=1                        # отменить N AI-мутаций
get_capabilities                         # Unity version, pipeline, packages
```

## [Play Mode] Watch System

**Gated:** `discover_tools category="RUNTIME"` first. `watch` is `direct_only`.

```
# Poll property every 500ms
watch path="/Player" component="Health" property="currentHp"

# Log/pause on condition
watch path="/Player" component="Health" property="currentHp" condition="< 10" trigger_action="log"
watch path="/Player" component="Health" property="currentHp" condition="== 0" trigger_action="pause"

# Custom interval
watch path="/Score" component="Currency" property="Value" interval_ms=200

# Read all watches + logs
get_watches
```

## Common Error Patterns

| Error | Fix |
|-------|-----|
| `NullReferenceException` | `debug path="/Obj"` (direct_only) |
| `MissingComponentException` | `scene_health focus="missing"` |
| Compile errors | `await_compile` → `diagnose` |
| Runtime perf drop | `get_frame_stats` → `profile action="analyze"` |
| Test run stuck/slow | `get_test_progress` (see `unity-testing.md`) |
