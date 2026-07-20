---
name: testing-tdd
description: TDD workflow for this project — Red-Green-Refactor cycle, pytest patterns (async, fixtures, parametrize, mocking), Unity NUnit patterns (EditMode, PlayMode, reflection), live integration tests against a running Unity instance, and test naming conventions. Load when writing or debugging tests in Python or C#, setting up fixtures, adding live integration tests, or following TDD discipline for new features and bugfixes.
user-invocable: false
---

# TDD: pytest + Unity NUnit

## Red-Green-Refactor

```
1. RED: Write failing test first → run → MUST FAIL
2. GREEN: Minimal code to pass → run → MUST PASS
3. REFACTOR: Clean up → run ALL → MUST STAY GREEN
```

## pytest (Python)

```python
def test_send_command_returns_response():
    bridge = MockBridge(response={"ok": True})
    assert bridge.send("ping", {})["ok"] is True

@pytest.mark.asyncio
async def test_connect_to_server():
    client = Bridge()
    await client.connect()
    assert client.is_connected

@pytest.fixture
def mock_service():
    service = Mock()
    service.process = AsyncMock(return_value="ok")
    return service

@pytest.mark.parametrize("cmd,expected", [
    ("get_hierarchy", True),
    ("invalid_cmd", False),
])
def test_command_validation(cmd, expected):
    assert is_valid_command(cmd) == expected
```

Config (`pyproject.toml`): `testpaths = ["tests"]`, `asyncio_mode = "auto"`, `python_files = "test_*.py"`.

## Unity NUnit

### EditMode (pure logic, fast)

```csharp
[TestFixture]
public class SerializerTests
{
    [Test]
    public void Serialize_EmptyScene_ReturnsEmptyString()
    {
        var result = HierarchySerializer.Serialize(depth: 1);
        Assert.That(result, Is.Empty);
    }

    [TestCase(1, "Main Camera")]
    [TestCase(2, "├─")]
    public void Serialize_WithDepth_FormatsCorrectly(int depth, string expected)
    {
        Assert.That(HierarchySerializer.Serialize(depth), Does.Contain(expected));
    }
}
```

### PlayMode (requires game loop)

```csharp
[UnityTest]
public IEnumerator Server_AcceptsConnection()
{
    var server = new GameObject().AddComponent<MCPServerComponent>();
    yield return new WaitForSeconds(0.1f);
    Assert.IsNotNull(server);
}
```

## Mocking Patterns

### Python

```python
from unittest.mock import Mock, AsyncMock

async def test_tool_returns_text():
    call_fn = AsyncMock(return_value="result text")
    assert "result" in await my_tool("param")

def test_pure_function_error():
    result = analyze_build("/nonexistent/path")
    assert "error" in result.lower()
```

### C#

```csharp
// Interface-based (preferred)
public interface INetworkClient { Task<string> SendAsync(string msg); }
public class MockNetworkClient : INetworkClient { ... }

// Reflection — for static classes (restore state in TearDown, fragile on renames)
var field = typeof(MyClass).GetField("_state", BindingFlags.Static | BindingFlags.NonPublic);
field.SetValue(null, testValue);
```

## Test Naming

```
test_<method>_<scenario>_<expected>     # Python
Test_<Method>_<Scenario>_<Expected>    # C#
```

## Live MCP Verification

Use `run_playtest` DSL for scripted gameplay verification (requires Unity running + MCP connected).

```
run_playtest script="
VAL $money /Money|Currency|Value
TIMESCALE 3
CAPTURE money $money
MOVE TO 5,0,-3
WAIT 2
ASSERT_CAPTURED money INCREASED
ASSERT_CONSOLE_CLEAN
"

run_playtest path="Playtests/my_test.playtest"

# Shared aliases across calls — use defs param
run_playtest(defs="money /Money|Currency|Value", script="CAPTURE money $money\nASSERT_CAPTURED money INCREASED")
```

`VAL $name path|comp|field` defines an alias expanded at parse time. `ALIAS` keyword is deprecated — use `VAL`.

## Key Rules

- `run_tests_wait mode="EditMode"` — **preferred** over manual poll loop (`run_tests` + `get_test_results` loop is legacy)
- `WAIT_UNTIL` instead of `WAIT` for state-dependent tests
- `ASSERT_CONSOLE_CLEAN` at end of every test sequence
- `get_test_progress` — poll live NUnit progress while `run_tests` is running (TIER2 TESTS)
- See `playmode-verification.md` for CLAIM/EVIDENCE/VERDICT format
- See `playtest-dsl.md` for full DSL command reference

## See Also

- `.claude/skills/playmode-verification.md` — CLAIM/EVIDENCE/VERDICT format, anti-hallucination rules
- `.claude/skills/playtest-dsl.md` — run_playtest DSL command reference
- `.claude/skills/csharp-unity.md` — Editor API patterns, domain reload safety
