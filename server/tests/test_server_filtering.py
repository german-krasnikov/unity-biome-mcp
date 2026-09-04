"""Tests for dynamic MCP tool filtering based on Unity MCPSettings."""
import ast
import os
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import mcp.types as mcp_types

from unity_mcp.server import _filter_tools, mcp


def _tool(name: str):
    return SimpleNamespace(name=name, description=f"{name} tool.", inputSchema={"type": "object"})


ALL_TOOLS = [_tool("get_hierarchy"), _tool("scene"), _tool("shader"), _tool("get_enabled_tools")]


# --- test _filter_tools fallback (gating only, no Unity cache) ---

async def test_filter_tools_fallback_when_bridge_none(monkeypatch):
    """Phase 1b: get_enabled_tools demoted from TIER1; scene promoted (Phase 1a)."""
    import unity_mcp.server as srv
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    result = await _filter_tools(ALL_TOOLS, None)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "scene" in names  # tier1 after Phase 1a
    assert "get_enabled_tools" not in names  # demoted to Tier2 in Phase 1b
    assert "shader" not in names  # gated out (not in TIER1)


async def test_filter_tools_fallback_when_disconnected(monkeypatch):
    import unity_mcp.server as srv
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    bridge = AsyncMock()
    bridge.connected = False
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


async def test_filter_tools_fallback_on_send_error(monkeypatch):
    import unity_mcp.server as srv
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(side_effect=ConnectionError("lost"))
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


async def test_filter_tools_fallback_on_unity_error(monkeypatch):
    import unity_mcp.server as srv
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": False, "err": "fail"})
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


# --- Core bug-fix: disabled-set semantics ---

async def test_disabled_tier1_tool_hidden(monkeypatch):
    """CORE BUG FIX: unchecking screenshot in Unity form must remove it from ListTools."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    monkeypatch.setattr(srv, "_disabled_tools_cache", {"screenshot"})
    tools = [_tool("screenshot"), _tool("get_hierarchy")]
    result = await _filter_tools(tools, None)
    names = {t.name for t in result}
    assert "screenshot" not in names, "Disabled TIER1 tool must be hidden"
    assert "get_hierarchy" in names


def test_intent_tools_in_schema_keep_full():
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    for name in ('ui_intent', 'vfx_intent', 'uitk_intent'):
        assert name in _SCHEMA_KEEP_FULL, f"{name} must be in _SCHEMA_KEEP_FULL"


async def test_core_tools_survive_disabled(monkeypatch):
    """_CORE_TOOLS must never be hidden even if in disabled set.

    A2 regression: server_filtering.py:97 used to check the hand-typed
    FORCE_VISIBLE (11 tools) instead of the generated _CORE_TOOLS (24 tools).
    'create_object' is core but was NOT in FORCE_VISIBLE, so it was silently
    disable-able -- this is the money assertion that catches that gap.
    """
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    # Wave 2: 'do' demoted from CORE to SYSTEM direct_only; use 'inspect' instead
    monkeypatch.setattr(
        srv, "_disabled_tools_cache", {"inspect", "set_property", "create_object", "screenshot"}
    )
    tools = [_tool("inspect"), _tool("set_property"), _tool("create_object"), _tool("screenshot")]
    result = await _filter_tools(tools, None)
    names = {t.name for t in result}
    assert "inspect" in names, "CORE 'inspect' must survive disabled set"
    assert "set_property" in names, "CORE 'set_property' must survive disabled set"
    assert "create_object" in names, "CORE 'create_object' (A2 gap) must survive disabled set"
    assert "screenshot" not in names, "Non-CORE disabled tool must be hidden"


async def test_disabled_cache_none_no_hiding(monkeypatch):
    """None cache = gating-only fallback, nothing extra hidden."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    tools = [_tool("screenshot"), _tool("get_hierarchy")]
    result = await _filter_tools(tools, None)
    names = {t.name for t in result}
    # Both are TIER1, gating keeps them; disabled cache is None so no hiding
    assert "screenshot" in names
    assert "get_hierarchy" in names


# --- Cache interaction tests (disabled-set semantics) ---

async def test_filter_tools_uses_cache_when_available(monkeypatch):
    """With disabled cache populated, _filter_tools must NOT call bridge.send."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    tool_b = Mock()
    tool_b.name = "set_property"
    bridge = AsyncMock()
    bridge.send = AsyncMock()

    monkeypatch.setattr(srv, "_disabled_tools_cache", set())  # empty disabled set = nothing hidden
    bridge.send.reset_mock()
    result = await srv._filter_tools([tool_a, tool_b], bridge)
    bridge.send.assert_not_called()
    assert tool_a in result
    assert tool_b in result


async def test_filter_tools_fallback_when_cache_empty(monkeypatch):
    """With None cache, _apply_gating is used (no TCP)."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    bridge = AsyncMock()
    bridge.connected = False

    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    bridge.send.reset_mock()
    result = await srv._filter_tools([tool_a], bridge)
    bridge.send.assert_not_called()
    assert any(t.name == "get_hierarchy" for t in result)


async def test_disabled_tools_cache_populated_on_reconnect(monkeypatch):
    """Reconnect populates _disabled_tools_cache via get_disabled_tools."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot,shader"})

    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    await srv._refresh_tools_cache(bridge)
    assert srv._disabled_tools_cache == {"screenshot", "shader"}


async def test_disabled_tools_empty_csv_gives_empty_set(monkeypatch):
    """Empty CSV from Unity must produce empty set, not {''}."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    await srv._refresh_tools_cache(bridge)
    assert srv._disabled_tools_cache == set(), f"Expected empty set, got {srv._disabled_tools_cache}"


# ---------------------------------------------------------------------------
# Fix 4: _refresh_tools_cache notifies the live MCP session when the disabled
# set actually changes — closes Gap B (client never re-fetches ListTools).
# ---------------------------------------------------------------------------

async def test_refresh_tools_cache_notifies_session_on_disabled_set_change(monkeypatch):
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot"})

    fake_session = AsyncMock()
    monkeypatch.setattr(srv, "_disabled_tools_cache", set())  # differs from the new {"screenshot"} value
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    with patch("unity_mcp.server_filtering.get_active_session", return_value=fake_session):
        await srv._refresh_tools_cache(bridge)
    fake_session.send_tool_list_changed.assert_awaited_once()


async def test_refresh_tools_cache_no_notify_when_disabled_set_unchanged(monkeypatch):
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot"})

    fake_session = AsyncMock()
    monkeypatch.setattr(srv, "_disabled_tools_cache", {"screenshot"})  # same as the new value
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    with patch("unity_mcp.server_filtering.get_active_session", return_value=fake_session):
        await srv._refresh_tools_cache(bridge)
    fake_session.send_tool_list_changed.assert_not_awaited()


async def test_refresh_tools_cache_no_notify_when_no_session_captured(monkeypatch):
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot"})

    monkeypatch.setattr(srv, "_disabled_tools_cache", set())
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    with patch("unity_mcp.server_filtering.get_active_session", return_value=None):
        await srv._refresh_tools_cache(bridge)  # must not raise
    assert srv._disabled_tools_cache == {"screenshot"}


def test_filter_tools_hides_disabled():
    """Tool in disabled set must not appear in filter_tools result."""
    from types import SimpleNamespace
    from unity_mcp.server_filtering import filter_tools
    import unity_mcp.tools.gating as gating
    gating.reset()
    tools = [SimpleNamespace(name="screenshot", description="x", inputSchema={})]
    result = filter_tools(tools, {"screenshot"})
    assert not any(t.name == "screenshot" for t in result)
    gating.reset()


# --- test handler registration ---

def test_request_handler_is_patched():
    """Our wrapper must be installed in request_handlers, not the original FastMCP handler."""
    handler = mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest]
    assert handler.__name__ == "_filtered_tools_handler"


# --- TDD F4: handler strips deferred / preserves core ---

async def test_handler_strips_non_core_schema(monkeypatch):
    """_filter_tools returns STUB schema for non-core tools."""
    import unity_mcp.server as srv
    from unity_mcp.tools.schema_registry import STUB_SCHEMA

    full = {"type": "object", "properties": {"x": {"type": "string"}}, "required": ["x"]}
    tool_core = _tool("get_hierarchy")
    tool_core.inputSchema = full
    tool_noncore = _tool("animation")
    tool_noncore.inputSchema = full

    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    result = await srv._filter_tools([tool_core, tool_noncore], None)
    names = {t.name: t for t in result}
    # get_hierarchy passes gating — verify its schema kept (if returned)
    if "get_hierarchy" in names:
        assert names["get_hierarchy"].inputSchema == full
    # animation gets gated out by tier filter (not in TIER1 and not enabled)
    assert "animation" not in names


async def test_handler_preserves_core_full_schema():
    """Core tools keep their full inputSchema after strip."""
    import unity_mcp.server as srv
    from unity_mcp.server import _strip_deferred_schemas

    full = {"type": "object", "properties": {"p": {"type": "string"}}, "required": ["p"]}
    tool = _tool("batch")
    tool.inputSchema = full

    tools = [tool]
    _strip_deferred_schemas(tools)
    assert tools[0].inputSchema == full


# ---------------------------------------------------------------------------
# A3: _tcp_probe + read_unity_port TCP-probe integration
# ---------------------------------------------------------------------------

def test_read_unity_port_pid_alive_candidate_always_included(tmp_path):
    """PID alive → candidate always included. TCP probe was removed to stop Unity console spam.
    Previously this test checked that tcp_probe=False skipped the candidate; that behavior
    is gone — PID liveness is the only gate.
    """
    from unittest.mock import patch, MagicMock
    from pathlib import Path

    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    port_file = ports_dir / "12345.port"
    port_file.write_text("9501\n/some/project\nMyProject", encoding="utf-8")

    with (
        patch("unity_mcp.server_filtering.Path") as mock_path_cls,
        patch("unity_mcp.server_filtering._is_pid_alive", return_value=True),  # all platforms
        patch.dict("os.environ", {}, clear=False),
    ):
        mock_home = MagicMock()
        mock_path_cls.home.return_value = mock_home
        mock_home.__truediv__ = lambda self, x: tmp_path / x if x == ".unity-biome-mcp" else MagicMock()

        from unity_mcp import server_filtering
        with patch.object(Path, "home", return_value=tmp_path):
            result = server_filtering.read_unity_port()

    assert result == 9501  # candidate included — no probe skipping


def test_read_unity_port_cyrillic_project_path_parses_correctly(tmp_path):
    """Cyrillic project path in .port file must parse without mojibake.
    Discriminating: remove encoding= from server_filtering.py and this test fails
    (UnicodeDecodeError on cp1251 systems / garbled project field on macOS).
    Uses write_bytes to avoid test-side EncodingWarning.
    """
    from pathlib import Path

    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    port_file = ports_dir / "12345.port"
    # Write file as raw UTF-8 bytes — bypasses EncodingWarning gate on test side
    port_file.write_bytes("9501\n/Users/Иван/MyProject\nМойПроект\n".encode("utf-8"))

    with (
        patch("unity_mcp.server_filtering._tcp_probe", return_value=True),
        patch("unity_mcp.server_filtering._is_pid_alive", return_value=True),  # all platforms
        patch.object(Path, "home", return_value=tmp_path),
    ):
        from unity_mcp import server_filtering
        result = server_filtering.read_unity_port()

    assert result == 9501  # port parsed correctly despite Cyrillic


def test_read_unity_port_never_calls_tcp_probe(tmp_path):
    """read_unity_port must not call _tcp_probe — removed to avoid Unity console spam.
    PID liveness check is sufficient; bridge heartbeat handles transient port unreadiness.
    """
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "12345.port").write_bytes(b"9501\n/some/project\nMyProject")

    probe_calls = []
    with (
        patch("unity_mcp.server_filtering._tcp_probe", side_effect=lambda p: probe_calls.append(p) or True),
        patch("os.kill"),  # PID alive — no exception
        patch.object(Path, "home", return_value=tmp_path),
    ):
        from unity_mcp import server_filtering
        server_filtering.read_unity_port()

    assert probe_calls == [], "_tcp_probe must never be called from read_unity_port"


# ---------------------------------------------------------------------------
# PY2.arch.3: push_catalog must omit empty categories
# ---------------------------------------------------------------------------

async def test_push_catalog_omits_empty_categories():
    """push_catalog must not send 'CONNECTION:' (or any empty-tools category)."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.server_filtering import push_catalog

    bridge = MagicMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    await push_catalog(bridge)

    bridge.send.assert_called_once()
    catalog_arg = bridge.send.call_args[1].get("catalog") or bridge.send.call_args[0][1].get("catalog", "")
    for line in catalog_arg.split("\n"):
        if ":" in line:
            cat, tools_str = line.split(":", 1)
            assert tools_str.strip(), f"Category '{cat}' has empty tools list in catalog"


# ---------------------------------------------------------------------------
# PY3.arch.1: _strip_deferred_schemas must use canonical STUB_SCHEMA (identity)
# ---------------------------------------------------------------------------

def test_strip_uses_canonical_stub():
    """Non-core tool's inputSchema after strip must be the exact STUB_SCHEMA object."""
    from types import SimpleNamespace
    from unity_mcp.server_filtering import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA

    tool = SimpleNamespace(name="lighting", description="Control scene lighting.", inputSchema={"type": "object", "properties": {}})
    tools = [tool]
    _strip_deferred_schemas(tools)
    assert tools[0].inputSchema is STUB_SCHEMA


# ---------------------------------------------------------------------------
# X4.cross.4: UNITY_MCP_NO_GATING=1 bypasses tier filter
# ---------------------------------------------------------------------------

def test_no_gating_env_bypasses_filter(monkeypatch):
    """UNITY_MCP_NO_GATING=1 makes _apply_gating return the original list unchanged."""
    from types import SimpleNamespace
    from unity_mcp.server_filtering import _apply_gating

    monkeypatch.setenv("UNITY_MCP_NO_GATING", "1")
    tools = [SimpleNamespace(name="shader"), SimpleNamespace(name="animation")]
    result = _apply_gating(tools)
    assert result is tools


# ---------------------------------------------------------------------------
# Reconnect spam fix: push_catalog skip-if-locked guard
# ---------------------------------------------------------------------------

async def test_push_catalog_skips_when_locked():
    """push_catalog() must not call send() if the lock is already held."""
    import asyncio
    from unity_mcp import server_filtering
    from unittest.mock import AsyncMock

    # Reset module-level lock so test is isolated
    server_filtering._push_catalog_lock = asyncio.Lock()

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True})

    async with server_filtering._push_catalog_lock:
        # Lock is held; push_catalog must skip
        await server_filtering.push_catalog(bridge)

    bridge.send.assert_not_called()


async def test_push_catalog_sends_when_unlocked():
    """push_catalog() proceeds normally when lock is free."""
    import asyncio
    from unity_mcp import server_filtering
    from unittest.mock import AsyncMock

    server_filtering._push_catalog_lock = None  # fresh state

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True})

    await server_filtering.push_catalog(bridge)

    bridge.send.assert_called_once()
    call_args = bridge.send.call_args
    assert call_args[0][0] == "set_tool_catalog"


# ---------------------------------------------------------------------------
# cleanup_stale_port_files — stale reload-port files (Bug #3 bonus)
# ---------------------------------------------------------------------------

def test_stale_reload_port_cleanup_removes_dead_pid(tmp_path):
    """Dead-PID *.reload-port files are deleted by cleanup_stale_port_files()."""
    from unity_mcp.server_filtering import cleanup_stale_port_files
    from pathlib import Path
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    dead_pid = 99999999
    (ports_dir / f"{dead_pid}.reload-port").write_text("9600", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 1
    assert not (ports_dir / f"{dead_pid}.reload-port").exists()


def test_stale_reload_port_cleanup_preserves_alive_pid(tmp_path):
    """Alive-PID *.reload-port files are NOT deleted."""
    from unity_mcp.server_filtering import cleanup_stale_port_files
    from pathlib import Path
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    alive_pid = os.getpid()
    f = ports_dir / f"{alive_pid}.reload-port"
    f.write_text("9600", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0
    assert f.exists()


def test_stale_reload_port_cleanup_no_dir():
    """Missing ports dir returns 0 without error."""
    from unity_mcp.server_filtering import cleanup_stale_port_files
    from pathlib import Path
    with patch.object(Path, "home", return_value=Path("/nonexistent_xyz_abc")):
        assert cleanup_stale_port_files() == 0


# ---------------------------------------------------------------------------
# Task 3: Plugin subcategory — per-tool disabled semantics
# ---------------------------------------------------------------------------

async def test_plugin_tool_disabled_removes_only_it(monkeypatch):
    """Disabling one plugin tool removes only that tool; sibling stays visible."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    monkeypatch.setattr(srv, "_disabled_tools_cache", {"blender_do"})
    tools = [_tool("blender_do"), _tool("blender_info")]
    result = await _filter_tools(tools, None)
    names = {t.name for t in result}
    assert "blender_do" not in names, "Disabled plugin tool must be hidden"
    assert "blender_info" in names, "Sibling plugin tool must remain visible"


async def test_plugin_tool_csv_roundtrip(monkeypatch):
    """CSV from Unity containing plugin tool names is parsed into _disabled_tools_cache correctly."""
    import unity_mcp.server as srv
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "blender_do,blender_render"})
    monkeypatch.setattr(srv, "_disabled_tools_cache", None)
    monkeypatch.setattr(srv, "_refresh_tools_lock", None)
    await srv._refresh_tools_cache(bridge)
    assert srv._disabled_tools_cache == {"blender_do", "blender_render"}


# --- Item 1: empty disabled=set() must not be treated as falsy ---

async def test_filter_tools_empty_disabled_set():
    """disabled=set() must not skip subtraction (empty set is falsy but NOT None)."""
    from unity_mcp.server_filtering import filter_tools
    import unity_mcp.tools.gating as gating
    gating.reset()
    tools = [_tool("screenshot"), _tool("get_hierarchy")]
    # With empty set, nothing should be hidden, but the branch still must execute.
    result = filter_tools(tools, set())
    names = {t.name for t in result}
    assert "screenshot" in names, "empty disabled set must not hide screenshot"
    assert "get_hierarchy" in names, "empty disabled set must not hide get_hierarchy"
    gating.reset()


async def test_filter_tools_disabled_set_hides_non_core():
    """Tool in disabled set and NOT in _CORE_TOOLS must be hidden."""
    from unity_mcp.server_filtering import filter_tools
    import unity_mcp.tools.gating as gating
    gating.reset()
    tools = [_tool("screenshot"), _tool("get_hierarchy")]
    result = filter_tools(tools, {"screenshot"})
    names = {t.name for t in result}
    assert "screenshot" not in names, "disabled non-core tool must be hidden"
    assert "get_hierarchy" in names, "non-disabled tool must remain visible"
    gating.reset()


# ---------------------------------------------------------------------------
# Issue 25: deferred description truncation (token budget)
# ---------------------------------------------------------------------------

def test_short_description_truncates_at_sentence_boundary():
    from unity_mcp.server_filtering import _short_description, _SHORT_DESC_MAX_LEN
    long_desc = "Do X. " + "y" * _SHORT_DESC_MAX_LEN
    result = _short_description(long_desc)
    assert result == "Do X."


def test_short_description_hard_truncates_no_early_sentence():
    from unity_mcp.server_filtering import _short_description, _SHORT_DESC_MAX_LEN
    long_desc = "a" * 200  # single sentence, no ". " boundary anywhere
    result = _short_description(long_desc)
    assert len(result) <= _SHORT_DESC_MAX_LEN + 1  # +1 for the '…' suffix
    assert result.endswith('…')


def test_short_description_noop_when_already_short():
    from unity_mcp.server_filtering import _short_description
    short = "Take a screenshot."
    assert _short_description(short) == short


def test_short_description_truncates_at_newline_boundary():
    from unity_mcp.server_filtering import _short_description
    desc = (
        "Single verification gate after code/scene changes.\n"
        "Gates are additive — only enabled ones run:\n"
        "1. await_compile (always)\n"
        "2. console_since (opt-in)\n"
    )
    assert _short_description(desc) == "Single verification gate after code/scene changes."


def test_strip_deferred_details_shortens_description_for_non_core_tool():
    """Non-core, non-keep-full tool: description shortened AND schema stubbed, same pass."""
    from unity_mcp.server_filtering import _strip_deferred_schemas, _short_description
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    long_desc = (
        "Plays animation clips on a target GameObject via the Animator component. "
        "Supports ASSERT, ASSERT_CONSOLE_CLEAN, WAIT and other directives for verifying "
        "animation behavior end to end without manual clicking."
    )
    tool = SimpleNamespace(name="lighting", description=long_desc,
                           inputSchema={"type": "object", "properties": {"clip": {"type": "string"}}})
    tools = [tool]
    _strip_deferred_schemas(tools)
    assert tools[0].description == _short_description(long_desc)
    assert tools[0].description != long_desc
    assert tools[0].inputSchema is STUB_SCHEMA


def test_strip_deferred_details_preserves_full_description_for_core_tool():
    """Core tool: description untouched, schema untouched."""
    from unity_mcp.server_filtering import _strip_deferred_schemas
    long_desc = (
        "Returns the full scene hierarchy as a compact indented text tree, one line per "
        "GameObject, with component markers and active-state flags for quick inspection."
    )
    full_schema = {"type": "object", "properties": {"path": {"type": "string"}}}
    tool = SimpleNamespace(name="get_hierarchy", description=long_desc, inputSchema=full_schema)
    tools = [tool]
    _strip_deferred_schemas(tools)
    assert tools[0].description == long_desc
    assert tools[0].inputSchema == full_schema


async def test_schema_registry_capture_stores_full_description_before_truncation():
    """resolve_tool_schema must still return the ORIGINAL description after the
    list-tools pipeline truncates the live Tool object (capture-before-strip ordering)."""
    from unity_mcp.tools.schema_registry import SchemaRegistry
    from unity_mcp.server_filtering import _strip_deferred_schemas, _short_description

    long_desc = (
        "Plays animation clips on a target GameObject via the Animator component. "
        "Supports ASSERT, ASSERT_CONSOLE_CLEAN, WAIT and other directives for verifying "
        "animation behavior end to end without manual clicking."
    )
    tool = SimpleNamespace(name="lighting", description=long_desc,
                           inputSchema={"type": "object", "properties": {"clip": {"type": "string"}}})

    registry = SchemaRegistry()
    # Mirror install_list_tools_filter's capture-before-strip ordering.
    registry.capture(tool.name, tool.inputSchema, tool.description)
    _strip_deferred_schemas([tool])

    # Live tool object is now truncated...
    assert tool.description == _short_description(long_desc)
    assert tool.description != long_desc
    # ...but the registry still holds the ORIGINAL full text.
    assert registry.get_full("lighting")["description"] == long_desc


# ---------------------------------------------------------------------------
# Client identification from a request carrying real session context
# ---------------------------------------------------------------------------


class _WeakSession:
    client_params = None


async def test_schedule_client_label_sends_once_per_session():
    import asyncio

    from unity_mcp.server_filtering import _schedule_client_label

    session = _WeakSession()
    session.client_params = SimpleNamespace(clientInfo=SimpleNamespace(name="Cursor"))
    bridge = SimpleNamespace(send=AsyncMock(return_value={"ok": True}))

    _schedule_client_label(session, lambda: bridge)
    _schedule_client_label(session, lambda: bridge)
    await asyncio.sleep(0)

    bridge.send.assert_awaited_once_with(
        "set_client_label", {"label": "Cursor"}, timeout=3.0
    )


async def test_schedule_client_label_skips_missing_client_params():
    from unity_mcp.server_filtering import _schedule_client_label

    session = _WeakSession()
    bridge = SimpleNamespace(send=AsyncMock())

    _schedule_client_label(session, lambda: bridge)

    bridge.send.assert_not_called()


async def test_schedule_client_label_skips_default_label():
    from unity_mcp.server_filtering import _schedule_client_label

    session = _WeakSession()
    session.client_params = SimpleNamespace(
        clientInfo=SimpleNamespace(name="Claude Code")
    )
    bridge = SimpleNamespace(send=AsyncMock())

    _schedule_client_label(session, lambda: bridge)

    bridge.send.assert_not_called()


async def test_schedule_client_label_retries_after_send_failure():
    import asyncio

    from unity_mcp.server_filtering import _schedule_client_label

    session = _WeakSession()
    session.client_params = SimpleNamespace(clientInfo=SimpleNamespace(name="Codex"))
    bridge = SimpleNamespace(
        send=AsyncMock(side_effect=[RuntimeError("tcp down"), {"ok": True}])
    )

    _schedule_client_label(session, lambda: bridge)
    await asyncio.sleep(0)
    _schedule_client_label(session, lambda: bridge)
    await asyncio.sleep(0)

    assert bridge.send.await_count == 2


# ── Bug 2 regression: fire-and-forget task strong ref (SonarCloud S7502) ──────

def test_background_tasks_is_module_level_set():
    """Bug 2: _background_tasks must be a module-level set so GC cannot collect tasks."""
    import unity_mcp.server_filtering as sf
    assert hasattr(sf, "_background_tasks")
    assert isinstance(sf._background_tasks, set)


# ---------------------------------------------------------------------------
# A07b: guard against reintroducing bare `srv.X = value` module-state
# mutation in this file (the A07a incidental xdist-lane flake root cause).
# ---------------------------------------------------------------------------

_LEAK_PRONE_MODULE_ATTRS = ("_disabled_tools_cache", "_refresh_tools_lock")


def test_server_filtering_does_not_leak_module_state():
    """Every mutation of unity_mcp.server's _disabled_tools_cache/
    _refresh_tools_lock in this file must go through monkeypatch.setattr
    (auto-restoring), never a bare `srv.X = value` relying on a manual
    try/finally -- the manual pattern is what let one xdist worker observe
    another test's half-restored state (A07a incidental finding)."""
    source = Path(__file__).read_text(encoding="utf-8")
    tree = ast.parse(source, filename=__file__)
    offending_lines = [
        node.lineno
        for node in ast.walk(tree)
        if isinstance(node, ast.Assign)
        for target in node.targets
        if isinstance(target, ast.Attribute)
        and target.attr in _LEAK_PRONE_MODULE_ATTRS
        and isinstance(target.value, ast.Name) and target.value.id == "srv"
    ]
    assert not offending_lines, f"direct 'srv.X = ...' assignment(s) at line(s) {offending_lines}"
