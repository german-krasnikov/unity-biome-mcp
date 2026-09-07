"""Stable public API for external plugins. Import from here, not internals."""
from unity_mcp.sampling import SamplingService
from unity_mcp.tools._annotations import DEL, RO, RW, RW_IDEM
from unity_mcp.tools.intent_common import sanitize_intent, strip_fences

API_VERSION = 1

__all__ = [
    "API_VERSION",
    "RO", "RW", "RW_IDEM", "DEL",
    "SamplingService", "strip_fences", "sanitize_intent",
    "register_dsl_tools", "register_read_cmds", "register_write_cmds",
    "register_tools", "register_features",
]


def _record_v1_journal(names) -> None:
    """Record names declared via an API-v1 call, for commit-time ownership
    validation in _atomic.register_plugin_module(). No-op at host level
    (register_all(), where no plugin is currently registering)."""
    from unity_mcp.plugins import _owner
    _owner.record(names)


def register_dsl_tools(*names: str):
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.update(names)
    _record_v1_journal(names)


def _reject_builtin_names(names: tuple, register_msg: str) -> list:
    """Drop names already owned by a builtin/core tool (gating._BUILTIN_NAMES,
    an immutable snapshot taken at import time) — a plugin must never
    reclassify an existing built-in command's read/write direction. Unlike
    gating._ALL_KNOWN, this set does NOT grow as plugins register their own
    tools, so a plugin's own name is never mistaken for a builtin."""
    from unity_mcp.tools import gating
    safe = [n for n in names if n not in gating._BUILTIN_NAMES]
    dropped = set(names) - set(safe)
    if dropped:
        import logging
        logging.getLogger(__name__).warning(
            f"Plugin attempted to reclassify builtin command(s) as {register_msg}: "
            f"{sorted(dropped)} — ignored"
        )
    return safe


def register_read_cmds(*names: str):
    from unity_mcp.middleware import READ_CMDS
    safe = _reject_builtin_names(names, "read")
    READ_CMDS.update(safe)
    # Journal the FULL names tuple (not just `safe`) so a builtin declared in
    # PLUGIN context still lands in the commit-time journal: it can never be
    # in that plugin's _command_owner (guarded.tool() already blocks a plugin
    # from claiming a builtin/host name), so it is "foreign" at commit and
    # rejects the whole plugin — same outcome as register_tools/dsl_tools/
    # features below, which journal their raw input. In HOST context
    # (_owner.current is None) this is a no-op either way, so register_all()
    # keeps today's filter-and-warn-only behavior unchanged.
    _record_v1_journal(names)


def register_write_cmds(*names: str):
    from unity_mcp.middleware import WRITE_CMDS
    safe = _reject_builtin_names(names, "write")
    WRITE_CMDS.update(safe)
    _record_v1_journal(names)  # see register_read_cmds — journal is pre-filter


def register_tools(category: str, tools: set):
    """Register plugin tools into a category. Visibility (TIER1) is platform-controlled —
    plugins cannot promote themselves into the always-on tool budget."""
    from unity_mcp.tools.gating import register_tools as _rt
    _rt(category, tools)
    _record_v1_journal(tools)


def register_features(features: dict):
    from unity_mcp.budget.registry import FEATURES, FeatureMeta
    for name, meta in features.items():
        if isinstance(meta, dict):
            meta = FeatureMeta(**meta)
        FEATURES[name] = meta
    _record_v1_journal(features.keys())
