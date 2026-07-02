"""TDD tests for do_intent package: catalog, validator, planner, executor, do tool."""
from unittest.mock import AsyncMock, patch, MagicMock

import pytest
from mcp.server.fastmcp.exceptions import ToolError


# ---------------------------------------------------------------------------
# 1. Catalog — allowed commands
# ---------------------------------------------------------------------------

def test_catalog_allowed_commands():
    from unity_mcp.do_intent.catalog import ALLOWED
    assert "create_object" in ALLOWED
    assert "set_property" in ALLOWED
    assert "manage_component" in ALLOWED
    assert "set_active" in ALLOWED
    assert "set_material" in ALLOWED
    assert "wire_event" in ALLOWED
    # forbidden not in catalog
    assert "delete_object" not in ALLOWED
    assert "execute_code" not in ALLOWED


def test_catalog_required_keys():
    from unity_mcp.do_intent.catalog import ALLOWED
    assert "name" in ALLOWED["create_object"]["required"]
    assert "path" in ALLOWED["set_property"]["required"]
    assert "component" in ALLOWED["set_property"]["required"]


def test_catalog_glossary_returns_string():
    from unity_mcp.do_intent.catalog import build_glossary
    g = build_glossary()
    assert "create_object" in g
    assert "set_property" in g


# ---------------------------------------------------------------------------
# 2. Validator — rejects forbidden command
# ---------------------------------------------------------------------------

def test_validator_rejects_forbidden_cmd():
    from unity_mcp.do_intent.validator import validate_plan
    plan = "delete_object path=/Cube"
    err = validate_plan(plan, scene_paths=set())
    assert err is not None
    assert "forbidden" in err.lower() and "delete_object" in err, err


def test_validator_rejects_execute_code():
    from unity_mcp.do_intent.validator import validate_plan
    plan = "execute_code code=Debug.Log(1)"
    err = validate_plan(plan, scene_paths=set())
    assert err is not None


# ---------------------------------------------------------------------------
# 3. Validator — missing required key
# ---------------------------------------------------------------------------

def test_validator_rejects_missing_required_key():
    from unity_mcp.do_intent.validator import validate_plan
    # set_property requires path, component, prop, value
    plan = "set_property path=/Cube component=Transform prop=m_LocalPosition"
    err = validate_plan(plan, scene_paths={"/Cube"})
    assert err is not None
    assert "value" in err.lower() and "missing" in err.lower(), err


# ---------------------------------------------------------------------------
# 4. Validator — path not in scene
# ---------------------------------------------------------------------------

def test_validator_rejects_path_not_in_scene():
    from unity_mcp.do_intent.validator import validate_plan
    plan = "set_property path=/Ghost component=Transform prop=m_LocalPosition value=(0,0,0)"
    err = validate_plan(plan, scene_paths={"/Cube"})
    assert err is not None
    assert "Ghost" in err and "path" in err.lower(), err


# ---------------------------------------------------------------------------
# 5. Validator — path created earlier in plan is valid
# ---------------------------------------------------------------------------

def test_validator_accepts_path_created_above_in_plan():
    from unity_mcp.do_intent.validator import validate_plan
    plan = (
        "create_object name=NewCube\n"
        "set_property path=/NewCube component=Transform prop=m_LocalPosition value=(1,0,0)"
    )
    err = validate_plan(plan, scene_paths=set())
    assert err is None


def test_validator_accepts_nested_create_with_parent():
    """Nested object: parent stored without leading slash, but referenced with /."""
    from unity_mcp.do_intent.validator import validate_plan
    plan = (
        "create_object name=Parent\n"
        "create_object name=Child parent=Parent\n"
        "set_active path=/Parent/Child active=true"
    )
    err = validate_plan(plan, scene_paths=set())
    assert err is None


# ---------------------------------------------------------------------------
# 6. Validator — REJECT prefix propagates
# ---------------------------------------------------------------------------

def test_validator_REJECT_prefix_propagates():
    from unity_mcp.do_intent.validator import validate_plan
    plan = "REJECT: intent is ambiguous"
    err = validate_plan(plan, scene_paths=set())
    assert err is not None
    assert "ambiguous" in err.lower() and "REJECT" in err, err


# ---------------------------------------------------------------------------
# 7. Validator — max 50 lines
# ---------------------------------------------------------------------------

def test_validator_max_50_lines():
    from unity_mcp.do_intent.validator import validate_plan
    lines = ["create_object name=Obj" + str(i) for i in range(51)]
    err = validate_plan("\n".join(lines), scene_paths=set())
    assert err is not None
    assert "50" in err and "limit" in err.lower(), err


def test_validator_exactly_50_lines_ok():
    from unity_mcp.do_intent.validator import validate_plan
    lines = ["create_object name=Obj" + str(i) for i in range(50)]
    err = validate_plan("\n".join(lines), scene_paths=set())
    assert err is None


# ---------------------------------------------------------------------------
# 8. Planner — builds prompt with intent and glossary
# ---------------------------------------------------------------------------

async def test_planner_builds_prompt_with_intent_and_glossary():
    from unity_mcp.do_intent.planner import Planner

    captured = {}

    async def fake_generate(prompt, **kw):
        captured["prompt"] = prompt
        return "create_object name=Cube"

    svc = MagicMock()
    svc.generate = fake_generate

    planner = Planner(svc)
    result = await planner.plan("add a red cube", scene_brief="Root\n  Cube")
    assert "add a red cube" in captured["prompt"]
    assert "create_object" in captured["prompt"]
    assert result == "create_object name=Cube"


async def test_planner_returns_none_when_service_disabled():
    from unity_mcp.do_intent.planner import Planner

    svc = MagicMock()
    svc.generate = AsyncMock(return_value=None)

    planner = Planner(svc)
    result = await planner.plan("add a red cube", scene_brief="")
    assert result is None


# ---------------------------------------------------------------------------
# 9. Executor — no failure returns summary
# ---------------------------------------------------------------------------

async def test_executor_no_failure_returns_summary():
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="ok: 3 ops")
    ex = Executor(send)
    result = await ex.execute("create_object name=Cube\nset_active path=/Cube active=true")
    assert "ok" in result or "2" in result or "ops" in result
    send.assert_called_once()


# ---------------------------------------------------------------------------
# 10. Executor — partial failure retries once
# ---------------------------------------------------------------------------

async def test_executor_partial_failure_retries_once():
    from unity_mcp.do_intent.executor import Executor

    # First call returns partial failure, second returns success
    call_count = 0

    async def fake_send(cmd, args):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return "[1] ok: created\n[2] err: set_active path=/Ghost active=true: path not found"
        return "ok: 1 ops"

    svc = MagicMock()

    async def fake_generate(prompt, **kw):
        return "set_active path=/Ghost active=true"

    svc.generate = fake_generate

    ex = Executor(fake_send, sampling=svc)
    result = await ex.execute(
        "create_object name=Cube\nset_active path=/Ghost active=true",
        original_intent="add cube and activate ghost",
        scene_paths={"/Ghost"},
    )
    assert call_count == 2
    assert result == "ok: 1 ops"


# ---------------------------------------------------------------------------
# 11. do dry_run returns plan without execution
# ---------------------------------------------------------------------------

async def test_do_dry_run_returns_plan_no_execution():
    from unity_mcp.do_intent.executor import Executor
    from unity_mcp.do_intent.planner import Planner

    send = AsyncMock()
    svc = MagicMock()
    svc.generate = AsyncMock(return_value="create_object name=Cube")

    planner = Planner(svc)
    ex = Executor(send, sampling=svc)

    plan = await planner.plan("add cube", scene_brief="")
    # dry_run: validate but don't execute
    from unity_mcp.do_intent.validator import validate_plan
    err = validate_plan(plan, scene_paths=set())
    assert err is None
    # executor NOT called
    send.assert_not_called()


# ---------------------------------------------------------------------------
# 12. do e2e happy path (mock everything)
# ---------------------------------------------------------------------------

async def test_do_e2e_happy_path():
    from unity_mcp.tools.do_tool import do

    with (
        patch("unity_mcp.tools.do_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.do_tool._sampling") as mock_svc,
        patch("unity_mcp.tools.do_tool._get_scene_brief", new_callable=AsyncMock) as mock_brief,
    ):
        mock_brief.return_value = "Root\n  Cube"
        mock_svc.generate = AsyncMock(return_value="create_object name=Cube")
        mock_send.return_value = "ok: 1 ops"

        result = await do("add a cube")
        assert "ok" in result.lower() or "1" in result


async def test_do_dry_run_e2e():
    from unity_mcp.tools.do_tool import do

    with (
        patch("unity_mcp.tools.do_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.do_tool._sampling") as mock_svc,
        patch("unity_mcp.tools.do_tool._get_scene_brief", new_callable=AsyncMock) as mock_brief,
    ):
        mock_brief.return_value = ""
        mock_svc.generate = AsyncMock(return_value="create_object name=Cube")

        result = await do("add a cube", dry_run=True)
        mock_send.assert_not_called()
        assert "create_object" in result or "dry" in result.lower() or "plan" in result.lower()


# ---------------------------------------------------------------------------
# 12a. _extract_paths unit test (PY5.test.5)
# ---------------------------------------------------------------------------

def test_extract_paths_from_hierarchy():
    from unity_mcp.tools.do_tool import _extract_paths
    hierarchy = "/Root\n  /Root/Child\n  !/Root/Inactive"
    result = _extract_paths(hierarchy)
    assert result == {"/Root", "/Root/Child", "/Root/Inactive"}


# ---------------------------------------------------------------------------
# 12c. do() blocks forbidden plan (X5.cross.1)
# ---------------------------------------------------------------------------

async def test_do_e2e_invalid_plan_blocked():
    from unity_mcp.tools.do_tool import do

    with (
        patch("unity_mcp.tools.do_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.do_tool._sampling") as mock_svc,
        patch("unity_mcp.tools.do_tool._get_scene_brief", new_callable=AsyncMock) as mock_brief,
    ):
        mock_brief.return_value = ""
        mock_svc.generate = AsyncMock(return_value="delete_object path=/X")

        with pytest.raises(ToolError, match="INVALID PLAN"):
            await do("delete a cube")
        mock_send.assert_not_called()


# ---------------------------------------------------------------------------
# 12b. do() returns error when Haiku unavailable (PY5.test.2)
# ---------------------------------------------------------------------------

async def test_do_returns_error_when_haiku_unavailable():
    from unity_mcp.tools.do_tool import do

    with (
        patch("unity_mcp.tools.do_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.do_tool._sampling") as mock_svc,
        patch("unity_mcp.tools.do_tool._get_scene_brief", new_callable=AsyncMock) as mock_brief,
    ):
        mock_brief.return_value = ""
        mock_svc.generate = AsyncMock(return_value=None)

        with pytest.raises(ToolError, match="Haiku planning unavailable"):
            await do("add a cube")
        mock_send.assert_not_called()


# ---------------------------------------------------------------------------
# 13a. Executor — retry accepts path created in first pass (PY5.arch.2)
# ---------------------------------------------------------------------------

async def test_executor_retry_accepts_path_created_in_first_pass():
    """Retry plan targeting a path created in original plan must not be rejected by validator."""
    from unity_mcp.do_intent.executor import Executor

    call_count = 0

    async def fake_send(cmd, args):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return "[1] ok: created\n[2] err: set_property path=/NewCube component=Transform prop=m_LocalPosition value=(1,0,0): not found"
        return "ok: 1 ops"

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="set_property path=/NewCube component=Transform prop=m_LocalPosition value=(1,0,0)")

    original_plan = "create_object name=NewCube\nset_property path=/NewCube component=Transform prop=m_LocalPosition value=(1,0,0)"
    ex = Executor(fake_send, sampling=svc)
    result = await ex.execute(original_plan, original_intent="create cube and move it", scene_paths=set())

    # send must be called twice: original + retry (not rejected by validator)
    assert call_count == 2, f"Expected 2 send calls, got {call_count}"
    assert result == "ok: 1 ops"


# ---------------------------------------------------------------------------
# 13. Executor — no retry when >5 failures (P1-7)
# ---------------------------------------------------------------------------

async def test_executor_no_retry_above_5_failures():
    """When >5 lines fail, executor returns raw result without retrying."""
    from unity_mcp.do_intent.executor import Executor

    # 6 error lines → above the ≤5 threshold
    raw_result = "\n".join(
        f"[{i}] err: set_property path=/Obj{i} component=T prop=x value=1: not found"
        for i in range(1, 7)
    )
    send = AsyncMock(return_value=raw_result)

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="create_object name=Cube")  # would be called on retry

    ex = Executor(send, sampling=svc)
    result = await ex.execute("some_plan")

    # send called exactly once — no retry
    send.assert_called_once()
    # result equals raw (no retry result substituted)
    assert result == raw_result
    # sampling service never called
    svc.generate.assert_not_called()


# ── Fix 11+12: do_intent executor retry logic ─────────────────────────────────

def test_is_partial_detects_err_format():
    """Fix 12: _is_partial must detect '[N] err:' batch format."""
    from unity_mcp.do_intent.executor import _is_partial
    assert _is_partial("[1] err: path not found")


def test_is_partial_detects_partial_colon():
    """Fix 12: _is_partial also detects legacy 'PARTIAL:' prefix."""
    from unity_mcp.do_intent.executor import _is_partial
    assert _is_partial("PARTIAL: 1/2 ok")


def test_is_partial_false_for_all_ok():
    """Fix 12: _is_partial returns False when all ops succeed."""
    from unity_mcp.do_intent.executor import _is_partial
    assert not _is_partial("[1] ok: created")
    assert not _is_partial("ok: 3 ops")


def test_count_failures_matches_actual_format():
    """Fix 12: _count_failures must parse '[N] err: ...' lines."""
    from unity_mcp.do_intent.executor import _count_failures
    result = "[1] ok: created\n[2] err: path not found\n[3] err: missing component"
    failed = _count_failures(result)
    assert len(failed) == 2
    assert any("path not found" in f for f in failed)


async def test_executor_applies_validate_plan_to_retry(monkeypatch):
    """Fix 11: validate_plan is applied to the retry commands before executing them."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.do_intent.executor import Executor

    call_args = []

    async def fake_send(cmd, args):
        call_args.append(args.get("commands", ""))
        if len(call_args) == 1:
            return "[1] err: path not found"
        return "[1] ok: created"

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="create_object name=Cube")

    ex = Executor(fake_send, sampling=svc)
    result = await ex.execute(
        "create_object name=Cube",
        original_intent="add cube",
        scene_paths=set(),
    )
    assert result is not None
    assert len(call_args) == 2
