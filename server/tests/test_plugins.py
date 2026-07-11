import pytest
import types
from unittest.mock import MagicMock, AsyncMock, patch
from unity_mcp.plugins import load_plugins


def _make_tool(name: str):
    t = MagicMock()
    t.name = name
    return t


class _DictToolManager:
    def __init__(self):
        self._tools = {}


class _DictMcp:
    """Minimal fake mirroring real FastMCP's mcp._tool_manager._tools registry
    (dict-backed, unlike the name-list FakeMcp used elsewhere in this file) —
    needed so _auto_gate_new_tools can diff before/after tool names."""

    def __init__(self):
        self._tool_manager = _DictToolManager()

    def tool(self, **kwargs):
        def decorator(fn):
            self._tool_manager._tools[fn.__name__] = fn
            return fn
        return decorator


def _clear_sys_modules(*substrings):
    import sys
    for k in list(sys.modules.keys()):
        if any(s in k for s in substrings):
            del sys.modules[k]


def test_load_plugins_calls_register():
    """load_plugins calls register on discovered plugins."""
    called = []

    fake_mod = types.ModuleType("test_plugin")
    fake_mod.register = lambda mcp, s, a: called.append("test")

    with patch("unity_mcp.plugins.pkgutil.iter_modules", return_value=[("finder", "test_plugin", False)]):
        with patch("unity_mcp.plugins.import_module", return_value=fake_mod):
            load_plugins(MagicMock(), MagicMock(), MagicMock())

    assert len(called) > 0


# no-assert: crash guard
def test_load_plugins_bad_module_skipped():
    """Plugin with import error is skipped, not crash."""
    with patch("unity_mcp.plugins.import_module") as mock_import:
        mock_import.side_effect = ImportError("no module")
        try:
            load_plugins(MagicMock(), MagicMock(), MagicMock())
        except ImportError:
            pytest.fail("ImportError should be swallowed by load_plugins")


# no-assert: crash guard
def test_plugin_without_register_skipped():
    """Module without register() is silently skipped."""
    with patch("unity_mcp.plugins.import_module") as mock_import:
        no_reg = types.ModuleType("no_reg")
        mock_import.return_value = no_reg
        try:
            load_plugins(MagicMock(), MagicMock(), MagicMock())
        except AttributeError:
            pytest.fail("Missing register() should be silently skipped")


def test_skip_plugins_env(monkeypatch):
    """UNITY_MCP_SKIP_PLUGINS=test_ skips matching plugins."""
    import importlib
    import unity_mcp.plugins as plugins_mod

    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "test_")
    importlib.reload(plugins_mod)

    registered_names = []

    class FakeMcp:
        def tool(self, **kwargs):
            def decorator(fn):
                registered_names.append(fn.__name__)
                return fn
            return decorator

    plugins_mod.load_plugins(FakeMcp(), MagicMock(), MagicMock())

    test_tools = [n for n in registered_names if n.startswith("test_")]
    assert test_tools == [], f"test_ tools registered despite skip: {test_tools}"

    monkeypatch.delenv("UNITY_MCP_SKIP_PLUGINS", raising=False)
    importlib.reload(plugins_mod)


def test_skip_plugins_empty_prefix_no_blanket_skip(monkeypatch):
    """UNITY_MCP_SKIP_PLUGINS='' or ',' does NOT skip all plugins."""
    import importlib
    import unity_mcp.plugins as plugins_mod

    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", ",")
    importlib.reload(plugins_mod)

    registered_names = []

    class FakeMcp:
        def tool(self, **kwargs):
            def decorator(fn):
                registered_names.append(fn.__name__)
                return fn
            return decorator

    plugins_mod.load_plugins(FakeMcp(), MagicMock(), MagicMock())

    # Empty skip list must not blanket-skip all plugins
    assert not plugins_mod._should_skip("some_plugin")
    monkeypatch.delenv("UNITY_MCP_SKIP_PLUGINS", raising=False)
    importlib.reload(plugins_mod)


# --- plugin_api facade tests ---

def test_plugin_api_register_dsl_tools():
    from unity_mcp.plugin_api import register_dsl_tools
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.discard("test_dsl_cmd")
    register_dsl_tools("test_dsl_cmd")
    assert "test_dsl_cmd" in _dsl_tools
    _dsl_tools.discard("test_dsl_cmd")


def test_plugin_api_register_read_cmds():
    from unity_mcp.plugin_api import register_read_cmds
    from unity_mcp.middleware import READ_CMDS
    register_read_cmds("test_read_cmd")
    assert "test_read_cmd" in READ_CMDS
    READ_CMDS.discard("test_read_cmd")


def test_plugin_api_register_write_cmds():
    from unity_mcp.plugin_api import register_write_cmds
    from unity_mcp.middleware import WRITE_CMDS
    register_write_cmds("test_write_cmd")
    assert "test_write_cmd" in WRITE_CMDS
    WRITE_CMDS.discard("test_write_cmd")


def test_plugin_api_register_tools():
    """Plugins register into a category only — the platform (not the plugin) controls
    TIER1 visibility. tier1= param no longer exists on register_tools()."""
    from unity_mcp.plugin_api import register_tools
    from unity_mcp.tools.gating import CATEGORIES, TIER1, _ALL_KNOWN
    register_tools("test_cat", {"tool_a", "tool_b"})
    assert "tool_a" in CATEGORIES["test_cat"]
    assert "tool_b" in CATEGORIES["test_cat"]
    assert "tool_a" not in TIER1, "plugins must never self-promote to TIER1"
    del CATEGORIES["test_cat"]
    _ALL_KNOWN.discard("tool_a")
    _ALL_KNOWN.discard("tool_b")


def test_plugin_api_register_features():
    from unity_mcp.plugin_api import register_features
    from unity_mcp.budget.registry import FEATURES
    register_features({"test_feat": {"priority": "low", "difficulty": 0.1, "est_in": 100, "est_out": 50, "image": False}})
    assert "test_feat" in FEATURES
    assert FEATURES["test_feat"].priority == "low"
    del FEATURES["test_feat"]


def test_plugin_api_exports():
    from unity_mcp import plugin_api
    for name in plugin_api.__all__:
        assert hasattr(plugin_api, name), f"plugin_api.__all__ lists '{name}' but it's not defined"


# --- Zone #28 gap tests ---

# no-assert: crash guard
def test_load_entry_points_bad_ep_load_skipped():
    """Entry point whose .load() raises is skipped, not crash."""
    from unity_mcp.plugins import _load_entry_points

    bad_ep = MagicMock()
    bad_ep.name = "bad_plugin"
    bad_ep.load.side_effect = Exception("failed to load")

    with patch("importlib.metadata.entry_points", return_value=[bad_ep]):
        # Should not raise
        _load_entry_points(MagicMock(), MagicMock(), MagicMock())


# no-assert: crash guard
def test_load_plugin_dirs_nonexistent_dir_skipped(monkeypatch):
    """UNITY_MCP_PLUGIN_DIRS pointing to non-existent dir is silently skipped."""
    from unity_mcp.plugins import _load_plugin_dirs

    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", "/nonexistent/path/that/does/not/exist")
    # Should not raise, just skip silently
    _load_plugin_dirs(MagicMock(), MagicMock(), MagicMock())


def test_check_api_version_too_high_returns_false():
    """Module requiring API v999 is rejected when server has API_VERSION=1."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("future_plugin")
    mod.REQUIRED_API_VERSION = 999
    result = _check_api_version(mod, "future_plugin")
    assert result is False


def test_check_api_version_compatible_returns_true():
    """Module with REQUIRED_API_VERSION <= API_VERSION is accepted."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("compat_plugin")
    mod.REQUIRED_API_VERSION = 1
    assert _check_api_version(mod, "compat_plugin") is True


def test_check_api_version_no_version_attr_returns_true():
    """Module with no REQUIRED_API_VERSION is always accepted."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("plain_plugin")
    assert _check_api_version(mod, "plain_plugin") is True


async def test_save_skill_invalid_name_slash():
    """save_skill with '/' in name raises ValueError."""
    from unity_mcp.tools.skills import save_skill
    with pytest.raises(ValueError, match="Invalid name"):
        await save_skill("bad/name", "desc", "code")


async def test_save_skill_invalid_name_dotdot():
    """save_skill with '..' in name raises ValueError."""
    from unity_mcp.tools.skills import save_skill
    with pytest.raises(ValueError, match="Invalid name"):
        await save_skill("../etc", "desc", "code")


def test_plugin_register_called_twice_no_crash():
    """Calling register twice on same module does not crash."""
    import types

    call_count = [0]
    mod = types.ModuleType("dup_plugin")
    mod.register = lambda mcp, s, a: call_count.__setitem__(0, call_count[0] + 1)

    mcp = MagicMock()
    send = MagicMock()
    args = MagicMock()

    with patch("unity_mcp.plugins.pkgutil.iter_modules", return_value=[("finder", "dup_plugin", False)]):
        with patch("unity_mcp.plugins.import_module", return_value=mod):
            from unity_mcp.plugins import load_plugins
            load_plugins(mcp, send, args)
            load_plugins(mcp, send, args)

    assert call_count[0] >= 2  # registered twice, no exception


# ── Fix 28: plugins PLUGIN_DIRS API version check ─────────────────────────────

def test_load_plugin_dirs_calls_check_api_version(tmp_path, monkeypatch):
    """Fix 28: _load_plugin_dirs must call _check_api_version for each loaded plugin."""
    import sys
    plugin_file = tmp_path / "fake_plugin.py"
    plugin_file.write_text(
        "REQUIRED_API_VERSION = 999\n"
        "def register(mcp, send, args): pass\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")

    for k in list(sys.modules.keys()):
        if "fake_plugin" in k:
            del sys.modules[k]

    checked = []
    import unity_mcp.plugins as plug

    original_check = plug._check_api_version

    def spy_check(module, name):
        checked.append(name)
        return original_check(module, name)

    monkeypatch.setattr(plug, "_check_api_version", spy_check)

    mcp = MagicMock()
    plug._load_plugin_dirs(mcp, MagicMock(), MagicMock())

    assert "fake_plugin" in checked, f"_check_api_version not called. checked={checked}"


# ── Issue 26: plugin tools default OFF Tier1 (auto-gated hidden) ──────────────

async def test_discover_tools_plugins_category_exists_with_zero_plugins_loaded():
    """The 'plugins' alias works — discover_tools never raises for it.
    Phase 2: 'plugins' maps to SYSTEM, so the result includes SYSTEM tools."""
    from unity_mcp.tools import gating

    result = await gating.discover_tools(category="plugins", enable=False)
    assert result.startswith("Category 'plugins':")


def test_plugin_tool_without_register_tools_is_auto_gated_hidden(tmp_path, monkeypatch):
    """Fix Issue 26: a plugin tool registered via bare mcp.tool() (no
    register_tools() call) is auto-enrolled into the hidden 'plugins' category —
    known but not Tier1, so filter_by_tier hides it by default."""
    from unity_mcp.plugins import _load_plugin_dirs
    from unity_mcp.tools import gating

    name = "untamed_plugin_tool"
    plugin_file = tmp_path / "fake_untamed_plugin.py"
    plugin_file.write_text(
        "def register(mcp, send, args):\n"
        f"    @mcp.tool()\n"
        f"    def {name}():\n"
        "        pass\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")
    _clear_sys_modules("fake_untamed_plugin")

    mcp = _DictMcp()
    try:
        _load_plugin_dirs(mcp, MagicMock(), MagicMock())

        assert name in gating.CATEGORIES["plugins"]
        assert name in gating._ALL_KNOWN
        assert name not in gating.TIER1
        assert gating.filter_by_tier([_make_tool(name)]) == []
    finally:
        gating.CATEGORIES["plugins"].discard(name)
        gating._ALL_KNOWN.discard(name)
        gating._THEMED_CATEGORIES["SYSTEM"] = [
            n for n in gating._THEMED_CATEGORIES["SYSTEM"] if n != name
        ]
        gating.CATEGORIES = gating._rebuild_categories()


async def test_plugin_tool_visible_after_discover_tools_plugins_category(tmp_path, monkeypatch):
    """Fix Issue 26: once auto-gated, a plugin tool becomes visible again via
    the public discover_tools(category='plugins', enable=True) escape hatch."""
    from unity_mcp.plugins import _load_plugin_dirs
    from unity_mcp.tools import gating

    name = "discoverable_plugin_tool"
    plugin_file = tmp_path / "fake_discoverable_plugin.py"
    plugin_file.write_text(
        "def register(mcp, send, args):\n"
        f"    @mcp.tool()\n"
        f"    def {name}():\n"
        "        pass\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")
    _clear_sys_modules("fake_discoverable_plugin")

    mcp = _DictMcp()
    tool = _make_tool(name)
    try:
        _load_plugin_dirs(mcp, MagicMock(), MagicMock())
        assert gating.filter_by_tier([tool]) == []  # hidden before discover

        await gating.discover_tools(category="plugins", enable=True)

        assert gating.filter_by_tier([tool]) == [tool]
    finally:
        gating.CATEGORIES["plugins"].discard(name)
        gating._ALL_KNOWN.discard(name)
        gating._THEMED_CATEGORIES["SYSTEM"] = [
            n for n in gating._THEMED_CATEGORIES["SYSTEM"] if n != name
        ]
        gating.CATEGORIES = gating._rebuild_categories()
        gating.reset()


def test_well_behaved_plugin_calling_register_tools_is_unaffected_by_auto_gate(tmp_path, monkeypatch):
    """Fix Issue 26 + architecture fix: a plugin that self-declares via
    plugin_api.register_tools() is untouched by the auto-gate diff — its explicit
    category choice wins, and it does NOT also land in the fallback 'plugins' bucket.
    tier1= no longer exists: the platform controls visibility, plugins never
    self-promote into TIER1 — the tool stays Tier2 (discoverable via its own category)."""
    from unity_mcp.plugins import _load_plugin_dirs
    from unity_mcp.tools import gating

    name = "well_behaved_tool"
    plugin_file = tmp_path / "fake_well_behaved_plugin.py"
    plugin_file.write_text(
        "from unity_mcp.plugin_api import register_tools\n"
        "def register(mcp, send, args):\n"
        f"    @mcp.tool()\n"
        f"    def {name}():\n"
        "        pass\n"
        f"    register_tools('custom_cat', {{'{name}'}})\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")
    _clear_sys_modules("fake_well_behaved_plugin")

    mcp = _DictMcp()
    try:
        _load_plugin_dirs(mcp, MagicMock(), MagicMock())

        assert name not in gating.TIER1, "plugins must never self-promote to TIER1"
        assert name in gating.CATEGORIES.get("custom_cat", set())
        assert name not in gating.CATEGORIES.get("plugins", set())
    finally:
        gating._ALL_KNOWN.discard(name)
        gating.CATEGORIES.pop("custom_cat", None)
        gating.CATEGORIES["plugins"].discard(name)


def test_auto_gate_diffs_per_plugin_not_across_all_plugins(tmp_path, monkeypatch):
    """Fix Issue 26: two plugins loaded in the same pass each get their own
    tool auto-enrolled — guards against a diff computed once across all
    plugins that could misattribute or drop entries."""
    from unity_mcp.plugins import _load_plugin_dirs
    from unity_mcp.tools import gating

    name_a, name_b = "plugin_a_tool", "plugin_b_tool"
    (tmp_path / "fake_plugin_a.py").write_text(
        "def register(mcp, send, args):\n"
        f"    @mcp.tool()\n"
        f"    def {name_a}():\n"
        "        pass\n",
        encoding="utf-8",
    )
    (tmp_path / "fake_plugin_b.py").write_text(
        "def register(mcp, send, args):\n"
        f"    @mcp.tool()\n"
        f"    def {name_b}():\n"
        "        pass\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")
    _clear_sys_modules("fake_plugin_a", "fake_plugin_b")

    mcp = _DictMcp()
    try:
        _load_plugin_dirs(mcp, MagicMock(), MagicMock())

        assert name_a in gating.CATEGORIES["plugins"]
        assert name_b in gating.CATEGORIES["plugins"]
    finally:
        gating._ALL_KNOWN.discard(name_a)
        gating._ALL_KNOWN.discard(name_b)
        gating._THEMED_CATEGORIES["SYSTEM"] = [
            n for n in gating._THEMED_CATEGORIES["SYSTEM"] if n not in (name_a, name_b)
        ]
        gating.CATEGORIES = gating._rebuild_categories()


def test_auto_gate_cleanup_leaves_no_themed_plugins_residue(monkeypatch):
    """Regression for C4: register_tools('plugins', ...) writes into
    _THEMED_CATEGORIES['SYSTEM'] (via alias) too; cleanup must mirror all writes or
    later tests (e.g. test_no_orphan_themed_tools) see phantom entries.
    Phase 2: 'plugins' alias resolves to SYSTEM."""
    from unity_mcp.tools import gating

    before = list(gating._THEMED_CATEGORIES["SYSTEM"])
    name = "plugin_c4_regression_tool"
    gating.register_tools("plugins", {name})
    assert name in gating._THEMED_CATEGORIES["SYSTEM"]
    # --- simulate the (fixed) cleanup a well-behaved test/plugin unload would do ---
    gating._ALL_KNOWN.discard(name)
    gating._THEMED_CATEGORIES["SYSTEM"] = [
        n for n in gating._THEMED_CATEGORIES["SYSTEM"] if n != name
    ]
    gating.CATEGORIES = gating._rebuild_categories()
    assert sorted(gating._THEMED_CATEGORIES["SYSTEM"]) == sorted(before)
