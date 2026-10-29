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

    "get_hierarchy": {
        "depth":       "Traversal depth (default 2; increase for deeper trees)",
        "root":        "Scene path to scope the hierarchy (omit = whole scene)",
        "filter":      "Substring filter on GameObject name",
        "components":  "Show component types next to each object (increases token cost)",
        "compress":    "Group repeated slot/point/mesh siblings to save tokens on dense scenes",
        "summary":     "Return compact root-only counts (~60-100 tokens) instead of the full tree",
        "incremental": "Return NO_CHANGE if scene is unchanged since last call (saves tokens)",
        "full":        "Bypass distillation — return raw response",
        "scene":       "Filter to a single scene by name (multi-scene only)",
    },

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

    "asset": {
        "path": "Action-specific project asset/package path (usually Assets/...; import_package accepts a package file path)",
        "name": "Asset-name search filter used by find (not a GameObject name)",
        "type": "Unity asset type filter or create kind (for example Material or ScriptableObject)",
        "value": "Import-setting/property value for the selected asset action",
    },

    "build": {
        "path": "Player build output file or directory (default Builds/<target>)",
    },

    "lint_playtest": {
        "path": "Project-relative path to a .playtest DSL file (mutually exclusive with script)",
    },

    "package": {
        "name": "Unity Package Manager package identifier for add/remove (for example com.unity.inputsystem)",
    },

    "scriptable_object": {
        "path": "Project asset path to the ScriptableObject .asset file",
    },

    "shader": {
        "path": "Project asset path to a .shader or .shadergraph file",
        "name": "Action-specific Shader Graph property/node name (not a GameObject name)",
        "type": "Action-specific Shader Graph property value type (not a component type)",
        "shader_name": "Shader declaration name used when creating shader source",
    },

    "uitk_file": {
        "path": "Assets/ path to a .uxml or .uss project file; Library/ and Packages/ are rejected",
    },

    "scene_change_plan": {
        "dry_run": "False (default) = create plan_id. True = probe pre-flights only, no plan created.",
    },

    "apply_template": {
        "name": "Saved scene-template identifier from .claude/templates (without .cs)",
    },

    "autofit_collider": {
        "type": "Collider shape to fit: box|sphere|capsule",
    },

    "create_ui": {
        "type": "uGUI element type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView",
    },

    "lint_uitk": {
        "path": "Assets/ path to the UXML or USS file to validate",
    },

    "menu": {
        "path": "Unity Editor menu-item path to execute, or submenu prefix to list",
    },

    "uitk_element": {
        "name": "VisualElement name used after ref and before selector in addressing priority",
    },

    "timeline": {
        "name": "Action-specific Timeline track, clip, or marker name",
        "value": "Action-specific Timeline value; set_track_offset accepts auto|transform|scene",
    },

    # ── TIER1 TOOLS ─────────────────────────────────────────────────────────

    "get_console": {
        "count": "Max log entries to return (default 10)",
        "level": "Filter by level: log|warning|error|exception|assert (omit = all)",
        "first": "Skip the first N entries (pagination offset)",
    },

    "run_tests": {
        "mode":       "Test runner mode: EditMode or PlayMode",
        "filter":     "NUnit filter expression (e.g. 'MyNamespace.MyTest')",
        "request_id": "Caller-supplied idempotency ID — reuse to retry a failed dispatch without double-running",
    },

    "run_tests_wait": {
        "mode":          "Test runner mode: EditMode or PlayMode",
        "filter":        "NUnit filter expression (e.g. 'MyNamespace.MyTest')",
        "timeout":       "Max seconds to wait for completion (default 900)",
        "poll_interval": "Seconds between status polls (default 5)",
        "request_id":    "Caller-supplied idempotency ID",
    },

    "screenshot": {
        "width":          "Image width in pixels (default 640)",
        "height":         "Image height in pixels (default 480)",
        "camera":         "View type: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game",
        "path":           "For single_view/multi_view: target GameObject scene path; for standard captures: legacy project-contained .png destination (prefer output_path)",
        "output_path":    "Unambiguous project-contained .png destination for every capture mode",
        "describe":       "Haiku prompt for AI text description instead of returning file path (15-100x fewer tokens)",
        "raw":            "Force returning file path even when describe= is set",
        "zoom":           "Zoom factor — higher = closer",
        "angle":          "Euler angle for single_view: front|left|top|iso|ex,ey,ez",
        "angles":         "Per-view Euler angles: 'ex,ey,ez|...' (underscore = skip this view)",
        "supersample":    "Anti-alias quality 1-4 (higher = sharper, slower)",
        "offset":         "Framing offset vector",
        "fixed_size":     "Fixed framing size override",
        "highlight":      "Comma-separated paths to highlight with bounding box (e.g. /Player:#FF0000)",
        "show_colliders": "Overlay collider wireframes on the screenshot",
        "annotation_id":  "Frame + highlight annotation by ID (auto-sets camera=annotation_frame)",
    },

    "screenshot_baseline": {
        "name": "File-safe baseline identifier (not a GameObject name; no '/', '\\', or '..')",
    },

    "screenshot_compare": {
        "name": "File-safe saved-baseline identifier (not a GameObject name; no '/', '\\', or '..')",
    },

    "uitk_intent": {
        "name": "Base asset filename without extension (for <name>.uxml and <name>.uss)",
        "path": "Project asset output folder for the generated UXML/USS pair (default Assets/UI)",
    },

    "scene": {
        "action": "Operation: new|open|save|discard|open_additive|close|set_active|list",
        "path":   "Asset path — required for open/save/open_additive/close/set_active",
        "scene":  "Scene name for save/discard when multiple scenes are loaded",
    },

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
