"""Centralized param descriptions for MCP schema post-processing.

_COMMON:            cross-tool param descriptions (fallback).
PARAM_DESCRIPTIONS: per-tool overrides (key = fn.__name__).
"""
from __future__ import annotations

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

    "get_component": {
        "path":     "Scene path to the GameObject (e.g. /Player or /World/Enemy)",
        "type":     "Component type name (e.g. 'Transform', 'Rigidbody', 'MeshRenderer')",
        "fields":   "Comma-separated fields to return (e.g. 'mass,localPosition') — skips all others, saves tokens",
        "full":     "Return raw uncompressed response (bypass distillation)",
        "compress": "Strip fields at their default value before TCP transfer — reduces response size",
    },

    "set_property": {
        "path":      "Scene path to the GameObject (e.g. /Player/Body)",
        "component": "Component type (empty string = Transform)",
        "prop":      "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')",
        "value":     "New value. ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), #instanceID, or 'null'",
        "dry_run":   "Show what would change without applying (safe preview)",
        "find_type": "Component type — bulk-sets prop on ALL scene objects with this component (no path needed)",
    },

    "create_object": {
        "name":        "Name for the new GameObject",
        "parent":      "Scene path of the parent (omit = scene root)",
        "components":  "Comma-separated component types to add on creation (e.g. 'Rigidbody,BoxCollider')",
        "primitive":   "Primitive mesh type: Cube|Sphere|Cylinder|Capsule|Plane|Quad",
        "prefab_path": "Asset path to instantiate from prefab (e.g. Assets/Prefabs/Enemy.prefab)",
        "scene":       "Target scene name when multiple scenes are loaded (omit = active scene)",
    },

    "manage_component": {
        "path":   "Scene path to the GameObject",
        "type":   "Component type (short: 'Button' or full namespace: 'UnityEngine.UI.Button')",
        "action": "'add' or 'remove' — not 'enable'/'disable' (use set_property m_Enabled for that)",
    },

    "inspect": {
        "paths":      "Comma-separated scene paths (e.g. '/Player,/Enemy,/Camera')",
        "components": "Comma-separated component types to read (omit = all)",
        "fields":     "Comma-separated field names to project across all objects — reduces tokens",
        "full":       "Return raw uncompressed response (bypass distillation)",
        "compress":   "Strip fields at their default value before TCP transfer — reduces response size",
        "find_type":  "Component type — auto-populates paths from all scene objects with this component",
    },

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
        "atomic":           "True = revert ALL prior ops on first failure via Unity Undo (fs side-effects not reverted)",
        "validate_aliases": "Dry-run alias validation before executing any mutations",
    },

    "execute_code": {
        "code":       "C# code to execute in the Unity Editor context",
        "undo_label": "Label for the Undo group entry (default 'execute_code')",
    },

    # ── TIER1 TOOLS ─────────────────────────────────────────────────────────

    "get_console": {
        "count": "Max log entries to return (default 10)",
        "level": "Filter by level: log|warning|error|exception (omit = all)",
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
        "timeout":       "Max seconds to wait for completion (default 120)",
        "poll_interval": "Seconds between status polls (default 5)",
        "request_id":    "Caller-supplied idempotency ID",
    },

    "screenshot": {
        "width":          "Image width in pixels (default 640)",
        "height":         "Image height in pixels (default 480)",
        "camera":         "View type: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game",
        "path":           "Save to this file path (omit = auto-generate in ScreenShots/)",
        "output_path":    "Alias for path — save to this file path",
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

    "run_playtest": {
        "script":              "Inline DSL script (mutually exclusive with path)",
        "timeout":             "Max seconds to wait for the playtest to finish (default 120)",
        "abort_on_fail":       "Stop script execution on first ASSERT failure",
        "defs":                "Inline VAL definitions prepended to script (alias block)",
        "path":                "Path to .playtest DSL file on disk (mutually exclusive with script)",
        "snapshot_on_failure": "Auto-capture screenshot on first ASSERT failure",
        "fresh":               "Stop and restart Play Mode before running the script",
        "before_hook":         "DSL commands to run before entering Play Mode",
        "after_hook":          "DSL commands to run after Play Mode exits",
    },

    "scene": {
        "action": "Operation: new|open|save|discard|open_additive|close|set_active|list",
        "path":   "Asset path — required for open/save/open_additive/close/set_active",
        "scene":  "Scene name for save/discard when multiple scenes are loaded",
    },

    "search_scene": {
        "query": "Syntax: 'name text', 't:Component', 'tag=Tag', 'layer=N', 'active=bool' — combine with spaces",
        "root":  "Scene path to scope the search (omit = whole scene)",
        "limit": "Max results (default 50; 0 = unlimited)",
        "scene": "Filter to a single scene by name (multi-scene only)",
    },

    "set_active": {
        "path":   "Scene path to the GameObject",
        "active": "True to activate, False to deactivate",
    },

    "set_parent": {
        "path":                 "Scene path of the GameObject to reparent",
        "parent":               "New parent scene path (omit or null = move to scene root)",
        "world_position_stays": "True (default) = preserve world transform; False = keep local transform relative to new parent",
    },

    "save_skill": {
        "code": "C# code or batch commands to save as a reusable skill",
    },

    "save_template": {
        "code": "C# code or batch commands to save as a scene template",
    },

    "delete_object": {
        "id":    "Instance ID of the GameObject to delete (from get_hierarchy)",
        "path":  "Scene path of the GameObject to delete",
        "force": "True to delete non-empty container objects without error",
    },
}
