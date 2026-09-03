"""P1 Deterministic Enrichment — middleware tests.

Items 1, 3, 5:
  1. resolve_path_live (async, calls Unity search on cache miss)
  3. component_cache + check_component_exists
  5. categorize_console_errors
"""
from unittest.mock import AsyncMock, MagicMock
from unity_mcp.middleware import Middleware, wrap_send


# ─── Item 1: resolve_path_live ────────────────────────────────────────────────

async def test_resolve_path_live_cache_hit(mw):
    """Cache has exact match — send_fn never called."""
    mw.known_paths = {"/Player/Arm"}
    send_fn = AsyncMock()
    path, marker = await mw.resolve_path_live("/Player/Arm", send_fn)
    assert path == "/Player/Arm"
    send_fn.assert_not_called()


async def test_resolve_path_live_ref_passthrough(mw):
    """$ref paths bypass all resolution."""
    send_fn = AsyncMock()
    path, marker = await mw.resolve_path_live("$ref:abc", send_fn)
    assert path == "$ref:abc"
    send_fn.assert_not_called()


async def test_resolve_path_live_hash_passthrough(mw):
    """#id paths bypass all resolution."""
    send_fn = AsyncMock()
    path, marker = await mw.resolve_path_live("#123", send_fn)
    assert path == "#123"
    send_fn.assert_not_called()


async def test_resolve_path_live_no_cache_passthrough(mw):
    """No cache yet — no query made, return original."""
    send_fn = AsyncMock()
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Player"
    send_fn.assert_not_called()


async def test_resolve_path_live_search_single_match(mw):
    """Cache miss + search returns 1 result → rewrite path."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(return_value="/Root/Player #123")
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Root/Player"
    send_fn.assert_called_once()


async def test_resolve_path_live_search_multiple(mw):
    """Multiple ambiguous candidates → disambiguator block (Cycle 5d)."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(return_value="/Root/Player #123\n/Other/Player #456")
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path.startswith("__DISAMBIG_BLOCK__"), f"Expected block, got: {path!r}"


async def test_resolve_path_live_search_no_match(mw):
    """Search returns empty → return original."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(return_value="")
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Player"


async def test_resolve_path_live_search_error(mw):
    """send_fn throws → silently return original."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(side_effect=Exception("TCP error"))
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Player"


async def test_resolve_path_live_existing_cache_resolve(mw):
    """Suffix match in cache — resolved without calling send_fn."""
    mw.known_paths = {"/Root/Player"}
    send_fn = AsyncMock()
    path, marker = await mw.resolve_path_live("Player", send_fn)
    assert path == "/Root/Player"
    send_fn.assert_not_called()


async def test_wrap_send_resolves_path_arg(mw):
    """wrap_send rewrites path arg when resolve_path_live returns different value."""
    mw.known_paths = {"/Root/SomethingElse"}

    async def fake_send(cmd, args, timeout=30.0):
        if cmd == "search_scene":
            return "/Root/Player #123"
        return f"ok path={args.get('path')}"

    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/Player", "type": "Transform"})
    assert "/Root/Player" in result


async def test_resolve_path_live_spaced_name_candidate_extracted_correctly(mw):
    """line.split()[0] truncated paths with spaces — fixed to split on ' #'. (P3)"""
    mw.known_paths = {"/Root/Other"}
    # search_scene line format: "path #instanceId [Components]" (SearchHelper.cs:30)
    send_fn = AsyncMock(return_value="/[NAME WITH SPACE]/Child #12345 [Transform]")
    path, _ = await mw.resolve_path_live("/[NAME WITH SPACE]/Child", send_fn)
    assert path == "/[NAME WITH SPACE]/Child"


async def test_resolve_path_live_strips_dollar_suffix(mw):
    """G1: search_scene line with $ref suffix — only path returned, not $ref."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(return_value="/Root/Player $ref_123")
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Root/Player"


async def test_resolve_path_live_strips_combined_suffix(mw):
    """G1: search_scene line with '& ref # comment' — only path token returned."""
    mw.known_paths = {"/Root/SomethingElse"}
    send_fn = AsyncMock(return_value="/Root/Player &456 # Transform")
    path, marker = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Root/Player"


async def test_resolve_path_live_strips_hash_with_long_suffix(mw):
    """S8786 fix: \\s+[#$&][^\\n]* instead of .* — verify long suffix stripped correctly."""
    mw.known_paths = {"/Root/Other"}
    long_meta = "#inst=" + "x" * 500
    send_fn = AsyncMock(return_value=f"/Root/Player {long_meta}")
    path, _ = await mw.resolve_path_live("/Player", send_fn)
    assert path == "/Root/Player"


async def test_resolve_path_live_bracket_name_leaf_extracted_correctly(mw):
    """rsplit('/') was bracket-blind — leaf of '/Root/[Zone_A/B]' should be '[Zone_A/B]'. (P2)"""
    mw.known_paths = {"/Root/Other"}
    send_fn = AsyncMock(return_value="")
    await mw.resolve_path_live("/Root/[Zone_A/B]", send_fn)
    # After fix: leaf extracted is '[Zone_A/B]', not 'B]'
    query = send_fn.call_args.args[1]["query"]
    assert "[Zone_A/B]" in query


# ─── Item 3: Component Cache ──────────────────────────────────────────────────

def test_component_cache_from_get_component(mw):
    mw.cache_components("get_component", {"path": "/Player", "type": "Health"}, "ok")
    assert "Health" in mw._component_cache.get("/Player", set())


def test_component_cache_get_component_no_path(mw):
    """Missing path key — no crash."""
    mw.cache_components("get_component", {"type": "Health"}, "ok")
    assert mw._component_cache == {}


def test_component_cache_from_inspect(mw):
    result = "--- /Player ---\n[Health]\nvalue: 100\n[Rigidbody]\nmass: 1"
    mw.cache_components("inspect", {}, result)
    assert "Health" in mw._component_cache.get("/Player", set())
    assert "Rigidbody" in mw._component_cache.get("/Player", set())


def test_component_cache_inspect_multiple_objects(mw):
    result = "--- /Player ---\n[Health]\n--- /Enemy ---\n[EnemyAI]\n"
    mw.cache_components("inspect", {}, result)
    assert "Health" in mw._component_cache.get("/Player", set())
    assert "EnemyAI" in mw._component_cache.get("/Enemy", set())


def test_check_component_exists_unknown_path(mw):
    """Path not in cache → None (unknown, let Unity handle)."""
    assert mw.check_component_exists("/Player", "Health") is None


def test_check_component_exists_known(mw):
    """Component in cache → None (exists)."""
    mw._component_cache["/Player"] = {"Health", "Transform"}
    assert mw.check_component_exists("/Player", "Health") is None


def test_check_component_exists_missing(mw):
    """Component NOT in cache for known path → warning string."""
    mw._component_cache["/Player"] = {"Transform"}
    result = mw.check_component_exists("/Player", "Health")
    assert result is not None
    assert "Health" in result
    assert "/Player" in result


def test_check_component_case_insensitive(mw):
    """Case difference → None (InputNormalizer handles it)."""
    mw._component_cache["/Player"] = {"health"}
    assert mw.check_component_exists("/Player", "Health") is None


def test_check_component_empty_cache_for_path(mw):
    """Path in cache but empty set → None (unknown)."""
    mw._component_cache["/Player"] = set()
    assert mw.check_component_exists("/Player", "Health") is None


async def test_wrap_send_populates_component_cache(mw):
    """wrap_send calls cache_components after each response."""
    fake_send = AsyncMock(return_value="[Health]\nvalue: 100")
    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_component", {"path": "/Player", "type": "Health"})
    assert "Health" in mw._component_cache.get("/Player", set())


async def test_wrap_send_blocks_missing_component(mw):
    """wrap_send raises ToolError when component definitely absent."""
    mw._component_cache["/Player"] = {"Transform"}
    fake_send = AsyncMock(return_value="ok")
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("set_property", {"path": "/Player", "component": "Health", "prop": "hp", "value": "50"})
    assert "Health" in result
    fake_send.assert_not_called()


# ─── Item 5: Console Error Categorization ─────────────────────────────────────

def test_categorize_nullref(mw):
    result = mw.categorize_console_errors("NullReferenceException: Object not set")
    assert "NullRef" in result and "validate_references" in result, result


def test_categorize_missing_component(mw):
    result = mw.categorize_console_errors("MissingComponentException: no component")
    assert "Missing component" in result and "get_components_list" in result, result


def test_categorize_format_error(mw):
    result = mw.categorize_console_errors("FormatException: Input string was not in a correct format")
    assert "Format error" in result and "get_schema" in result, result


def test_categorize_no_errors(mw):
    original = "ok: property set"
    result = mw.categorize_console_errors(original)
    assert result == original


def test_categorize_format_error_variant(mw):
    result = mw.categorize_console_errors("Input string was not in a correct format")
    assert "[HINT:" in result


async def test_wrap_send_categorizes_errors(mw):
    """wrap_send appends HINT when result has NullReferenceException."""
    fake_send = AsyncMock(return_value="NullReferenceException: boom")
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    assert "[HINT:" in result


# ── TestResolvePathAndValidate ────────────────────────────────────────────────

import pytest


class TestResolvePathAndValidate:
    """Unit tests for the extracted _resolve_path_and_validate helper."""

    @pytest.mark.asyncio
    async def test_no_path_in_args_returns_empty_marker(self):
        """No 'path' key in args → returns (args, '') without calling resolve."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        mw = MagicMock()
        mw.schema_guard = None
        flags = {"_explicit_path": False, "_no_validate": True}
        args = {"value": "42"}
        result = await _resolve_path_and_validate("set_property", args, mw, AsyncMock(), flags)
        assert isinstance(result, tuple)
        assert result[0] == args
        assert result[1] == ""
        mw.resolve_path_live.assert_not_called()

    @pytest.mark.asyncio
    async def test_explicit_path_skips_resolution(self):
        """_explicit_path=True → path not resolved."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        mw = MagicMock()
        mw.schema_guard = None
        flags = {"_explicit_path": True, "_no_validate": True}
        result = await _resolve_path_and_validate(
            "get_component", {"path": "/Player"}, mw, AsyncMock(), flags
        )
        assert isinstance(result, tuple)
        mw.resolve_path_live.assert_not_called()

    @pytest.mark.asyncio
    async def test_disambig_block_returns_block_string(self):
        """Disambig block from resolve → returns the block text (early exit)."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        mw = MagicMock()
        mw.resolve_path_live = AsyncMock(
            return_value=("__DISAMBIG_BLOCK__\nChoose: /A or /B", "")
        )
        mw.schema_guard = None
        flags = {"_explicit_path": False, "_no_validate": True}
        result = await _resolve_path_and_validate(
            "get_component", {"path": "/P"}, mw, AsyncMock(), flags
        )
        assert isinstance(result, str)
        assert "Choose:" in result

    @pytest.mark.asyncio
    async def test_schema_guard_block_returns_block(self):
        """Schema guard validation fails → returns block string."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        from unittest.mock import patch
        mw = MagicMock()
        mw.resolve_path_live = AsyncMock(return_value=("/Player", ""))
        mw.schema_guard = MagicMock()
        mw.schema_guard.validate = AsyncMock(return_value="schema error")
        flags = {"_explicit_path": False, "_no_validate": False}
        with patch("unity_mcp.metrics.METRICS", MagicMock()):
            result = await _resolve_path_and_validate(
                "set_property", {"path": "/Player"}, mw, AsyncMock(), flags
            )
        assert result == "schema error"

    @pytest.mark.asyncio
    async def test_component_absent_returns_warn(self):
        """Component not in cache → returns comp_warn string."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        mw = MagicMock()
        mw.resolve_path_live = AsyncMock(return_value=("/Player", ""))
        mw.schema_guard = None
        mw.check_component_exists.return_value = "Component 'Rigidbody' not found"
        flags = {"_explicit_path": False, "_no_validate": True}
        result = await _resolve_path_and_validate(
            "set_property",
            {"path": "/Player", "component": "Rigidbody"},
            mw, AsyncMock(), flags,
        )
        assert result == "Component 'Rigidbody' not found"

    @pytest.mark.asyncio
    async def test_all_pass_returns_resolved_args_and_marker(self):
        """All guards pass → (updated_args, resolve_marker) tuple."""
        from unity_mcp.middleware_pipeline import _resolve_path_and_validate
        mw = MagicMock()
        mw.resolve_path_live = AsyncMock(
            return_value=("/Player_found", "[RESOLVED: /Player → /Player_found]")
        )
        mw.schema_guard = None
        flags = {"_explicit_path": False, "_no_validate": True}
        result = await _resolve_path_and_validate(
            "get_component", {"path": "/Player"}, mw, AsyncMock(), flags
        )
        assert isinstance(result, tuple)
        args, marker = result
        assert args["path"] == "/Player_found"
        assert "RESOLVED" in marker
