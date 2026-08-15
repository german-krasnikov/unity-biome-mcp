"""P1.8 Schema/Catalog Parity Gate.

Verifies that every MCP tool and every C# TCP command is consistently
registered across tool_specs._SPECS and CommandRouter.Registration.cs.
Pure static analysis — no Unity required.
"""
import re
from pathlib import Path

_REPO = Path(__file__).resolve().parents[2]
_EDITOR = _REPO / "unity-plugin" / "Editor"

# C# commands that are protocol/internal — no ToolSpec needed.
_CS_PROTOCOL_ONLY = {
    "get_disabled_tools",  # internal tool-gating state
    "set_tool_catalog",    # catalog push from Python on connect
    "force_play_stop",     # T5 recovery — not an MCP tool
    "set_client_label",    # session metadata — not an MCP tool
    "get_aliases",         # alias cache seeding by Python middleware
    "clear_console",       # not exposed as a named MCP tool
    "console_clear_buffer",  # internal (resets dropped-problem count without clearing ring buffer)
    "compile_status",      # internal (await_compile polls this)
    "sync",                # internal (Python sync_unity → this)
    "sync_status",         # internal polling
    "force_refresh",       # T5 recovery — not an MCP tool
    "get_capabilities",    # internal capability query
    "get_status",          # C# backing for mcp_status Python tool (name mismatch)
    "navmesh",             # conditional-compile alias for navmesh_query
    "watch_add",           # sub-action of "watch" MCP tool
    "watch_remove",        # sub-action of "watch" MCP tool
    "watch_clear",         # sub-action of "watch" MCP tool
    "watch_reset",         # sub-action of "watch" MCP tool
    "search_context",      # internal (resources refresh_dynamic polls this)
    "set_runtime_property",  # C# handler stays: middleware reroutes set_property here in Play Mode
    "warm_type_cache",     # internal (sync_unity warms TypeCache after domain reload)
    "checkpoint_undo_restore",  # T19: internal C# command called by checkpoint_restore tool
    "get_profile_context",     # T22: TCP command for profiler data, not an MCP tool
}

# _SPECS entries with no dedicated C# command (Python-only MCP tools).
_PYTHON_ONLY = {
    # LLM / sampling
    "ask", "do", "doctor", "discover_tools", "resolve_tool_schema", "set_llm_config",
    # TCP connection management
    "list_connections", "reconnect_unity",
    # Local state / meta
    "budget_status", "permission_prompt",
    # Console helpers (Python-side)
    "console_mark", "get_console_since",
    # Compile / sync orchestration (poll C# commands internally)
    "await_compile", "sync_unity", "run_tests_wait",
    # Playtest orchestration (reads files, delegates to run_playtest C#)
    "run_playtest_suite", "lint_playtest_suite",
    # Screenshot comparison (uses screenshot C# internally, adds Python logic)
    "screenshot_baseline", "screenshot_compare",
    # Metrics / debug / snapshot (Python-side aggregators)
    "get_metrics", "debug", "snapshot",
    # Session storage (Python files)
    "save_session", "load_session",
    # Skills / templates (Python file ops)
    "save_skill", "use_skill", "list_skills",
    "apply_template", "save_template", "list_templates",
    # Autobatch orchestrators (pure Python)
    "setup_objects", "set_properties", "configure_objects",
    # Intent tools (LLM orchestration over existing C# commands)
    "animator_intent", "vfx_intent", "ui_intent",
    # Code helpers (delegate to execute_code / get_console C# internally)
    "smart_build", "auto_fix",
    # Watch: Python orchestrator dispatches to watch_add/watch_remove/etc. sub-commands
    "watch",
    # Name mismatch: C# uses "navmesh", spec uses "navmesh_query"
    "navmesh_query",
    # Name mismatch: C# uses "get_status", Python MCP tool is "mcp_status"
    "mcp_status",
    # Verification orchestrator — calls Python tool functions internally
    "verify_after_change",
    # Transaction orchestrators — pure Python, delegate to existing C# commands
    "scene_change_plan", "apply_scene_change",
    # Release orchestrator — composite of C# reads, no C# registration
    "release_smoke",
    # T16: ChangeSet summary — pure Python coordinator query, no C# command
    "get_changeset",
    # T19: checkpoint orchestrators — call C# internally (checkpoint, diagnose, checkpoint_undo_restore)
    "checkpoint_create", "checkpoint_restore",
    # T20: plan orchestrators — pure Python, no C# handler
    "plan_create", "plan_approve", "plan_reject", "plan_edit", "plan_status",
    # T21: project brief — pure Python context aggregator, no C# handler
    "brief_build",
    # UITOOLKIT tools — C# handlers added in Session 4
    "inspect_uitk", "lint_uitk",
}


def _extract_cs_commands() -> set[str]:
    """Regex-scan non-Test C# files for CommandRegistry.Register* calls."""
    pattern = re.compile(r'CommandRegistry\.Register(?:Async|Action)?\(\s*"([^"]+)"')
    result = set()
    for cs in _EDITOR.rglob("*.cs"):
        if "Tests" in cs.parts:
            continue
        result |= set(pattern.findall(cs.read_text(encoding="utf-8")))
    return result


def _specs() -> dict:
    from unity_mcp.tools.tool_specs import _SPECS
    return _SPECS


# ── tests ──────────────────────────────────────────────────────────────────────

def test_cs_commands_parseable():
    cmds = _extract_cs_commands()
    assert len(cmds) > 50, f"Expected >50 C# commands, got {len(cmds)}: {sorted(cmds)}"


def test_specs_nonempty():
    assert len(_specs()) > 50


def test_all_cs_commands_have_spec_or_are_protocol_only():
    """Every C# command is in _SPECS or whitelisted as protocol-internal."""
    cs = _extract_cs_commands()
    specs = set(_specs().keys())
    unknown = cs - specs - _CS_PROTOCOL_ONLY
    assert not unknown, (
        f"C# commands missing from _SPECS and not in _CS_PROTOCOL_ONLY: {sorted(unknown)}\n"
        "→ Add to _SPECS in tool_specs.py, or to _CS_PROTOCOL_ONLY in this test."
    )


def test_all_specs_have_cs_command_or_are_python_only():
    """Every non-_INTERNAL ToolSpec has a C# command or is known Python-only."""
    specs = _specs()
    cs = _extract_cs_commands()
    non_internal = {k for k, v in specs.items() if v.category not in ("_INTERNAL", "DEPRECATED")}
    missing = non_internal - _PYTHON_ONLY - cs
    assert not missing, (
        f"_SPECS tools with no C# command and not in _PYTHON_ONLY: {sorted(missing)}\n"
        "→ Register in C#, or add to _PYTHON_ONLY in this test."
    )


def test_tier1_tools_are_accounted_for():
    """Every tier1 ToolSpec has a C# command or is in _PYTHON_ONLY."""
    specs = _specs()
    cs = _extract_cs_commands()
    # _INTERNAL tier1 entries would be a data error, but guard anyway
    tier1 = {k for k, v in specs.items() if v.tier1 and v.category != "_INTERNAL"}
    missing = tier1 - cs - _PYTHON_ONLY
    assert not missing, (
        f"tier1 tools with no C# command and not Python-only: {sorted(missing)}\n"
        "→ Register in C#, or add to _PYTHON_ONLY in this test."
    )


def test_python_only_entries_have_no_csharp_command():
    """Nothing in _PYTHON_ONLY should actually have a C# command registration.

    A phantom entry in _PYTHON_ONLY incorrectly documents a tool as Python-side
    when C# already handles it, hiding schema drift.
    """
    cs = _extract_cs_commands()
    phantoms = _PYTHON_ONLY & cs
    assert not phantoms, (
        f"_PYTHON_ONLY entries that ARE registered in C#: {sorted(phantoms)}\n"
        "→ Remove from _PYTHON_ONLY — they have real C# handlers."
    )


def test_direct_only_tools_covered_by_csharp_guard():
    """Every direct_only tool must be in CommandRouter._PythonOnlyTools (C#).

    The C# HashSet acts as a guard — direct TCP callers get an actionable
    error instead of InvalidOperationException. If a new tool gets
    direct_only=True in Python, it must be added to the C# guard too.
    """
    direct_only = {name for name, spec in _specs().items() if spec.direct_only}
    guard_path = _EDITOR / "CommandRouter.cs"
    text = guard_path.read_text(encoding="utf-8")
    for tool in sorted(direct_only):
        assert f'"{tool}"' in text, (
            f"direct_only tool '{tool}' missing from CommandRouter._PythonOnlyTools — "
            "add it so direct TCP callers get a clear error."
        )


def test_discover_tools_categories_match_specs():
    """Every static (non-plugin) tool in CATEGORIES has a ToolSpec."""
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    specs = set(_specs().keys())
    all_category_tools = set()
    for tools in _THEMED_CATEGORIES.values():
        all_category_tools |= set(tools)
    ghost = all_category_tools - specs
    assert not ghost, (
        f"Tools in _THEMED_CATEGORIES but missing from _SPECS: {sorted(ghost)}\n"
        "→ Add to _SPECS in tool_specs.py, or remove from category."
    )


# ── P-419: _tool_surface_line must include mutability ────────────────────────

def test_tool_surface_line_includes_mutability_read():
    """P-419: _tool_surface_line must include mutability=read for read tools."""
    from unity_mcp.tools.gating import _tool_surface_line
    line = _tool_surface_line("get_hierarchy")
    assert "mutability=read" in line


def test_tool_surface_line_includes_mutability_write():
    """P-419: _tool_surface_line must include mutability=write for write tools."""
    from unity_mcp.tools.gating import _tool_surface_line
    line = _tool_surface_line("set_property")
    assert "mutability=write" in line
