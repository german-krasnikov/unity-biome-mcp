"""Capability gating — tier-based tool visibility with session enable.

P1: Themed taxonomy + get_catalog() + is_core(). Single source of truth.
Plugin tools are NOT in catalog — discovered dynamically via PluginRegistry.

M8: _CORE_TOOLS/_THEMED_CATEGORIES/TIER1/_ALL_KNOWN are GENERATED from
tool_specs._SPECS at import time (one ToolSpec entry per tool) instead of being
4 independently hand-typed literals that could silently drift out of sync with
each other. _CATEGORY_ALIAS (legacy category-name -> themed-group mapping)
stays hand-typed — it is category-level metadata, not a per-tool attribute.

P-12440 Phase 1 surface: 13 CORE + 20 TIER1-only = 33 always-visible.
"""
from .tool_specs import _SPECS

# ---------------------------------------------------------------------------
# Themed category keys — includes categories with zero tools today (CONNECTION,
# PLUGINS) so register_tools()/get_catalog() can still find/populate them. This
# list of KEYS is category-level metadata, not per-tool — doesn't belong in
# ToolSpec.
# ---------------------------------------------------------------------------

_THEMED_CATEGORY_KEYS: tuple[str, ...] = (
    "SCENE", "COMPONENTS", "ASSETS", "MEDIA",
    "VERIFY", "RUNTIME", "TESTS", "SYSTEM",
    "UGUI", "UITOOLKIT",
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
    if _spec.category not in ("CORE", "_INTERNAL", "DEPRECATED"):
        _THEMED_CATEGORIES[_spec.category].append(_name)
del _name, _spec

# TIER1: always visible (CORE + themed tools individually promoted to tier1).
TIER1: set[str] = {name for name, spec in _SPECS.items() if spec.core or spec.tier1}

# All known tool names across all tiers (everything except _INTERNAL protocol
# commands). DEPRECATED stubs are included so filter_by_tier can gate them —
# they're not TIER1 and not session-enabled, so is_visible() returns False and
# they are hidden in ListTools but remain callable by name (MCP091-011).
_ALL_KNOWN: set[str] = {name for name, spec in _SPECS.items()
                        if spec.category != "_INTERNAL"}

# Python-only tools: have no C# CommandRegistry handler; never sent to Unity.
_DIRECT_ONLY: frozenset[str] = frozenset(
    name for name, spec in _SPECS.items() if spec.direct_only
)

# ---------------------------------------------------------------------------
# Backward-compat: CATEGORIES derived from _THEMED_CATEGORIES
# ---------------------------------------------------------------------------

_CATEGORY_ALIAS: dict[str, list[str]] = {
    # --- legacy human aliases ---
    "object":       ["SCENE", "COMPONENTS"],
    "animation":    ["MEDIA"],
    "asset":        ["ASSETS"],
    "advanced":     ["SYSTEM"],
    "ui":           ["UGUI", "UITOOLKIT"],
    "ugui":         ["UGUI"],
    "uitoolkit":    ["UITOOLKIT"],
    "runtime":      ["RUNTIME", "TESTS"],
    "connection":   ["SYSTEM"],
    "session":      ["SYSTEM"],
    "profiling":    ["RUNTIME"],
    "rendering":    ["MEDIA"],
    "debug":        ["RUNTIME"],
    "perf":         ["RUNTIME"],
    "plugins":      ["SYSTEM"],
}


def _rebuild_categories() -> dict[str, set[str]]:
    """Rebuild the alias->themed-group view, preserving ad-hoc categories a
    plugin registered via the fallback branch (documented public API — a
    themed-category registration by a DIFFERENT plugin must not wipe them).

    Also includes the 8 themed keys directly so discover_tools("SCENE") etc. work.
    """
    rebuilt = {
        alias: set().union(*(set(_THEMED_CATEGORIES[k]) for k in groups))
        for alias, groups in _CATEGORY_ALIAS.items()
    }
    # Include each themed key directly so new-style category names work in CATEGORIES
    for key in _THEMED_CATEGORIES:
        rebuilt[key] = set(_THEMED_CATEGORIES[key])
    rebuilt.update({
        key: tools
        for key, tools in globals().get("CATEGORIES", {}).items()
        if key not in _CATEGORY_ALIAS and key not in _THEMED_CATEGORIES
    })
    return rebuilt


CATEGORIES: dict[str, set[str]] = _rebuild_categories()

_session_enabled: set[str] = set()


# ---------------------------------------------------------------------------
# P1 API: get_catalog() + is_core()
# ---------------------------------------------------------------------------

def get_catalog() -> dict:
    """Return catalog dict: {categories: {CAT: [tools]}}.

    PUBLIC tools only — never includes plugin/NDA tool names.
    CORE tools appear only in categories["CORE"], not in their category bucket.
    """
    categories = {
        cat: [t for t in tools if t not in _CORE_TOOLS and t not in _DIRECT_ONLY]
        for cat, tools in _THEMED_CATEGORIES.items()
    }
    categories["CORE"] = sorted(_CORE_TOOLS - _DIRECT_ONLY)
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
    # Resolve alias (e.g. "debug" → "RUNTIME") via _CATEGORY_ALIAS (keys are lowercase)
    alias_key = category.lower()
    if themed_key not in _THEMED_CATEGORIES and alias_key in _CATEGORY_ALIAS:
        for mapped in _CATEGORY_ALIAS[alias_key]:
            if mapped in _THEMED_CATEGORIES:
                themed_key = mapped
                break
    if themed_key not in _THEMED_CATEGORIES:
        # category not a themed key — fall back to direct CATEGORIES write.
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


def _tool_surface_line(name: str) -> str:
    spec = _SPECS.get(name)
    if spec is None:
        return f"  {name}"
    tags = []
    if spec.core: tags.append("core")
    elif spec.tier1: tags.append("tier1")
    surfaces = "direct" if spec.direct_only else "direct,batch"
    parts = [f"  {name}"]
    if tags: parts.append(" ".join(tags))
    parts.append(f"surfaces={surfaces}")
    if spec.mutability:
        parts.append(f"mutability={spec.mutability}")
    return "  ".join(parts)


async def discover_tools(category: str | None = None, enable: bool = True,
                         include_legacy: bool = False, structured: bool = False) -> str:
    """Find and enable tools by category.
    Canonical categories: SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM.
    Legacy aliases (object, animation, etc.) available with include_legacy=True.
    structured=True adds surface/mutability info per tool. enable=False to browse only."""
    if category is None:
        keys = list(CATEGORIES.keys()) if include_legacy else list(_THEMED_CATEGORY_KEYS)
        if structured:
            parts = []
            for k in keys:
                tools = sorted(CATEGORIES.get(k, []))
                parts.append(f"{k}:")
                parts.extend(_tool_surface_line(t) for t in tools)
            return "\n".join(parts)
        lines = [f"{k}: {', '.join(sorted(CATEGORIES.get(k, [])))}" for k in keys]
        return "\n".join(lines)
    if category not in CATEGORIES:
        raise ValueError(f"Unknown category: '{category}'. Valid: {sorted(CATEGORIES)}")
    names = sorted(CATEGORIES[category])
    if enable:
        _session_enabled.update(CATEGORIES[category])
    return f"Category '{category}': {', '.join(names)}"


discover_tools.__test__ = False  # prevent pytest collection
