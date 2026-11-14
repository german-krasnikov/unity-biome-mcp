"""TDD tests for ask package: router, executor, summarizer, ask tool."""
import re
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


# ---------------------------------------------------------------------------
# 1. Router — broken refs pattern
# ---------------------------------------------------------------------------

def test_router_broken_refs_pattern():
    from unity_mcp.ask.router import route
    plan = route("show me all broken references in scene")
    assert plan is not None
    assert plan.key == "BROKEN_REFS"


def test_router_broken_refs_pattern_alt():
    from unity_mcp.ask.router import route
    plan = route("are there any broken links?")
    assert plan is not None
    assert plan.key == "BROKEN_REFS"


# ---------------------------------------------------------------------------
# 2. Router — scene health pattern
# ---------------------------------------------------------------------------

def test_router_scene_health_pattern():
    from unity_mcp.ask.router import route
    plan = route("any errors in the scene?")
    assert plan is not None
    assert plan.key == "SCENE_HEALTH"


def test_router_scene_health_console():
    from unity_mcp.ask.router import route
    plan = route("what problems are in the console?")
    assert plan is not None
    assert plan.key == "SCENE_HEALTH"


# ---------------------------------------------------------------------------
# 3. Router — editor state pattern
# ---------------------------------------------------------------------------

def test_router_editor_state_pattern():
    from unity_mcp.ask.router import route
    plan = route("is Unity in play mode?")
    assert plan is not None
    assert plan.key == "EDITOR_STATE"


def test_router_compile_errors_pattern():
    from unity_mcp.ask.router import route
    plan = route("any compilation errors?")
    assert plan is not None
    assert plan.key == "COMPILE_ERRORS"


# ---------------------------------------------------------------------------
# 4. Router — out of scope: no Unity noun
# ---------------------------------------------------------------------------

def test_router_out_of_scope_no_unity_noun():
    from unity_mcp.ask.router import route
    plan = route("what is the weather today?")
    assert plan is None


def test_router_out_of_scope_generic():
    from unity_mcp.ask.router import route
    plan = route("tell me a joke")
    assert plan is None


# ---------------------------------------------------------------------------
# 5. Router — rejects mutating verbs
# ---------------------------------------------------------------------------

def test_router_rejects_mutating_verbs_delete():
    from unity_mcp.ask.router import route
    result = route("delete all objects in scene")
    # Either None (no plan) or raises — implementation can choose
    # But we test that mutating_verb detection works
    from unity_mcp.ask.router import is_mutating
    assert is_mutating("delete all objects in scene")


def test_router_rejects_mutating_verbs_set():
    from unity_mcp.ask.router import is_mutating
    assert is_mutating("set the health to 100")


def test_router_read_only_not_mutating():
    from unity_mcp.ask.router import is_mutating
    assert not is_mutating("what are the broken references?")


# ---------------------------------------------------------------------------
# 6. Executor — runs canonical plan
# ---------------------------------------------------------------------------

async def test_executor_runs_canonical_plan():
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock(return_value="refs: none broken")
    ex = AskExecutor(send)

    plan = ToolPlan([("validate_references", {"path": "/", "depth": "5"})], "broken refs only", "BROKEN_REFS")
    results = await ex.run(plan)
    assert len(results) == 1
    assert results[0] == "refs: none broken"
    send.assert_called_once_with("validate_references", {"path": "/", "depth": "5"})


async def test_executor_runs_multi_tool_plan():
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    responses = ["scan ok", "refs ok", "no errors", "no compile"]
    idx = 0

    async def fake_send(cmd, args):
        nonlocal idx
        r = responses[idx]
        idx += 1
        return r

    ex = AskExecutor(fake_send)
    plan = ToolPlan([
        ("scan_scene", {}),
        ("validate_references", {"path": "/", "depth": "3"}),
        ("get_console", {"count": "10", "level": "Error"}),
        ("get_compile_errors", {}),
    ], "summarize top issues", "SCENE_HEALTH")

    results = await ex.run(plan)
    assert len(results) == 4


# ---------------------------------------------------------------------------
# 7. Summarizer — bypass mode: short single-tool result
# ---------------------------------------------------------------------------

async def test_summarizer_bypass_mode_short_single_tool():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock()  # Should NOT be called

    s = Summarizer(svc)
    raw = ["no broken refs"]  # short, single tool
    result = await s.summarize("broken refs?", raw, hint="broken refs only")
    assert result == "no broken refs"
    svc.generate.assert_not_called()


async def test_summarizer_bypass_mode_long_triggers_haiku():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="3 errors found in scene")

    s = Summarizer(svc)
    # More than 200 chars
    long_raw = ["x" * 250]
    result = await s.summarize("any errors?", long_raw, hint="summarize top issues")
    svc.generate.assert_called_once()
    assert result == "3 errors found in scene"


# ---------------------------------------------------------------------------
# 8. Summarizer — haiku fallback when service disabled
# ---------------------------------------------------------------------------

async def test_summarizer_haiku_fallback_when_none():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value=None)

    s = Summarizer(svc)
    long_raw = ["x" * 250]
    # Falls back to raw data when haiku returns None
    result = await s.summarize("any errors?", long_raw, hint="something")
    assert len(result) == 250  # fallback to raw "x"*250


# ---------------------------------------------------------------------------
# 9. ask e2e — broken refs returns concise
# ---------------------------------------------------------------------------

async def test_ask_e2e_broken_refs_returns_concise():
    from unity_mcp.tools.ask_tool import ask

    with patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "no broken refs"
        result = await ask("are there broken references?")
        assert result is not None
        assert len(result) < 300


# ---------------------------------------------------------------------------
# 10. ask e2e — scene health uses haiku (multi-tool)
# ---------------------------------------------------------------------------

async def test_ask_e2e_scene_health_uses_haiku():
    from unity_mcp.tools.ask_tool import ask

    with (
        patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.ask_tool._sampling") as mock_svc,
    ):
        mock_send.return_value = "x" * 300  # long → triggers haiku
        mock_svc.generate = AsyncMock(return_value="Scene has 2 errors: NullRef in Player.cs")

        result = await ask("what problems are in the scene?")
        assert result == "Scene has 2 errors: NullRef in Player.cs"


# ---------------------------------------------------------------------------
# 11. ask — rejects mutating question
# ---------------------------------------------------------------------------

async def test_ask_rejects_mutating_question():
    from unity_mcp.tools.ask_tool import ask

    result = await ask("delete all the objects please")
    # multiple valid rejection phrasings — all are legitimate responses
    assert re.search(r"read-only|mutating|\bask\b", result, re.IGNORECASE), result


# ---------------------------------------------------------------------------
# 12. ask — no Unity noun returns rejection
# ---------------------------------------------------------------------------

async def test_ask_no_unity_noun_returns_rejection():
    from unity_mcp.tools.ask_tool import ask

    result = await ask("what is the meaning of life?")
    assert result is not None
    # should indicate it can't answer or is out of scope
    assert len(result) < 300


# ---------------------------------------------------------------------------
# 13. Executor corroboration — get_compile_errors result passes through corroborate
# ---------------------------------------------------------------------------

async def test_executor_corroborates_get_compile_errors():
    """AskExecutor.run must pass get_compile_errors result through editor_log.corroborate."""
    import unity_mcp.editor_log as el
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    raw_csharp = "No compilation errors."
    corroborated = "[editor.log - dll stale]\nAssets/Foo.cs:1:1: error CS0117: stale"

    send = AsyncMock(return_value=raw_csharp)

    with patch.object(el, "corroborate", return_value=corroborated) as mock_cor:
        ex = AskExecutor(send)
        plan = ToolPlan([("get_compile_errors", {})], "compile errors", "COMPILE_ERRORS")
        results = await ex.run(plan)

    mock_cor.assert_called_once_with(raw_csharp)
    assert results[0] == corroborated


async def test_executor_does_not_corroborate_other_tools():
    """AskExecutor.run must NOT call corroborate for non-compile tools."""
    import unity_mcp.editor_log as el
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock(return_value="scan ok")

    with patch.object(el, "corroborate") as mock_cor:
        ex = AskExecutor(send)
        plan = ToolPlan([("scan_scene", {})], "scan", "SCAN")
        results = await ex.run(plan)

    mock_cor.assert_not_called()
    assert results[0] == "scan ok"


# ---------------------------------------------------------------------------
# 14. Router — route() returns None for mutating question (P2)
# ---------------------------------------------------------------------------

def test_router_route_returns_none_for_mutating_question():
    """route() must return None when question starts with a mutating verb."""
    from unity_mcp.ask.router import route
    result = route("delete all objects in scene")
    assert result is None


def test_router_route_returns_none_for_set_verb():
    from unity_mcp.ask.router import route
    result = route("set the health to 100 on Player")
    assert result is None


# ---------------------------------------------------------------------------
# 15. Summarizer — multi_result always triggers haiku (P2)
# ---------------------------------------------------------------------------

async def test_summarizer_multi_result_short_total_still_calls_haiku():
    """Two short results totalling <200 chars must still call Haiku (multi_result path)."""
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="summary from haiku")

    s = Summarizer(svc)
    # Two results, each short, combined well under 200 chars
    raw = ["ok: no refs", "ok: no errors"]
    assert sum(len(r) for r in raw) < 200

    result = await s.summarize("any issues?", raw, hint="scene health")
    svc.generate.assert_called_once()
    assert result == "summary from haiku"


# ---------------------------------------------------------------------------
# 16. Router — COUNT_ACTIVE returns None (plan not implemented) (P2)
# ---------------------------------------------------------------------------

def test_router_count_active_falls_through_to_scene_query():
    """COUNT_ACTIVE has None plan → falls through to SCENE_QUERY fallback."""
    from unity_mcp.ask.router import route
    result = route("how many active objects are there?")
    assert result is not None
    assert result.key == "SCENE_QUERY"


# ---------------------------------------------------------------------------
# 17. Summarizer — empty result list goes to Haiku (P2)
# ---------------------------------------------------------------------------

async def test_summarizer_empty_result_list_calls_haiku():
    """Empty raw_results: combined='', len!=1 → Haiku path."""
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="nothing found")

    s = Summarizer(svc)
    result = await s.summarize("any errors?", [], hint="scene health")
    svc.generate.assert_called_once()
    assert result == "nothing found"


async def test_summarizer_empty_result_list_haiku_returns_none_falls_back():
    """Empty list + Haiku returns None → fallback to combined[:500] which is ''."""
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value=None)

    s = Summarizer(svc)
    result = await s.summarize("any errors?", [], hint="hint")
    assert result == ""


# ---------------------------------------------------------------------------
# 18. Summarizer — exception during summarization (P2)
# ---------------------------------------------------------------------------

async def test_summarizer_exception_during_haiku_propagates():
    """If svc.generate raises, exception propagates (no silent swallow in summarizer)."""
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(side_effect=RuntimeError("haiku down"))

    s = Summarizer(svc)
    with pytest.raises(RuntimeError, match="haiku down"):
        await s.summarize("any errors?", ["x" * 250], hint="hint")


# ---------------------------------------------------------------------------
# 21. Summarizer — passes feature='summarize' to generate (PY5.arch.3)
# ---------------------------------------------------------------------------

async def test_summarizer_passes_summarize_feature_to_generate():
    """summarize() must pass feature='summarize' so budget router applies correct gating."""
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="summary")

    s = Summarizer(svc)
    await s.summarize("any errors?", ["x" * 250], hint="hint")
    svc.generate.assert_called_once()
    call_kwargs = svc.generate.call_args.kwargs
    assert call_kwargs.get("feature") == "summarize", f"Expected feature='summarize', got: {call_kwargs}"


# ---------------------------------------------------------------------------
# 19. AskExecutor — empty plan returns empty list (P2)
# ---------------------------------------------------------------------------

async def test_executor_empty_plan_returns_empty_list():
    """ToolPlan with no steps → run() returns []."""
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock()
    ex = AskExecutor(send)
    plan = ToolPlan([], "hint", "EMPTY")
    results = await ex.run(plan)
    assert results == []
    send.assert_not_called()


# ---------------------------------------------------------------------------
# 20. AskExecutor — all steps failing (P2)
# ---------------------------------------------------------------------------

async def test_executor_all_steps_failing_returns_error_strings():
    """All steps raise → each result is 'ERROR: ...' string, no exception bubbles."""
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock(side_effect=ConnectionError("unity offline"))
    ex = AskExecutor(send)
    plan = ToolPlan(
        [("scan_scene", {}), ("validate_references", {"path": "/"})],
        "hint",
        "SCENE_HEALTH",
    )
    results = await ex.run(plan)
    assert len(results) == 2
    assert all(r.startswith("ERROR:") for r in results)


# ---------------------------------------------------------------------------
# 22. Router — SPATIAL_QUERY pattern
# ---------------------------------------------------------------------------

def test_router_spatial_query_positions():
    from unity_mcp.ask.router import route
    plan = route("What are the world positions (x,y,z) of all waypoints?")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


def test_router_spatial_query_where_is():
    from unity_mcp.ask.router import route
    plan = route("where is the Main Camera?")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


def test_router_spatial_query_coordinates():
    from unity_mcp.ask.router import route
    plan = route("show me the coordinates of the spawn point object")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


# ---------------------------------------------------------------------------
# 23. Router — OBJECT_QUERY pattern
# ---------------------------------------------------------------------------

def test_router_object_query_list_objects():
    from unity_mcp.ask.router import route
    plan = route("list all objects with Collider component")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


def test_router_object_query_children():
    from unity_mcp.ask.router import route
    plan = route("what children does Player gameobject have?")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


def test_router_object_query_find():
    from unity_mcp.ask.router import route
    plan = route("find all objects in the scene hierarchy")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


# ---------------------------------------------------------------------------
# 24. Router — spatial nouns pass the noun gate
# ---------------------------------------------------------------------------

def test_router_spatial_noun_transform_waypoint():
    from unity_mcp.ask.router import route
    # "transform" and "waypoint" are now Unity nouns — should not return None from noun gate
    plan = route("show me transform of the waypoint")
    assert plan is not None


# ---------------------------------------------------------------------------
# 25. Router — non-Unity questions still rejected (regression)
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# 26. ask e2e — spatial query does not return "no matching Unity context"
# ---------------------------------------------------------------------------

async def test_ask_e2e_spatial_query_returns_result():
    from unity_mcp.tools.ask_tool import ask

    with patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "Waypoint1: (0,1,0), Waypoint2: (5,0,3)"
        result = await ask("What are the world positions of all waypoints?")
        assert result is not None
        assert "no matching Unity context" not in result.lower()


async def test_ask_e2e_object_query_returns_result():
    from unity_mcp.tools.ask_tool import ask

    with patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "Player, Enemy, Cube"
        result = await ask("list all objects with a Collider component")
        assert result is not None
        assert "no matching Unity context" not in result.lower()


# ---------------------------------------------------------------------------
# 27. Router — fallback: Unity noun without specific pattern → SCENE_QUERY
# ---------------------------------------------------------------------------

def test_router_fallback_unity_noun_no_pattern():
    """Question with Unity noun but no specific pattern → fallback SCENE_QUERY."""
    from unity_mcp.ask.router import route
    plan = route("describe the material on the terrain")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"


def test_router_fallback_camera_question():
    from unity_mcp.ask.router import route
    plan = route("what settings does the camera have?")
    assert plan is not None
    assert plan.key == "SCENE_QUERY"
