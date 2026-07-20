# Unity Testing & Verification (MCP)

Тестирование и верификация через MCP-тулы.

## Running Tests

**PREFERRED: `run_tests_wait` (v0.81.4, TIER1) — blocks until done, no manual polling.**

```
# Preferred — blocking (TIER1)
run_tests_wait mode="EditMode"
run_tests_wait mode="PlayMode"

# With filter (pipe-separated)
run_tests_wait mode="EditMode" filter="TestClass.TestMethod"
run_tests_wait mode="EditMode" filter="TestA|TestB|TestC"

# Count tests in project (TIER2 TESTS)
get_test_count
```

**Legacy (manual poll) — fallback only:**

```
run_tests mode="EditMode"       # returns immediately: "tests-started|EditMode|poll..."
get_test_progress               # optional live NUnit progress (TIER2) → "running|12|10|2|0|50|4.3|eta=8s"
get_test_results                # → "pending" | "none" | results — repeat until not pending
```

- `get_test_results` swallows exceptions, safe to poll during domain reload.
- Results persist across domain reloads — saved to `~/.unity-mcp/test-results/port-{port}.txt`.
- `run_tests` auto-calls `diagnose` before running. Returns `BLOCKED: <verdict>` on `FAIL:`/`WEDGE`.

**Standalone runner (no MCP):** `python run_unity_tests.py [EditMode|PlayMode] [--filter=TestClass1|TestClass2]`

## Verification Workflows

### После создания объекта
```
create_object name="NewEnemy" parent="/Enemies" primitive="Capsule" components="Rigidbody"
find_objects name="NewEnemy"
get_component path="/Enemies/NewEnemy" type="Rigidbody"
screenshot
```

### После настройки физики
```
set_property path="/Ball" component="Rigidbody" prop="mass" value="0.5"
get_component path="/Ball" type="Rigidbody"
editor action="play" → screenshot → editor action="stop"
```

### После перекомпиляции
```
compile_preflight              # быстрый Roslyn check ~200ms (TIER1)
recompile                      # триггерит реимпорт (async)
await_compile timeout=60       # блокирует до конца
run_tests_wait mode="EditMode"
```

**Low-level:** `get_console level="Error"` → `editor action="state"`

## Play Mode Testing

### Полный flow
```
diagnose
get_console level="Error"
editor action="play"
get_console count=20
screenshot
query_state queries="/Player|Transform|position,/Player|Rigidbody|velocity"
editor action="stop"
get_console level="Error" count=20
```

### Scripted Testing

```
# run_playtest requires Play Mode
editor action="play"

# Разовый DSL скрипт
run_playtest script="MOVE TO 5,0,-3 ..."

# Загрузить DSL из файла
run_playtest path="Playtests/farm_pipeline_early.playtest"

# Набор файлов (direct_only — не в batch)
run_playtest_suite paths="Playtests/*.playtest" timeout_per_test=120 stop_on_fail=false

# Lint без выполнения
lint_playtest script="ASSERT /Player|activeSelf"

# Lint набора файлов (direct_only — не в batch)
lint_playtest_suite paths="Playtests/*.playtest"
```

### Что проверять в Play Mode
- Нет ли NullReferenceException в консоли
- Объекты на ожидаемых позициях
- Физика работает корректно
- UI отображается правильно

## Pre-Build Checklist

```
# 1. Быстрый Roslyn check (~200ms)
compile_preflight

# 2. Полная компиляция
await_compile timeout=60

# 3. Compile state OK
diagnose

# 4. EditMode тесты
run_tests_wait mode="EditMode"

# 5. PlayMode тесты
run_tests_wait mode="PlayMode"

# 6. Консоль чистая
get_console level="Error" count=50

# 7. Визуальная проверка
screenshot width=1920 height=1080

# 8. Сохранить сцену
scene action="save"
```

## Batch Verification

```
batch commands="
get_console level=\"Error\" count=20
editor action=\"state\"
get_hierarchy depth=1
screenshot
"
```
