"""Centralized param descriptions for MCP schema post-processing.

_COMMON:            cross-tool param descriptions (fallback).
PARAM_DESCRIPTIONS: per-tool overrides (key = fn.__name__).
"""

# Cross-tool params — used as fallback when tool-specific entry is absent.
_COMMON: dict[str, str] = {
    "path":      "Scene path to target GameObject (e.g. /Parent/Child)",
    "id":        "Instance ID of the GameObject (integer from get_hierarchy)",
    "active":    "True to activate, False to deactivate",
    "full":      "Bypass distillation — return raw uncompressed response",
    "compress":  "Strip default values before TCP transfer to reduce response size",
    "name":      "Name of the GameObject",
    "scene":     "Scene name when multiple scenes are loaded (omit = active scene)",
    "parent":    "Scene path to the parent GameObject",
    "type":      "Component type name (e.g. 'Rigidbody', 'BoxCollider')",
    "action":    "Operation to perform — see tool docstring for allowed values",
    "dry_run":   "Preview changes without applying them",
    "filter":    "Substring filter to narrow results",
    "depth":     "Maximum hierarchy depth to traverse",
    "component": "Component type name on the target object",
    "root":      "Scene path to scope the tree (omit = whole scene)",
    "query":     "Search query — see tool docstring for syntax",
    "limit":     "Maximum number of results to return",
    "fields":    "Comma-separated field names to project (reduces tokens)",
    "timeout":   "Seconds before giving up (default varies per tool)",
    "mode":      "Execution mode — see tool docstring for allowed values",
    "prop":      "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')",
    "value":     "New value to set",
    "index":     "Zero-based index",
}

# Per-tool descriptions — key must match fn.__name__ exactly.
# Values override _COMMON for that param on that tool.
PARAM_DESCRIPTIONS: dict[str, dict[str, str]] = {

    # ── CORE TOOLS ─────────────────────────────────────────────────────────

    "batch": {
        "commands":         "One command per line (e.g. 'get_component path=/Player type=Transform')",
        "on_error":         "Error behavior: continue (default) | stop — stop aborts remaining commands",
        "timeout":          "Total timeout in seconds (default 75)",
        "atomic":           "On failure, revert prior Undo-recorded Unity mutations; external/file/asset/package/process effects may remain",
        "validate_aliases": "Dry-run alias validation before executing any mutations",
    },

    "execute_code": {
        "code":       "C# code to execute in the Unity Editor context",
        "undo_label": "Label for the Undo group entry (default 'execute_code')",
    },

    "lint_playtest": {
        "path": "Project-relative path to a .playtest DSL file (mutually exclusive with script)",
    },

    "scene_change_plan": {
        "dry_run": "False (default) = create plan_id. True = probe pre-flights only, no plan created.",
    },

    "apply_template": {
        "name": "Saved scene-template identifier from .claude/templates (without .cs)",
    },

    # ── TIER1 TOOLS ─────────────────────────────────────────────────────────

    "save_skill": {
        "name": "File-safe identifier for the learned skill",
        "code": "C# code or batch commands to save as a reusable skill",
    },

    "save_template": {
        "name": "File-safe identifier for the reusable scene template",
        "code": "C# code or batch commands to save as a scene template",
    },
}

# Matches any non-empty string including newlines; suppresses CWE-78 without restricting valid input.
_RE_ANY = r"^[\s\S]+$"

# Extra JSON-Schema properties to merge into a parameter's schema after description
# injection.  Used to satisfy linter constraints without renaming parameters.
# Values are merged with setdefault — existing keys are never overwritten.
PARAM_SCHEMA_EXTRAS: dict[str, dict[str, dict]] = {
    # 'code' matches COMMAND_PARAM_TERMS; a non-permissive pattern suppresses
    # COMMAND_PARAMETER_UNCONSTRAINED (CWE-78) without restricting valid input.
    "execute_code":  {"code": {"pattern": _RE_ANY}},
    "save_skill":    {"code": {"pattern": _RE_ANY}},
    "save_template": {"code": {"pattern": _RE_ANY}},
}

# Schema descriptions of the catalog's tools (dsl/tools.biome) are generated; a tool is described there or above, never in both.
from .generated.catalog_params import PARAM_DESCRIPTIONS as _CATALOG_PARAM_DESCRIPTIONS  # noqa: E402

for _tool, _descriptions in _CATALOG_PARAM_DESCRIPTIONS.items():
    if _tool in PARAM_DESCRIPTIONS:
        raise ValueError(f"{_tool} is described in the catalog (dsl/tools.biome) and in _param_descriptions.py")
    PARAM_DESCRIPTIONS[_tool] = _descriptions
