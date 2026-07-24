---
name: unity-code-intel
description: C# code intelligence via Unity Biome MCP — execute_code (TIER1), compile_preflight (TIER1), await_compile (TIER1), get_compile_errors (CORE), auto_fix/get_schema/smart_build (TIER2 SYSTEM), serialized_field_rename_audit (TIER2 VERIFY).
user-invocable: false
---

# Code Intelligence Tools

## Tiers

| Tool | Tier | Gating |
|------|------|--------|
| `execute_code` | TIER1 SYSTEM | Always visible |
| `compile_preflight` | TIER1 VERIFY | Always visible |
| `await_compile` | TIER1 VERIFY | Always visible |
| `get_compile_errors` | CORE | Always visible |
| `auto_fix` | TIER2 SYSTEM | `discover_tools category="SYSTEM"` |
| `get_schema` | TIER2 SYSTEM | `discover_tools category="SYSTEM"` |
| `smart_build` | TIER2 SYSTEM | `discover_tools category="SYSTEM"` |
| `serialized_field_rename_audit` | TIER2 VERIFY | `discover_tools category="VERIFY"` |

## execute_code

Запустить произвольный C# в Editor-контексте. Timeout 60s.

**Security gating (v0.89):** `SecurityLevel` enum — `AllowAll` (default) / `Standard` / `Strict`. `AllowAll` skips all scans — all C# APIs available. `Standard` blocks unsafe namespaces + reflection accessors. `Strict` additionally blocks `GetField`/`GetProperty`/`GetFields`/`GetProperties`. Если операция отклонена — переключи уровень в MCPSettings.

```
execute_code code="Selection.activeGameObject.name"
execute_code code="""
var go = GameObject.Find("Player");
Debug.Log(go.GetComponent<Health>().currentHp);
"""
execute_code code="AssetDatabase.Refresh()"
```

## compile_preflight

Проверить C# код на ошибки ДО записи на диск. ~200ms, не триггерит Unity recompile.

```
compile_preflight file_path="Assets/Scripts/Services/CurrencyService.cs" new_content="<full file content>"
```

**Response:** `OK preflight ... (Nms)` при успехе, `ERR preflight ...` + diagnostics при ошибках.

Принимает **полный файл**, не сниппет.

**UnityPreflightHints (v0.92):** помимо ошибок компилятора, `compile_preflight` теперь выдаёт дополнительные предупреждения:
- Serialized `Dictionary<>` поля (Unity 6+ требует `[SerializeField]` + `SerializableDictionary`)
- Поля с типом интерфейса / абстрактного класса — не сериализуются
- `[FormerlySerializedAs]` без соответствующего нового поля — устаревший атрибут-мусор

## await_compile

Блокирует до завершения компиляции + domain reload, возвращает ошибки если есть.

```
await_compile                   # default timeout 60s
await_compile timeout=30.0
await_compile timeout=0         # immediate check, no loop
```

**Response:** `compile clean (Xs)` | список ошибок | `still compiling` | `timeout after Xs`

## get_compile_errors

CORE-тул. Возвращает текущие ошибки компиляции Unity. Не ждёт — snapshot текущего состояния.

```
get_compile_errors
```

## serialized_field_rename_audit (TIER2 VERIFY)

**Триггер:** после любого переименования C# поля без `[FormerlySerializedAs]`.

Сканирует префабы, сцены и ScriptableObjects на наличие устаревшего имени поля в YAML-данных. Использует regex по файловой системе (не `SerializedObject.FindProperty` — он падает после ренейма). Возвращает список ассетов с риском потери данных.

```
discover_tools category="VERIFY"
serialized_field_rename_audit old_field_name="oldName" type_name="MyComponent"
```

**Workflow после rename:**
```
# 1. Переименовали поле CurrencyAmount → Amount
# 2. Аудит — найти все ассеты со старым именем
serialized_field_rename_audit old_field_name="CurrencyAmount" type_name="CurrencyService"
# 3. Если есть хиты → добавить [FormerlySerializedAs("CurrencyAmount")] и пересохранить ассеты
# 4. Повторный аудит — должно вернуть 0 хитов
```

## auto_fix (TIER2)

AI-assisted анализ и предложение фиксов для ошибок компиляции. Read-only.

```
discover_tools category="SYSTEM"
auto_fix file_path="Assets/Scripts/Player.cs"
```

## get_schema (TIER2)

Reflection-info о типе: поля, методы, базовые классы.

```
discover_tools category="SYSTEM"
get_schema type="UnityEngine.Rigidbody"
get_schema type="PlayerController"
```

## smart_build (TIER2)

Build проекта с оптимизациями.

```
discover_tools category="SYSTEM"
smart_build target="Android"
```

---

## Workflows

### Безопасная запись C#

```
# 1. Прочитать файл
# 2. Подготовить изменения
# 3. Проверить ДО записи
compile_preflight file_path="Assets/Scripts/X.cs" new_content="<full content>"
# 4. Если OK → Write файл
# 5. Дождаться компиляции
await_compile
# 6. Если ошибки → исправить и повторить
```

### Проверить статус

```
await_compile timeout=0   # → "still compiling" или результат
get_compile_errors         # snapshot ошибок
```

### Выполнить C# быстро

```
execute_code code="Debug.Log(Application.dataPath)"
```

---

## Anti-Patterns

| Bad | Good |
|-----|------|
| Write C# → sleep → get_compile_errors | compile_preflight → Write → await_compile |
| `sleep(30)` после записи .cs | `await_compile` |
| Ручной poll get_compile_errors | `await_compile timeout=60` |
| execute_code для валидации кода | `compile_preflight` (не меняет состояние) |
| Переименовать поле → надеяться что данные целы | `serialized_field_rename_audit` → `[FormerlySerializedAs]` → повторный аудит |
