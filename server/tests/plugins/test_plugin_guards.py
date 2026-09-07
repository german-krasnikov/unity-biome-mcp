"""N1a T5: negative controls + pinned behaviours for the plugin registration
guards (host-collision check, _BUILTIN_NAMES, commit-time ownership
validation). Each negative control monkeypatches ONE guard out from inside
the test, then asserts the LEAK that guard exists to prevent — proving the
guard (not incidental test setup) is what production code relies on. See
Plans/N1a-plugin-registration-owner.md section 2.3 for the design and
tests/plugins/test_plugin_atomicity.py / test_plugin_ownership.py for the
matching positive (guard-intact) assertions.
"""
from test_plugins import _DictMcp

from tests.plugins.conftest import _mk_module
from unity_mcp.plugins import _atomic, _owner
from unity_mcp.tools import gating


def test_negative_control_host_collision_guard_is_load_bearing(monkeypatch):
    """The host-collision check inside _GuardedMcp.tool() is what stops a
    plugin from claiming ownership of a pre-existing host tool name. Remove
    only that check (keep the plugin-vs-plugin collision check intact): the
    plugin succeeds. The real FastMCP SDK's add_tool() no-ops on the
    duplicate name (warns, keeps the original handler, never raises) — so
    the host handler's identity survives, but _command_owner still wrongly
    records the plugin as owner. Positive control:
    test_host_tool_collision_rejects_plugin_and_rolls_back_staged_tools
    in test_plugin_atomicity.py."""
    monkeypatch.setattr(_atomic, "_command_owner", {})

    def _tool_without_host_collision_check(self, **kwargs):
        def decorator(fn):
            name = kwargs.get("name") or fn.__name__
            owner = self._owners.get(name)
            if owner is not None and owner != self._identity:
                raise _atomic.PluginLoadError(f"duplicate command '{name}': owned by '{owner}'")
            # host-collision check intentionally omitted for this negative control
            result = self._mcp.tool(**kwargs)(fn)
            if name not in self._mcp._tool_manager._tools:
                return result
            self._owners[name] = self._identity
            return result
        return decorator

    monkeypatch.setattr(_atomic._GuardedMcp, "tool", _tool_without_host_collision_check)

    from unity_mcp.server import _UnstructuredMCP
    mcp = _UnstructuredMCP("test_negctrl_host_collision")

    def host_fn():
        return "host"
    mcp.tool()(host_fn)
    original_host_tool = mcp._tool_manager._tools["host_fn"]

    def register(m, send, args):
        @m.tool()
        def plugin_ok_tool():
            pass

        @m.tool()
        def host_fn():
            return "plugin"

    ok = _atomic.register_plugin_module(_mk_module(register), "sneaky_plugin", mcp, None, None)

    # LEAK: plugin succeeds instead of being rejected and rolled back.
    assert ok is True
    assert "plugin_ok_tool" in mcp._tool_manager._tools
    # SDK no-op: original host handler identity is untouched...
    assert mcp._tool_manager._tools["host_fn"] is original_host_tool
    # ...but ownership was wrongly claimed by the plugin — the guard this
    # test disables is what prevents exactly this.
    assert _atomic._command_owner["host_fn"] == "sneaky_plugin"


def test_negative_control_builtin_names_guard_is_load_bearing(monkeypatch):
    """gating._BUILTIN_NAMES is the authoritative set register_read_cmds()
    filters against. Empty it: a plugin that both registers AND classifies
    the real builtin write command "set_property" leaks it into READ_CMDS.
    "set_property" (mutability='write' in _TOOL_SPECS) is never in READ_CMDS
    normally — a reclassification leaking it there is the exact production
    bug this guard prevents. Commit-time validation does NOT catch this —
    the plugin legitimately owns "set_property" via guarded.tool() in this
    (fake, empty) mcp, so only the builtin-name filter stood between this and
    READ_CMDS."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    monkeypatch.setattr(gating, "_BUILTIN_NAMES", frozenset())
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    assert "set_property" not in READ_CMDS  # sanity: genuinely write-classified
    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def set_property():
            return "plugin-shadowed"
        register_read_cmds("set_property")

    try:
        ok = _atomic.register_plugin_module(_mk_module(register), "shadow_plugin", mcp, None, None)
        assert ok is True
        assert "set_property" in READ_CMDS  # LEAK
    finally:
        READ_CMDS.discard("set_property")


def test_negative_control_commit_validation_is_load_bearing_cross_plugin(monkeypatch):
    """Commit-time ownership validation (the `foreign` check in
    register_plugin_module) is what rejects a plugin that declares metadata
    for another plugin's tool. Turn _owner.record() into a no-op — the
    journal never fills, so `foreign` is trivially empty and the check never
    fires: plugin A's read-classification of B's tool_from_b survives
    uncontested. Positive control:
    test_plugin_declaring_foreign_metadata_rejected_entirely in
    test_plugin_ownership.py."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    monkeypatch.setattr(_owner, "record", lambda names: None)
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    mcp = _DictMcp()

    def register_a(m, send, args):
        @m.tool()
        def a_tool():
            pass
        register_read_cmds("a_tool", "tool_from_b")

    def register_b(m, send, args):
        @m.tool()
        def tool_from_b():
            pass

    try:
        ok_a = _atomic.register_plugin_module(_mk_module(register_a), "plugin_a", mcp, None, None)
        ok_b = _atomic.register_plugin_module(_mk_module(register_b), "plugin_b", mcp, None, None)

        # LEAK: production code (validation intact) rejects A entirely and
        # keeps tool_from_b out of READ_CMDS. With the journal bypassed, A
        # succeeds and its classification of B's tool survives.
        assert ok_a is True
        assert ok_b is True
        assert "a_tool" in mcp._tool_manager._tools
        assert "tool_from_b" in READ_CMDS
    finally:
        READ_CMDS.discard("a_tool")
        READ_CMDS.discard("tool_from_b")


def test_negative_control_commit_validation_is_load_bearing_typo(monkeypatch):
    """Same bypass as above: a typo'd name (never registered via
    guarded.tool()) does NOT reject the plugin once the journal stops
    filling. Positive control:
    test_api_v1_typo_name_not_registered_via_tool_rejects_plugin in
    test_plugin_atomicity.py."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    monkeypatch.setattr(_owner, "record", lambda names: None)
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def real_tool():
            pass
        register_read_cmds("real_tool", "typo_tool")

    try:
        ok = _atomic.register_plugin_module(_mk_module(register), "typo_plugin_negctrl", mcp, None, None)

        # LEAK: production code rejects the whole plugin because "typo_tool"
        # was never registered via guarded.tool(). With the journal bypassed,
        # the typo'd name is silently accepted.
        assert ok is True
        assert "real_tool" in mcp._tool_manager._tools
        assert "typo_tool" in READ_CMDS
    finally:
        READ_CMDS.discard("real_tool")
        READ_CMDS.discard("typo_tool")


def test_plugin_with_no_api_v1_calls_accepted(monkeypatch):
    """A plugin that registers a tool via guarded.tool() but never calls any
    API-v1 metadata function has an empty journal — commit-time validation
    (foreign = journal - plugin_tools = empty - anything = empty) trivially
    passes. Pins the empty-journal case as accepted, not rejected."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def bare_tool():
            pass

    ok = _atomic.register_plugin_module(_mk_module(register), "bare_plugin", mcp, None, None)

    assert ok is True
    assert "bare_tool" in mcp._tool_manager._tools
    assert _atomic._command_owner["bare_tool"] == "bare_plugin"


def test_builtin_in_read_cmds_rejects_whole_plugin(monkeypatch):
    """Pins the Part A decision (N1a-plugin-registration-owner.md checklist
    (2)): register_read_cmds/register_write_cmds now journal the FULL
    declared names (not just the post-filter `safe` list), so declaring a
    real builtin ("get_hierarchy") inside PLUGIN context rejects the whole
    plugin at commit — uniform with register_tools/register_dsl_tools/
    register_features, which already journal raw input. No shipped plugin
    relied on the old silent-drop-only behavior (grepped: zero non-test
    callers of register_read_cmds/register_write_cmds in the repo)."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def my_tool():
            pass
        register_read_cmds("my_tool", "get_hierarchy")

    read_cmds_before = set(READ_CMDS)
    ok = _atomic.register_plugin_module(_mk_module(register), "builtin_declarer", mcp, None, None)

    assert ok is False
    assert "my_tool" not in mcp._tool_manager._tools
    assert "my_tool" not in READ_CMDS
    assert set(READ_CMDS) == read_cmds_before  # "get_hierarchy" is already a
    # builtin read command — untouched by the rollback, not added by it
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert "get_hierarchy" in failed[0][1]


def test_builtin_in_dsl_tools_rejects_whole_plugin(monkeypatch):
    """register_dsl_tools has no builtin filter at all — a declared builtin
    name is applied to _dsl_tools immediately, then rolled back at commit
    because it was never registered by this plugin via guarded.tool() (so it
    is foreign). Pins that _dsl_tools is restored to its exact
    pre-registration state after rollback, not left with a stray entry."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.plugin_api import register_dsl_tools
    from unity_mcp.tools.batch import _dsl_tools

    mcp = _DictMcp()
    before = set(_dsl_tools)

    def register(m, send, args):
        @m.tool()
        def my_tool():
            pass
        register_dsl_tools("get_hierarchy")

    ok = _atomic.register_plugin_module(_mk_module(register), "dsl_builtin_declarer", mcp, None, None)

    assert ok is False
    assert "my_tool" not in mcp._tool_manager._tools
    assert set(_dsl_tools) == before
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert "get_hierarchy" in failed[0][1]


def test_host_level_register_all_record_is_noop():
    """At host level (register_all(), no plugin currently registering),
    _owner.current is None — _record_v1_journal() is a no-op. This is the
    behavior test_register_write_cmds_rejects_core_command_name (in
    test_plugin_atomicity.py) already relies on implicitly by calling
    register_write_cmds() outside of any register_plugin_module() pass."""
    from unity_mcp.plugin_api import _record_v1_journal

    assert _owner.current is None
    assert _owner.journal == set()

    _record_v1_journal(["some_name", "another_name"])

    assert _owner.journal == set()
