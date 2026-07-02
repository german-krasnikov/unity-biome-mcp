"""TDD tests for intent_common — shared DSL utilities."""
import pytest
from mcp.server.fastmcp.exceptions import ToolError

# ---------------------------------------------------------------------------
# 1. strip_fences
# ---------------------------------------------------------------------------

def test_strip_fences_removes_backtick_block():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "```\nPARAM Speed float 0\n```"
    assert strip_fences(raw) == "PARAM Speed float 0"


def test_strip_fences_removes_language_tagged_block():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "```dsl\nSTATE Idle Idle.anim\n```"
    assert strip_fences(raw) == "STATE Idle Idle.anim"


def test_strip_fences_passthrough_no_fences():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "PARAM Speed float 0"
    assert strip_fences(raw) == "PARAM Speed float 0"


def test_strip_fences_trims_whitespace():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "  PARAM Speed float 0  "
    assert strip_fences(raw) == "PARAM Speed float 0"


# ---------------------------------------------------------------------------
# 2. parse_kv — key=value line parser
# ---------------------------------------------------------------------------

def test_parse_kv_basic():
    from unity_mcp.tools.intent_common import parse_kv
    result = parse_kv("anchor=top-left pos=20,-20 size=200,30")
    assert result == {"anchor": "top-left", "pos": "20,-20", "size": "200,30"}


def test_parse_kv_quoted_value():
    from unity_mcp.tools.intent_common import parse_kv
    result = parse_kv('text="Hello World" fontSize=24')
    assert result["text"] == "Hello World"
    assert result["fontSize"] == "24"


def test_parse_kv_empty_string():
    from unity_mcp.tools.intent_common import parse_kv
    assert parse_kv("") == {}


def test_parse_kv_single_pair():
    from unity_mcp.tools.intent_common import parse_kv
    assert parse_kv("color=#c33") == {"color": "#c33"}


# ---------------------------------------------------------------------------
# 3. parse_indent_tree — 2-space indent parser
# ---------------------------------------------------------------------------

def test_parse_indent_tree_flat():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas"
    nodes = parse_indent_tree(dsl)
    assert len(nodes) == 1
    assert nodes[0]["line"] == "canvas Canvas"
    assert nodes[0]["depth"] == 0
    assert nodes[0]["parent"] is None


def test_parse_indent_tree_nested():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n  panel HUD"
    nodes = parse_indent_tree(dsl)
    assert nodes[1]["depth"] == 1
    assert nodes[1]["parent"] == nodes[0]


def test_parse_indent_tree_deep_nesting():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n  panel HUD\n    image HealthBar"
    nodes = parse_indent_tree(dsl)
    assert nodes[2]["depth"] == 2
    assert nodes[2]["parent"] == nodes[1]


def test_parse_indent_tree_skips_empty_lines():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n\n  panel HUD"
    nodes = parse_indent_tree(dsl)
    assert len(nodes) == 2


# ---------------------------------------------------------------------------
# 4. build_batch_line — assemble command line from parts
# ---------------------------------------------------------------------------

def test_build_batch_line_simple():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("animator", path="/Player", action="add_param")
    assert line == "animator path=/Player action=add_param"


def test_build_batch_line_skips_none():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("create_ui", type="Image", name=None, parent="/HUD")
    assert "name" not in line
    assert "create_ui type=Image parent=/HUD" == line


def test_build_batch_line_no_extra_args():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("create_object", name="Cube")
    assert line == "create_object name=Cube"


# ---------------------------------------------------------------------------
# 5. run_intent_pipeline
# ---------------------------------------------------------------------------

async def test_run_intent_pipeline_haiku_unavailable():
    """None from sampling.generate → ToolError raised, no send/parse/build calls."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.tools.intent_common import run_intent_pipeline
    sampling = MagicMock()
    sampling.generate = AsyncMock(return_value=None)
    with pytest.raises(ToolError, match="unavailable"):
        await run_intent_pipeline(
            send=AsyncMock(), sampling=sampling, prompt="p", feature="f",
            parse_fn=MagicMock(), build_fn=MagicMock(), dry_run=False,
        )


async def test_run_intent_pipeline_calls_parse_and_build():
    """Happy path: generate → strip → parse_fn → build_fn called in order."""
    from unittest.mock import AsyncMock, MagicMock, call
    from unity_mcp.tools.intent_common import run_intent_pipeline
    sampling = MagicMock()
    sampling.generate = AsyncMock(return_value="```\nDSL line\n```")
    parse_fn = MagicMock(return_value=["parsed"])
    build_fn = MagicMock(return_value=["batch line"])
    send = AsyncMock(return_value="ok")
    result = await run_intent_pipeline(
        send=send, sampling=sampling, prompt="p", feature="f",
        parse_fn=parse_fn, build_fn=build_fn, dry_run=False,
    )
    parse_fn.assert_called_once_with("DSL line")
    build_fn.assert_called_once_with(["parsed"])
    send.assert_called_once_with("batch", {"commands": "batch line"})
    assert "f:" in result and "1 ops" in result


async def test_run_intent_pipeline_dry_run_skips_send():
    """dry_run=True returns batch text without calling send."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.tools.intent_common import run_intent_pipeline
    sampling = MagicMock()
    sampling.generate = AsyncMock(return_value="DSL")
    parse_fn = MagicMock(return_value=["x"])
    build_fn = MagicMock(return_value=["cmd1", "cmd2"])
    send = AsyncMock()
    result = await run_intent_pipeline(
        send=send, sampling=sampling, prompt="p", feature="f",
        parse_fn=parse_fn, build_fn=build_fn, dry_run=True,
    )
    send.assert_not_called()
    assert result == "cmd1\ncmd2"


async def test_run_intent_pipeline_empty_build_returns_error():
    """build_fn returning [] → ToolError raised."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.tools.intent_common import run_intent_pipeline
    sampling = MagicMock()
    sampling.generate = AsyncMock(return_value="DSL")
    with pytest.raises(ToolError, match="DSL produced no commands"):
        await run_intent_pipeline(
            send=AsyncMock(), sampling=sampling, prompt="p", feature="f",
            parse_fn=MagicMock(return_value=[]), build_fn=MagicMock(return_value=[]),
            dry_run=False,
        )
