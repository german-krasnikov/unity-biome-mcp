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


def register_dsl_tools(*names: str):
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.update(names)


def register_read_cmds(*names: str):
    from unity_mcp.middleware import READ_CMDS
    READ_CMDS.update(names)


def register_write_cmds(*names: str):
    from unity_mcp.middleware import WRITE_CMDS
    WRITE_CMDS.update(names)


def register_tools(category: str, tools: set):
    """Register plugin tools into a category. Visibility (TIER1) is platform-controlled —
    plugins cannot promote themselves into the always-on tool budget."""
    from unity_mcp.tools.gating import register_tools as _rt
    _rt(category, tools)


def register_features(features: dict):
    from unity_mcp.budget.registry import FEATURES, FeatureMeta
    for name, meta in features.items():
        if isinstance(meta, dict):
            meta = FeatureMeta(**meta)
        FEATURES[name] = meta
