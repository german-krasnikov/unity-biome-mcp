"""N1a checklist (5) / T4: one external fixture module with read+write tools,
loaded through the PRODUCTION external-plugin-directory discovery path — not
by calling _atomic.register_plugin_module() directly on an in-memory module
object (that bypasses discovery entirely, and is what every other file in
this package does).

Production path exercised: unity_mcp.plugins._load_plugin_dirs()
(server/src/unity_mcp/plugins/__init__.py:92-113), the UNITY_MCP_PLUGIN_DIRS
branch of unity_mcp.plugins.load_plugins() (called at
server/src/unity_mcp/plugins/__init__.py:29, from server startup at
server/src/unity_mcp/server.py:663). It really does: read the env var, list
.py files in that directory with pkgutil.iter_modules(), and import_module()
each one — the only difference from server startup is which directory the
env var points at, so a tmp_path + monkeypatch.setenv here exercises the
exact same code, no repo file is written, and nothing is mocked.

_load_plugin_dirs() is called directly (not the full load_plugins()) so this
test isn't also re-loading every built-in plugin onto a fresh mcp instance —
that would drown the two fixture-tool assertions in unrelated noise. It is
still the literal production function for this discovery source, not a
substitute for one.
"""
from unity_mcp.plugins import _atomic, _load_plugin_dirs
from unity_mcp.server import _UnstructuredMCP
from unity_mcp.tools import gating

_OK_SOURCE = '''
def register(mcp, send, args):
    from unity_mcp.plugin_api import register_read_cmds, register_write_cmds

    @mcp.tool()
    def ext_fixture_read_tool():
        return "ext-read-value"

    @mcp.tool()
    def ext_fixture_write_tool():
        return "ext-write-value"

    register_read_cmds("ext_fixture_read_tool")
    register_write_cmds("ext_fixture_write_tool")
'''

# Same shape as _OK_SOURCE, plus a third declaration for a real builtin name
# ("get_hierarchy") the fixture never registers via guarded.tool() — foreign
# at commit-time validation, so the whole fixture must be rejected.
_REJECTED_SOURCE = '''
def register(mcp, send, args):
    from unity_mcp.plugin_api import register_read_cmds, register_write_cmds

    @mcp.tool()
    def ext_fixture_bad_read_tool():
        return "bad-read"

    @mcp.tool()
    def ext_fixture_bad_write_tool():
        return "bad-write"

    register_read_cmds("ext_fixture_bad_read_tool")
    register_write_cmds("ext_fixture_bad_write_tool")
    register_read_cmds("get_hierarchy")
'''


def _load_external_dir_plugin(tmp_path, monkeypatch, module_name, source, mcp):
    """Write `source` as an external plugin-dir module and run it through the
    real UNITY_MCP_PLUGIN_DIRS discovery path. Cleans up the sys.path entry
    and sys.modules cache it creates, so no state leaks to later tests."""
    import contextlib
    import sys

    (tmp_path / f"{module_name}.py").write_text(source, encoding="utf-8")
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    try:
        _load_plugin_dirs(mcp, lambda *a, **k: None, lambda *a, **k: None)
    finally:
        sys.modules.pop(module_name, None)
        with contextlib.suppress(ValueError):
            sys.path.remove(str(tmp_path))


async def test_external_fixture_module_read_write_no_core_edits(tmp_path, monkeypatch):
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.middleware import READ_CMDS, WRITE_CMDS

    mcp = _UnstructuredMCP("test_ext_fixture_ok")

    def host_fn():
        return "host"
    mcp.tool()(host_fn)
    original_host_tool = mcp._tool_manager._tools["host_fn"]

    builtin_names_before = gating._BUILTIN_NAMES
    all_known_before = set(gating._ALL_KNOWN)
    read_before, write_before = set(READ_CMDS), set(WRITE_CMDS)

    try:
        _load_external_dir_plugin(
            tmp_path, monkeypatch, "fixture_ext_rw_ok", _OK_SOURCE, mcp
        )

        # 1. both tools present in the FastMCP host manager...
        assert "ext_fixture_read_tool" in mcp._tool_manager._tools
        assert "ext_fixture_write_tool" in mcp._tool_manager._tools

        # ...and callable through the public list/invoke path.
        tools = await mcp.list_tools()
        names = {t.name for t in tools}
        assert {"ext_fixture_read_tool", "ext_fixture_write_tool"} <= names

        read_result = await mcp.call_tool("ext_fixture_read_tool", {})
        write_result = await mcp.call_tool("ext_fixture_write_tool", {})
        assert read_result[0].text == "ext-read-value"
        assert write_result[0].text == "ext-write-value"

        # 2. READ_CMDS/WRITE_CMDS grew by exactly the fixture's names, correctly classified.
        assert READ_CMDS - read_before == {"ext_fixture_read_tool"}
        assert WRITE_CMDS - write_before == {"ext_fixture_write_tool"}
        assert "ext_fixture_write_tool" not in READ_CMDS
        assert "ext_fixture_read_tool" not in WRITE_CMDS

        # 3. _command_owner maps both to the fixture identity (module base name).
        assert _atomic._command_owner["ext_fixture_read_tool"] == "fixture_ext_rw_ok"
        assert _atomic._command_owner["ext_fixture_write_tool"] == "fixture_ext_rw_ok"

        # 4. gating._BUILTIN_NAMES unchanged (identity — a frozenset, so `is` already
        # implies content equality) — never mutated.
        assert gating._BUILTIN_NAMES is builtin_names_before

        # 5. _ALL_KNOWN grew only by the two fixture names (auto-gated into "plugins").
        assert gating._ALL_KNOWN - all_known_before == {
            "ext_fixture_read_tool", "ext_fixture_write_tool",
        }

        # 6. host builtin handler identity untouched.
        assert mcp._tool_manager._tools["host_fn"] is original_host_tool

        # 7. no failures recorded.
        assert _atomic.get_failed_plugins() == []
    finally:
        READ_CMDS.discard("ext_fixture_read_tool")
        WRITE_CMDS.discard("ext_fixture_write_tool")


async def test_external_fixture_module_foreign_name_rejects_whole_fixture(tmp_path, monkeypatch):
    """Negative control: same fixture shape, plus a third declaration for a
    builtin name it never registered via guarded.tool(). The whole fixture
    must be rejected — both of its own tools absent, tables restored exactly."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.middleware import READ_CMDS, WRITE_CMDS

    mcp = _UnstructuredMCP("test_ext_fixture_rejected")

    all_known_before = set(gating._ALL_KNOWN)
    read_before, write_before = set(READ_CMDS), set(WRITE_CMDS)
    owner_before = dict(_atomic._command_owner)

    try:
        _load_external_dir_plugin(
            tmp_path, monkeypatch, "fixture_ext_rw_rejected", _REJECTED_SOURCE, mcp
        )

        # Both of the fixture's own tools absent — not a partial, filtered load.
        assert "ext_fixture_bad_read_tool" not in mcp._tool_manager._tools
        assert "ext_fixture_bad_write_tool" not in mcp._tool_manager._tools

        tools = await mcp.list_tools()
        names = {t.name for t in tools}
        assert "ext_fixture_bad_read_tool" not in names
        assert "ext_fixture_bad_write_tool" not in names

        # Tables restored to their exact pre-load state — no leaked entries.
        assert read_before == READ_CMDS
        assert write_before == WRITE_CMDS
        assert all_known_before == gating._ALL_KNOWN
        assert owner_before == _atomic._command_owner

        failed = _atomic.get_failed_plugins()
        assert len(failed) == 1
        assert failed[0][0] == "fixture_ext_rw_rejected"
        assert "get_hierarchy" in failed[0][1]
    finally:
        READ_CMDS.discard("ext_fixture_bad_read_tool")
        WRITE_CMDS.discard("ext_fixture_bad_write_tool")
