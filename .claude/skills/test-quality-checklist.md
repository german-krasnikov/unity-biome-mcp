---
name: test-quality-checklist
description: "Quality checklist for new tests — naming, logs, DRY, isolation, file size."
user-invocable: false
globs:
  - "server/tests/**/*.py"
  - "unity-plugin/Editor/Tests/**/*.cs"
---

# Test Quality Checklist

Supplements `testing-tdd.md`. No duplication — naming conventions and tautology rules live there.

## 1. Universal Rules

| Rule | Limit |
|------|-------|
| File size | soft 200 lines, hard 400 |
| SUTs per file | 1 (class/module under test) |
| Name matches assert | if name says `Returns_X`, assert checks X |
| Tautology check | revert production code → test must go RED |
| Sprint/audit codes | BANNED in class and method names (F02, CS3, _Audit, _Fix) |

## 2. Python-Specific

```python
# WRONG — asyncio_mode=auto makes this decorator redundant noise
@pytest.mark.asyncio
async def test_foo(): ...

# RIGHT
async def test_foo(): ...
```

```python
# WRONG — stdout leaks into test output, breaks CI parsing
def test_bar():
    print("debug")  # forbidden
    assert result == expected

# RIGHT — use caplog
def test_bar(caplog):
    with caplog.at_level(logging.DEBUG):
        result = fn()
    assert "expected" in caplog.text
```

```python
# Crash-guard test (no business assert, intent is "must not raise"):
def test_serialize_empty_scene_no_crash():
    """Regression: empty scene raised KeyError before fix."""  # docstring required
    HierarchySerializer.serialize(depth=1)  # no-assert: crash guard
```

```python
# DRY: 3+ similar tests → parametrize
@pytest.mark.parametrize("cmd", ["get_hierarchy", "get_console", "get_metrics"])
async def test_tool_requires_connection(cmd, disconnected_bridge):
    result = await call_tool(cmd, {})
    assert "not connected" in result.lower()
```

Fixtures in `conftest.py` or `tests/helpers.py`. Never define a fixture inside a test file (unless it's file-scoped and used by 3+ tests in that file).

## 3. C#-Specific

### Scene Cleanup

`new GameObject()` dirties the scene via the Undo system. `DestroyImmediate()` removes the object but does NOT clear the Undo dirty flag. Only `SceneTestBase` (which calls `NewScene + Undo.ClearAll`) properly resets scene state.

**CRITICAL RULE:** Any C# test class that creates a `GameObject` MUST inherit `SceneTestBase`. No exceptions. `DestroyImmediate` alone is INSUFFICIENT — the Undo stack keeps the scene dirty, causing "Save modified scenes?" popups.

```csharp
// REQUIRED — inherit SceneTestBase (NewScene + Undo.ClearAll in TearDown)
public class MyTests : SceneTestBase
{
    [Test]
    public void Foo() { new GameObject("Tmp"); } // scene reset in TearDown
}
```

```csharp
// WRONG — DestroyImmediate does NOT clear Undo dirty flag
[TearDown]
public void TearDown()
{
    Object.DestroyImmediate(_go); // object gone, scene STILL dirty!
}

// WRONG — NewScene WITHOUT Undo.ClearAll (scene stays dirty in Unity 6)
[TearDown]
public void TearDown()
{
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    // Missing Undo.ClearAll() — dirty flag persists!
}

// RIGHT — if not using SceneTestBase, BOTH are required
[TearDown]
public void TearDown()
{
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    Undo.ClearAll(); // MANDATORY in Unity 6
}
```

```csharp
// WRONG — accessing destroyed object
[TearDown]
public void TearDown()
{
    Object.DestroyImmediate(_go);
    Debug.Log($"Cleaned {_go.name}");  // MissingReferenceException!
}

// RIGHT — capture name BEFORE destroy
[TearDown]
public void TearDown()
{
    var name = _go ? _go.name : "(null)";
    Object.DestroyImmediate(_go);
    // use `name` if needed
}
```

**NEVER** rely on session-scoped `_cleanup_orphans` — it is a last-resort safety net for live tests, not a substitute for per-test cleanup.

### Property Mutation Cleanup (Python live tests)

Any live test that mutates a property on an existing scene object (e.g. `set_property`, `set_runtime_property`) MUST revert it in teardown/finally. `_orphan_guard` only watches root object counts — it is blind to property mutations.

```python
# WRONG — mutates MoveSpeed, never reverts
async def test_set_speed(bridge):
    await bridge.send("set_runtime_property", {"path": "/Player", "component": "Movement", "field": "MoveSpeed", "value": "50"})
    # ... assertions ...
    # MoveSpeed stays at 50 for all subsequent tests!

# RIGHT — revert in finally or fixture
@pytest.fixture(autouse=True)
async def reset_player(bridge):
    yield
    await bridge.send("set_runtime_property", {"path": "/Player", "component": "Movement", "field": "MoveSpeed", "value": "5"})
```

### Logging

```csharp
// WRONG — Debug.Log creates noise in NUnit output, triggers LogAssert failure
[Test]
public void Foo_Bar_Baz()
{
    Debug.Log("testing...");  // FORBIDDEN
    Assert.IsTrue(result);
}

// RIGHT
[Test]
public void Foo_Bar_Baz()
{
    TestContext.WriteLine("testing...");  // allowed
    Assert.IsTrue(result);
}
```

```csharp
// WRONG — reflection on public API
var method = typeof(ChipPill).GetMethod("BuildChip", BindingFlags.NonPublic | BindingFlags.Instance);

// RIGHT — add [assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")] and mark internal
internal ChipPill BuildChip(...) { ... }
```

DRY helpers available in test assembly:
- `ChipTestHelpers.H(text)` / `.S(text)` — create chip strings
- `TestStringHelpers.CountOccurrences(str, sub)` — count substrings
- `ChipTestBase` — base class with registry reset boilerplate

## 4. Log Rules

### Python — FORBIDDEN in tests
```python
print(...)              # no
logging.basicConfig(...)  # no
sys.stdout.write(...)   # no
```
ALLOWED: `caplog`, `pytest.warns()`, `warnings.warn()` inside a `with pytest.warns(...)` block.

### C# — FORBIDDEN without matching assertion
```csharp
Debug.Log(...)       // no — unless immediately followed by LogAssert.Expect
Debug.LogWarning(...)  // no
Debug.LogError(...)    // no
```
ALLOWED:
```csharp
LogAssert.Expect(LogType.Warning, "exact message");
Debug.LogWarning("exact message");  // now paired
TestContext.WriteLine("anything");  // always ok
```

## 5. PR Checklist

Before merging any test PR or new test file, verify each item:

- [ ] No sprint/audit codes in class or method names
- [ ] File ≤ 200 lines (or justified if 200–400)
- [ ] One SUT per file
- [ ] Method name matches the actual assertion
- [ ] Tautology check done (reverted production code → RED)
- [ ] No `print()` / `Debug.Log()` without pair
- [ ] No `@pytest.mark.asyncio` decorator
- [ ] C# test with `new GameObject()` inherits `SceneTestBase` (MANDATORY — `DestroyImmediate` alone leaves Undo dirty)
- [ ] C# test with manual `NewScene()` also calls `Undo.ClearAll()` (Unity 6 requirement)
- [ ] Python live test that mutates properties reverts them in teardown/finally
- [ ] Fixtures defined in conftest.py, not inline in test file
- [ ] Crash-guard tests have docstring + `# no-assert: crash guard`
- [ ] 3+ identical tests collapsed into `@pytest.mark.parametrize` / `[TestCase]`
- [ ] Reflection replaced with `InternalsVisibleTo` where possible
- [ ] Temp files in `Assets/TestsTemp/` only (C#)
- [ ] `[TestFixture]` present on every C# test class

## Anti-patterns (AVOID)

| Instead of | Do this | Why |
|------------|---------|-----|
| Class name like `F02Tests` or `CS5AuditTests` | Name after the SUT, e.g. `CommandRouterTests` | Sprint/audit codes hide the subject under test |
| Test method named `Test1` | `Method_Scenario_Expected` | Meaningless names let bugs hide |
| Assertion that passes if the fix is reverted | Rewrite to verify real behavior | Tautological tests provide false confidence |
| Inline `print()` / `Debug.Log()` | Use `caplog` / `TestContext.WriteLine` | Pollutes CI output and breaks log assertions |
| Inline fixture definitions | Put fixtures in `conftest.py` or `tests/helpers.py` | Reusability and isolation |
| Inline `DestroyImmediate` in TearDown | Accumulate objects and batch destroy | Survives exceptions, avoids "Save Scene?" modal |
| Temp files in `Assets/` root | `TestPaths.TempFolder + "/file"` | Prevents cross-test pollution |

## Key Files

| File | Role |
|------|------|
| `server/tests/conftest.py` | shared fixtures |
| `unity-plugin/Editor/Tests/TestPaths.cs` | temp paths helper |
| `.claude/skills/testing-tdd.md` | naming rules |

## See Also

- .claude/skills/testing-tdd.md
- .claude/skills/csharp-unity.md
- .claude/skills/python-mcp.md

## Sources

- pytest docs: https://docs.pytest.org/
- NUnit docs: https://docs.nunit.org/
