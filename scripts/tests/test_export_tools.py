"""TDD tests for scripts/export_tools.py — no real unity_mcp import."""
import json
import pathlib
import sys
import types

import pytest

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"


# ---------------------------------------------------------------------------
# Fake objects that stand in for FastMCP internals
# ---------------------------------------------------------------------------

class FakeTool:
    def __init__(self, desc: str, params: dict, title: str = ""):
        self.description = desc
        self.parameters = params
        self.title = title or desc


FAKE_SPECS = {
    "batch":   {"category": "CORE"},
    "ping":    {"category": "_INTERNAL"},
    "zebra":   {"category": "SCENE"},
    "alpha":   {"category": "SCENE"},
    "get_version": {"category": "_INTERNAL"},
}

FAKE_TOOLS = {
    "batch":       FakeTool("Run batch ops", {"type": "object", "properties": {"ops": {}}}),
    "ping":        FakeTool("Ping Unity", {}),
    "zebra":       FakeTool("Zebra tool", {"type": "object", "properties": {}}),
    "alpha":       FakeTool("Alpha tool", {"type": "object", "properties": {}}),
    "get_version": FakeTool("Get version", {}),
}


class FakeToolManager:
    _tools = FAKE_TOOLS


class FakeMcp:
    _tool_manager = FakeToolManager()


class FakeMcpNoManager:
    """Simulates FastMCP API drift — no _tool_manager."""
    pass


def _inject_fake_modules(mcp_obj=None):
    """Inject fake unity_mcp modules into sys.modules."""
    if mcp_obj is None:
        mcp_obj = FakeMcp()

    # fake unity_mcp package
    pkg = types.ModuleType("unity_mcp")
    sys.modules["unity_mcp"] = pkg

    # fake unity_mcp.server
    server_mod = types.ModuleType("unity_mcp.server")
    server_mod.mcp = mcp_obj
    sys.modules["unity_mcp.server"] = server_mod
    pkg.server = server_mod

    # fake unity_mcp.tools package
    tools_pkg = types.ModuleType("unity_mcp.tools")
    sys.modules["unity_mcp.tools"] = tools_pkg
    pkg.tools = tools_pkg

    # fake unity_mcp.tools.tool_specs
    specs_mod = types.ModuleType("unity_mcp.tools.tool_specs")

    class FakeToolSpec:
        def __init__(self, category="CORE", **_):
            self.category = category

    specs_mod._SPECS = {k: FakeToolSpec(category=v["category"]) for k, v in FAKE_SPECS.items()}
    sys.modules["unity_mcp.tools.tool_specs"] = specs_mod
    tools_pkg.tool_specs = specs_mod


def _load_export_tools(mcp_obj=None):
    """Load (or reload) export_tools with fake modules injected."""
    _inject_fake_modules(mcp_obj)
    # Remove cached module so reimport picks up fakes
    sys.modules.pop("export_tools", None)
    if str(SCRIPTS_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPTS_DIR))
    import export_tools  # noqa: PLC0415
    return export_tools


_FAKE_MODULE_KEYS = (
    "unity_mcp",
    "unity_mcp.server",
    "unity_mcp.tools",
    "unity_mcp.tools.tool_specs",
    "export_tools",
)


@pytest.fixture(autouse=True)
def _restore_sys_modules():
    """`_inject_fake_modules` shadows the real `unity_mcp` package name in
    `sys.modules`. Restore pre-test state so other test files sharing this
    process (e.g. install/tests importing the real package) are not polluted.
    """
    saved = {key: sys.modules.get(key) for key in _FAKE_MODULE_KEYS}
    yield
    for key, mod in saved.items():
        if mod is None:
            sys.modules.pop(key, None)
        else:
            sys.modules[key] = mod


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_toolsmith_format_structure():
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    assert "tools" in result
    assert isinstance(result["tools"], list)


def test_mcplint_format_bare_array():
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="mcplint"))
    assert isinstance(result, list)


def test_internal_tools_excluded():
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    names = {t["name"] for t in result["tools"]}
    assert "ping" not in names
    assert "get_version" not in names
    assert "batch" in names


def test_tools_sorted_by_name():
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    names = [t["name"] for t in result["tools"]]
    assert names == sorted(names)


def test_each_tool_has_required_fields():
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    for tool in result["tools"]:
        assert "name" in tool, f"missing 'name' in {tool}"
        assert "title" in tool, f"missing 'title' in {tool}"
        assert "description" in tool, f"missing 'description' in {tool}"
        assert "inputSchema" in tool, f"missing 'inputSchema' in {tool}"


def test_tool_title_matches_tool_object():
    """title field must come from tool.title, not be derived inline."""
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    by_name = {t["name"]: t for t in result["tools"]}
    assert by_name["batch"]["title"] == FAKE_TOOLS["batch"].title


def test_missing_tool_manager_raises():
    mod = _load_export_tools(mcp_obj=FakeMcpNoManager())
    with pytest.raises(RuntimeError, match="_tool_manager"):
        mod.export_json(fmt="toolsmith")


def test_main_out_file(tmp_path):
    mod = _load_export_tools()
    out = tmp_path / "out.json"
    mod.main(["--format", "toolsmith", "--out", str(out)])
    assert out.exists()
    data = json.loads(out.read_text(encoding="utf-8"))
    assert "tools" in data


# ── G50: versioned catalog ─────────────────────────────────────────────────────

def test_toolsmith_format_has_version_field():
    """Toolsmith export must include a version field for schema drift detection."""
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="toolsmith"))
    assert "version" in result, "toolsmith format must include a version field"
    assert isinstance(result["version"], str)
    assert len(result["version"]) > 0


def test_toolsmith_version_is_stable():
    """version field is deterministic across two calls (same specs → same hash)."""
    mod = _load_export_tools()
    v1 = json.loads(mod.export_json(fmt="toolsmith"))["version"]
    v2 = json.loads(mod.export_json(fmt="toolsmith"))["version"]
    assert v1 == v2


def test_mcplint_format_has_no_version_field():
    """mcplint format is a bare array — no version wrapper."""
    mod = _load_export_tools()
    result = json.loads(mod.export_json(fmt="mcplint"))
    assert isinstance(result, list)  # bare array, no version
