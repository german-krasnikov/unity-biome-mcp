"""TDD: Python-authoritative tool catalog (P1 Part A).

Tests:
1. get_catalog() returns expected structure
2. CORE tools are locked (is_core)
3. Every public tool is in exactly one category (drift guard)
4. No plugin/NDA tool names in catalog
5. _push_catalog sends set_tool_catalog with plain-text catalog
"""
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _get_gating():
    import importlib
    import unity_mcp.tools.gating as g
    importlib.reload(g)
    return g


# ---------------------------------------------------------------------------
# Cycle 1: get_catalog() structure
# ---------------------------------------------------------------------------

def test_get_catalog_returns_dict_with_categories():
    from unity_mcp.tools.gating import get_catalog
    cat = get_catalog()
    assert isinstance(cat, dict)
    assert "categories" in cat
    assert "core" not in cat


def test_get_catalog_categories_are_dict_of_lists():
    from unity_mcp.tools.gating import get_catalog
    cats = get_catalog()["categories"]
    assert isinstance(cats, dict)
    for name, tools in cats.items():
        assert isinstance(tools, list), f"category {name!r} should be a list"


# ---------------------------------------------------------------------------
# Cycle 2: taxonomy categories present
# ---------------------------------------------------------------------------

EXPECTED_CATEGORIES = [
    "CORE", "SCENE", "COMPONENTS", "ASSETS", "MEDIA",
    "VERIFY", "RUNTIME", "TESTS", "SYSTEM",
]


def test_all_expected_categories_present():
    from unity_mcp.tools.gating import get_catalog
    cats = get_catalog()["categories"]
    for cat in EXPECTED_CATEGORIES:
        assert cat in cats, f"Missing category: {cat}"


def test_core_category_has_expected_tools():
    """Phase 1a: delete_object/scene/search_scene moved to SCENE tier1 (not CORE)."""
    from unity_mcp.tools.gating import get_catalog
    core_tools = get_catalog()["categories"]["CORE"]
    for tool in ("get_hierarchy", "get_component", "inspect", "batch", "set_property",
                 "create_object", "manage_component"):
        assert tool in core_tools, f"Expected {tool!r} in CORE"
    for tool in ("delete_object", "scene", "search_scene"):
        assert tool not in core_tools, f"{tool!r} should be SCENE tier1, not CORE"


# ---------------------------------------------------------------------------
# Cycle 3: is_core()
# ---------------------------------------------------------------------------

def test_is_core_returns_true_for_core_tools():
    from unity_mcp.tools.gating import is_core
    for tool in ("get_hierarchy", "batch", "inspect", "set_property"):
        assert is_core(tool), f"{tool!r} should be core"


def test_is_core_returns_false_for_non_core():
    from unity_mcp.tools.gating import is_core
    for tool in ("animation", "shader", "execute_code", "save_skill"):
        assert not is_core(tool), f"{tool!r} should NOT be core"


# ---------------------------------------------------------------------------
# Cycle 4: catalog holds only first-party (public) tools, no external plugins
# ---------------------------------------------------------------------------

# A tool is "public" iff its function lives in the unity_mcp package. External
# plugins register from their own packages — identified generically by module,
# so this guard needs no hardcoded plugin/tool-name literals.
_PUBLIC_PKG = "unity_mcp"


def _public_tool_names() -> set[str]:
    """Names of registered tools whose implementation is first-party."""
    from unity_mcp import server
    out = set()
    for name, tool in server.mcp._tool_manager._tools.items():
        fn = getattr(tool, "fn", None) or getattr(tool, "func", None)
        mod = getattr(fn, "__module__", "") or ""
        if mod.split(".")[0] == _PUBLIC_PKG:
            out.add(name)
    return out


def test_catalog_contains_only_public_tools():
    """Every tool listed in the catalog must be a first-party tool."""
    from unity_mcp.tools.gating import get_catalog
    cat = get_catalog()
    all_cat_tools = {t for tools in cat["categories"].values() for t in tools}
    external = all_cat_tools - _public_tool_names()
    assert not external, f"Non-public tools leaked into catalog: {sorted(external)}"


# ---------------------------------------------------------------------------
# Cycle 5: every tool in exactly one category (drift guard)
# ---------------------------------------------------------------------------

def test_no_tool_in_multiple_categories():
    """Each tool appears in at most one category."""
    from unity_mcp.tools.gating import get_catalog
    cat = get_catalog()
    # CORE is the locked set; also check non-CORE categories for duplicates
    cats = cat["categories"]
    all_tools = []
    for tools in cats.values():
        all_tools.extend(tools)
    # duplicates within non-core categories
    seen = {}
    for t in all_tools:
        assert t not in seen, f"Tool {t!r} appears in multiple categories: {seen[t]} and current"
        seen[t] = True


# budget_status is registered only when the cost tracker is active and is
# meta-infrastructure (not a user-facing scene tool) — intentionally not in the catalog.
_CATALOG_EXEMPT = {"budget_status"}


def test_drift_guard_all_public_tools_in_catalog():
    """Every registered first-party tool must appear in exactly one catalog category."""
    from unity_mcp.tools.gating import get_catalog
    cat = get_catalog()
    all_cat_tools: set = set()
    for tools in cat["categories"].values():
        all_cat_tools.update(tools)

    registered = _public_tool_names() - _CATALOG_EXEMPT
    uncategorized = registered - all_cat_tools
    assert not uncategorized, f"Public tools missing from catalog: {sorted(uncategorized)}"


# ---------------------------------------------------------------------------
# Cycle 6: _push_catalog sends correct command
# ---------------------------------------------------------------------------

async def test_push_catalog_sends_set_tool_catalog():
    from unity_mcp.server import _push_catalog
    mock_bridge = AsyncMock()
    mock_bridge.connected = True
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": ""})
    await _push_catalog(mock_bridge)
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "set_tool_catalog"


async def test_push_catalog_sends_text_format():
    from unity_mcp.server import _push_catalog
    mock_bridge = AsyncMock()
    mock_bridge.connected = True
    captured = {}

    async def fake_send(cmd, args, timeout=5.0):
        captured["cmd"] = cmd
        captured["args"] = args
        return {"ok": True, "data": ""}

    mock_bridge.send = fake_send
    await _push_catalog(mock_bridge)
    assert captured["cmd"] == "set_tool_catalog"
    catalog_str = captured["args"]["catalog"]
    lines = [l for l in catalog_str.splitlines() if l.strip()]
    assert any(l.startswith("CORE:") for l in lines), f"No CORE: line in: {catalog_str!r}"
    # Must NOT be valid JSON
    import json as _json
    with pytest.raises((_json.JSONDecodeError, ValueError)):
        _json.loads(catalog_str)


# no-assert: crash guard
async def test_push_catalog_silent_on_failure():
    """_push_catalog must not raise even if bridge.send fails."""
    from unity_mcp.server import _push_catalog
    mock_bridge = AsyncMock()
    mock_bridge.send = AsyncMock(side_effect=ConnectionError("lost"))
    # Must not raise
    await _push_catalog(mock_bridge)


# ---------------------------------------------------------------------------
# Cycle 7: backward-compat — existing gating still works after refactor
# ---------------------------------------------------------------------------

def test_tier1_tools_still_visible():
    from unity_mcp.tools.gating import filter_by_tier, reset, TIER1
    reset()
    tools = [MagicMock(name=n) for n in list(TIER1)[:5]]
    for t, n in zip(tools, list(TIER1)[:5]):
        t.name = n
    result = filter_by_tier(tools)
    assert len(result) == len(tools)


def test_old_categories_accessible():
    """CATEGORIES dict must still contain animation/runtime for backward-compat."""
    from unity_mcp.tools.gating import CATEGORIES
    assert "animation" in CATEGORIES
    assert "runtime" in CATEGORIES


def test_get_categories_still_works():
    from unity_mcp.tools.gating import get_categories
    cats = get_categories()
    assert len(cats) >= 8  # at least the original 8 categories


# ---------------------------------------------------------------------------
# Cycle 8: _SPECS-based consistency (no server import required)
# ---------------------------------------------------------------------------

def test_all_spec_tools_in_catalog():
    """Every non-_INTERNAL tool in _SPECS must appear in the catalog."""
    from unity_mcp.tools.gating import get_catalog
    from unity_mcp.tools.tool_specs import _SPECS
    catalog_tools = {t for tools in get_catalog()["categories"].values() for t in tools}
    missing = {
        name for name, spec in _SPECS.items()
        if spec.category != "_INTERNAL" and name not in catalog_tools
    }
    assert not missing, f"Tools in _SPECS missing from catalog: {sorted(missing)}"


def test_spec_tools_in_exactly_one_category():
    """Every non-_INTERNAL tool in _SPECS appears in exactly one catalog category."""
    from unity_mcp.tools.gating import get_catalog
    from unity_mcp.tools.tool_specs import _SPECS
    public_names = {n for n, s in _SPECS.items() if s.category != "_INTERNAL"}
    counts: dict[str, int] = {name: 0 for name in public_names}
    for tools in get_catalog()["categories"].values():
        for t in tools:
            if t in counts:
                counts[t] += 1
    duplicates = {n for n, c in counts.items() if c > 1}
    assert not duplicates, f"Tools in multiple categories: {sorted(duplicates)}"


def test_no_phantom_tools_in_catalog():
    """Every tool in the catalog must have a matching _SPECS entry."""
    from unity_mcp.tools.gating import get_catalog
    from unity_mcp.tools.tool_specs import _SPECS
    catalog_tools = {t for tools in get_catalog()["categories"].values() for t in tools}
    phantoms = catalog_tools - set(_SPECS)
    assert not phantoms, f"Phantom tools in catalog (no _SPECS entry): {sorted(phantoms)}"


