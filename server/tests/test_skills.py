"""Tests for Persistent Skill Library (Feature 3)."""
import json
import os
import pytest
from unity_mcp.tools.skills import apply_template, list_skills, save_skill, save_template, use_skill


@pytest.fixture
def skills_dir(tmp_path, monkeypatch):
    """Use tmp dir for skills to avoid polluting real .claude/skills/learned/."""
    d = tmp_path / "learned"
    monkeypatch.setattr("unity_mcp.tools.skills._skills_dir", lambda: str(d))
    return d


async def test_save_skill_creates_file(skills_dir):
    result = await save_skill("test_skill", "Does something", "var go = new GameObject();")
    assert result == "Skill saved: test_skill — Does something"
    path = skills_dir / "test_skill.json"
    assert path.exists()
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["name"] == "test_skill"
    assert data["description"] == "Does something"
    assert data["code"] == "var go = new GameObject();"
    assert data["used_count"] == 0
    # Compact contract: skill file must be a single line (no embedded newlines in JSON)
    content = (skills_dir / "test_skill.json").read_text(encoding="utf-8")
    assert content.count('\n') == 0, "skill JSON must be compact (no indent=2)"


async def test_use_skill_executes_code(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "executed"}
    await save_skill("my_skill", "Creates obj", "var go = new GameObject();")
    result = await use_skill("my_skill")
    # C# code → execute_code
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "execute_code"
    assert "var go = new GameObject();" in call_args[1]["code"]


async def test_use_skill_batch_detection(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "batch done"}
    await save_skill("batch_skill", "Moves obj", "set_property path=Cube pos=1,2,3")
    result = await use_skill("batch_skill")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "batch"


async def test_use_skill_not_found_lists_available(skills_dir):
    result = await use_skill("nonexistent")
    # use_skill delegates to list_skills when name not found — always returns "No skills..."
    assert "No skills" in result, result


async def test_list_skills_empty(skills_dir):
    result = await list_skills()
    assert result == "No skills saved yet. Use save_skill to create one."


async def test_list_skills_with_saved(skills_dir):
    await save_skill("alpha", "Alpha skill", "var x = 1;")
    await save_skill("beta", "Beta skill", "create_object name=Test")
    result = await list_skills()
    assert "alpha" in result
    assert "Alpha skill" in result
    assert "beta" in result
    assert "0x" in result  # used_count


async def test_use_skill_increments_count(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await save_skill("countable", "Test count", "var x = 1;")
    await use_skill("countable")
    await use_skill("countable")
    path = skills_dir / "countable.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["used_count"] == 2


async def test_use_skill_param_substitution(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await save_skill("spawn", "Spawns obj", "var go = new GameObject(\"${name}\");")
    await use_skill("spawn", params="name=Player")
    call_args = mock_bridge.send.call_args[0]
    assert "Player" in call_args[1]["code"]
    assert "${name}" not in call_args[1]["code"]


async def test_save_skill_stores_kind_csharp(skills_dir):
    await save_skill("cs_skill", "Does C#", "UnityEditor.AssetDatabase.Refresh();")
    data = json.loads((skills_dir / "cs_skill.json").read_text(encoding="utf-8"))
    assert data["kind"] == "csharp"


async def test_save_skill_stores_kind_batch(skills_dir):
    await save_skill("batch_skill2", "Does batch", "set_property path=Cube pos=1,2,3")
    data = json.loads((skills_dir / "batch_skill2.json").read_text(encoding="utf-8"))
    assert data["kind"] == "batch"


async def test_use_skill_routes_by_stored_kind_not_heuristic(skills_dir, mock_bridge):
    """C# skill with NO typical C# keywords must still route to execute_code via stored kind."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    # Code has no var/new/GameObject// — heuristic would misroute to batch
    skill_data = {
        "name": "tricky", "description": "tricky", "kind": "csharp",
        "code": "UnityEditor.AssetDatabase.Refresh();",
        "created": "2026-01-01 00:00", "used_count": 0,
    }
    skills_dir.mkdir(exist_ok=True)
    (skills_dir / "tricky.json").write_text(json.dumps(skill_data), encoding="utf-8")
    await use_skill("tricky")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "execute_code"


async def test_list_skills_includes_kind(skills_dir):
    await save_skill("tagged", "Tagged skill", "var x = 1;")
    result = await list_skills()
    assert "csharp" in result


async def test_use_skill_vector_param(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await save_skill("spawn", "desc", "var go = new GameObject(\"${name}\"); go.transform.position = ${pos};")
    await use_skill("spawn", params="pos=(0,5,0),name=Enemy")
    code = mock_bridge.send.call_args[0][1]["code"]
    assert "(0,5,0)" in code
    assert "Enemy" in code


async def test_skills_file_operations_use_async_threading(monkeypatch, tmp_path):
    """skill file reads/writes must not run on the main async event loop."""
    import unity_mcp.tools.skills as skills_mod

    calls: list[str] = []

    async def fake_to_thread(func, *args, **kwargs):
        calls.append(func.__name__)
        return func(*args, **kwargs)

    monkeypatch.setattr(skills_mod.asyncio, "to_thread", fake_to_thread)
    monkeypatch.setattr(skills_mod, "_skills_dir", lambda: str(tmp_path))

    orig_send, orig_args = skills_mod._send, skills_mod._args
    async def fake_send(cmd, args=None):
        return {"ok": True, "data": "ok"}

    skills_mod._send = fake_send
    skills_mod._args = lambda **kwargs: kwargs
    try:
        await save_skill("threaded", "description", "set_property a=b")
        await use_skill("threaded")
        await list_skills()
    finally:
        skills_mod._send = orig_send
        skills_mod._args = orig_args

    assert "_read_json" in calls
    assert "_write_json" in calls


async def test_template_operations_use_async_threading(monkeypatch, tmp_path):
    """template file reads/writes must use asyncio.to_thread too."""
    import unity_mcp.tools.skills as skills_mod

    orig_getcwd = skills_mod.os.getcwd
    orig_to_thread = skills_mod.asyncio.to_thread
    calls: list[str] = []

    async def fake_to_thread(func, *args, **kwargs):
        calls.append(func.__name__)
        return func(*args, **kwargs)

    monkeypatch.setattr(skills_mod.os, "getcwd", lambda: str(tmp_path))
    monkeypatch.setattr(skills_mod.asyncio, "to_thread", fake_to_thread)

    template_dir = tmp_path / ".claude" / "templates"
    template_dir.mkdir(parents=True)
    template_path = template_dir / "hello.cs"
    template_path.write_text("Debug.Log(\"x\");", encoding="utf-8")

    orig_send, orig_args = skills_mod._send, skills_mod._args
    sent: list[tuple] = []

    async def fake_send(cmd, args=None):
        sent.append((cmd, args))
        return "ok"

    skills_mod._send = fake_send
    skills_mod._args = lambda **kwargs: kwargs
    try:
        await apply_template("hello")
        await save_template("greet", "Debug.Log(\"y\");")
    finally:
        skills_mod._send = orig_send
        skills_mod._args = orig_args
        skills_mod.os.getcwd = orig_getcwd
        skills_mod.asyncio.to_thread = orig_to_thread

    assert "_read_text" in calls
    assert "_write_text" in calls
    assert sent and sent[0][0] == "execute_code"
