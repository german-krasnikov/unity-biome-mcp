"""Capability gating — tier-based tool visibility with session enable.

P1: Themed taxonomy + get_catalog() + is_core(). Single source of truth.
Plugin tools are NOT in catalog — discovered dynamically via PluginRegistry.

M8: _CORE_TOOLS/_THEMED_CATEGORIES/TIER1/_ALL_KNOWN are GENERATED from
tool_specs._SPECS at import time (one ToolSpec entry per tool) instead of being
4 independently hand-typed literals that could silently drift out of sync with
each other. _CATEGORY_ALIAS (legacy category-name -> themed-group mapping)
stays hand-typed — it is category-level metadata, not a per-tool attribute.
"""
from .tool_specs import _SPECS

# ---------------------------------------------------------------------------
# Themed category keys — includes categories with zero tools today (CONNECTION,
# PLUGINS) so register_tools()/get_catalog() can still find/populate them. This
# list of KEYS is category-level metadata, not per-tool — doesn't belong in
# ToolSpec.
# ---------------------------------------------------------------------------

_THEMED_CATEGORY_KEYS: tuple[str, ...] = (
    "SCENE_EDIT", "COMPONENTS", "ANIMATION", "SHADERS_MATERIAL", "VFX", "UI",
    "SCREENSHOTS", "UNIT_TESTS", "RUNTIME", "DEBUG", "ASSETS", "ADVANCED_CODE",
    "SESSION_SKILLS", "CONNECTION", "META", "PROFILING", "RENDERING", "PLUGINS",
)

# ---------------------------------------------------------------------------
# Derived from _SPECS (M8) — was 4 hand-typed literals, now generated once.
# ---------------------------------------------------------------------------

_CORE_TOOLS: frozenset[str] = frozenset(
    name for name, spec in _SPECS.items() if spec.core
)

_THEMED_CATEGORIES: dict[str, list[str]] = {key: [] for key in _THEMED_CATEGORY_KEYS}
for _name in sorted(_SPECS):
    _spec = _SPECS[_name]
    if _spec.category not in ("CORE", "_INTERNAL"):
        _THEMED_CATEGORIES[_spec.category].append(_name)
del _name, _spec

# TIER1: always visible (CORE + themed tools individually promoted to tier1).
TIER1: set[str] = {name for name, spec in _SPECS.items() if spec.core or spec.tier1}

# All known tool names across all tiers (everything except _INTERNAL protocol
# commands, which are not MCP tools).
_ALL_KNOWN: set[str] = {name for name, spec in _SPECS.items() if spec.category != "_INTERNAL"}

# ---------------------------------------------------------------------------
# Backward-compat: CATEGORIES derived from _THEMED_CATEGORIES
# ---------------------------------------------------------------------------

_CATEGORY_ALIAS: dict[str, list[str]] = {
    "object":     ["SCENE_EDIT", "COMPONENTS"],
    "animation":  ["ANIMATION"],
    "asset":      ["ASSETS", "SHADERS_MATERIAL"],
    "advanced":   ["ADVANCED_CODE", "META"],
    "ui":         ["UI", "VFX"],
    "runtime":    ["RUNTIME", "UNIT_TESTS"],
    "connection": ["CONNECTION"],
    "session":    ["SESSION_SKILLS", "SCREENSHOTS"],
    "profiling":  ["PROFILING"],
    "rendering":  ["RENDERING"],
    "debug":      ["DEBUG"],
    "perf":       ["PROFILING", "RENDERING"],
    "plugins":    ["PLUGINS"],
}

def _rebuild_categories() -> dict[str, set[str]]:
    """Rebuild the alias->themed-group view, preserving ad-hoc categories a
    plugin registered via the fallback branch (documented public API — a
    themed-category registration by a DIFFERENT plugin must not wipe them)."""
    rebuilt = {
        alias: set().union(*(set(_THEMED_CATEGORIES[k]) for k in groups))
        for alias, groups in _CATEGORY_ALIAS.items()
    }
    for key, tools in globals().get("CATEGORIES", {}).items():
        if key not in _CATEGORY_ALIAS:
            rebuilt[key] = tools
    return rebuilt


CATEGORIES: dict[str, set[str]] = _rebuild_categories()

_session_enabled: set[str] = set()


# ---------------------------------------------------------------------------
# P1 API: get_catalog() + is_core()
# ---------------------------------------------------------------------------

def get_catalog() -> dict:
    """Return catalog dict: {categories: {CAT: [tools]}}.

    PUBLIC tools only — never includes plugin/NDA tool names.
    CORE tools appear only in categories["CORE"].
    """
    categories = {cat: list(tools) for cat, tools in _THEMED_CATEGORIES.items()}
    categories["CORE"] = sorted(_CORE_TOOLS)
    return {"categories": categories}


def is_core(name: str) -> bool:
    """True if tool is in the locked CORE group."""
    return name in _CORE_TOOLS


# ---------------------------------------------------------------------------
# Legacy API (unchanged)
# ---------------------------------------------------------------------------

def register_tools(category: str, tools: set) -> None:
    """Plugin self-registration: add tools to a category.

    Plugins do NOT control their own visibility — the platform does. There is no
    tier1= escape hatch: a plugin cannot promote its own tools into the always-on
    TIER1 budget. Registered tools are Tier2 (category-gated, hidden by default,
    reachable via discover_tools()).

    CATEGORIES is a derived view of _THEMED_CATEGORIES (see _rebuild_categories) —
    mutate _THEMED_CATEGORIES only, then re-derive, so the two can never drift.
    """
    global CATEGORIES
    _ALL_KNOWN.update(tools)
    themed_key = category.upper()
    if themed_key not in _THEMED_CATEGORIES:
        # category not a themed key (e.g. legacy alias not backed by a themed
        # group) — fall back to direct CATEGORIES write, same as before.
        CATEGORIES.setdefault(category, set()).update(tools)
        return
    _THEMED_CATEGORIES[themed_key] = list(set(_THEMED_CATEGORIES[themed_key]) | set(tools))
    CATEGORIES = _rebuild_categories()


def reset() -> None:
    _session_enabled.clear()


def get_categories() -> dict[str, set[str]]:
    return CATEGORIES


def is_visible(name: str) -> bool:
    if name in TIER1:
        return True
    return name in _session_enabled


def enable_category(category: str) -> list[str]:
    if category not in CATEGORIES:
        raise ValueError(f"Unknown category: '{category}'. Valid: {sorted(CATEGORIES)}")
    names = CATEGORIES[category]
    _session_enabled.update(names)
    return sorted(names)


def is_deferred(name: str) -> bool:
    """True if tool should have schema deferred: known but not in _CORE_TOOLS."""
    return name in _ALL_KNOWN and name not in _CORE_TOOLS


def filter_by_tier(tools: list) -> list:
    """Keep TIER1 + session-enabled + unknown (plugin) tools."""
    return [t for t in tools if t.name not in _ALL_KNOWN or is_visible(t.name)]


async def discover_tools(category: str | None = None, enable: bool = True) -> str:
    """Find and enable tools by category.
    Categories: object, animation, asset, advanced, ui, runtime, connection, session.
    Pass enable=False to browse without enabling."""
    if category is None:
        lines = [f"{k}: {', '.join(sorted(v))}" for k, v in CATEGORIES.items()]
        return "\n".join(lines)
    if category not in CATEGORIES:
        raise ValueError(f"Unknown category: '{category}'. Valid: {sorted(CATEGORIES)}")
    names = sorted(CATEGORIES[category])
    if enable:
        _session_enabled.update(CATEGORIES[category])
    return f"Category '{category}': {', '.join(names)}"


discover_tools.__test__ = False  # prevent pytest collection
