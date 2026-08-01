"""
API Design Standards enforcement — auto-checks for Pattern A booleans,
duplicate docstrings, ToolSpec coverage, and arg-name → TCP-key alignment.
Runs in the standard 'not live' pytest suite (no Unity required).

Complements test_toolspec_v2_parity.py (which checks _SPECS completeness).
Covers all tool modules that expose public async functions registered via mcp.tool().
Helper modules (intent_common, reload_ladder, schema_registry) are excluded
because their public async functions are internal utilities, not MCP tools.
"""
import ast
import inspect
import importlib
import textwrap

import pytest

# --- Whitelists ---

# bool = True params that are semantically correct (not Pattern A violations).
# Tuple: (module_short_name, function_name, param_name)
BOOL_TRUE_EXCEPTIONS = {
    ("objects", "transfer_object", "world_position_stays"),   # semantic: False=snap
    ("objects", "set_parent", "world_position_stays"),        # semantic: False=snap
    ("diagnose", "diagnose", "expected_compile"),             # semantic: True=normal state
    ("gating", "discover_tools", "enable"),                   # semantic: True=enable on discover
    ("meta", "discover_tools", "enable"),                     # semantic: True=enable on discover
    ("scene", "get_changes", "clear"),                        # semantic: True=consume-on-read
    ("spatial", "region_clear", "dry_run"),                   # safety guard: preview before delete
    ("asset", "asset", "include_deps"),                       # semantic: True=include deps by default
    ("runtime", "run_playtest_suite", "stop_after"),          # semantic: True=stop suite on failure
    ("transaction", "scene_change_plan", "dry_run"),          # semantic: True=safe preview by default
    ("transaction", "apply_scene_change", "verify"),          # semantic: True=verify after apply
    ("transaction", "apply_scene_change", "save"),            # semantic: True=save after apply
}

# bool params with no default — allowed ONLY when the bool IS the payload.
BOOL_NO_DEFAULT_ALLOWED = {
    ("objects", "set_active", "active"),  # sole purpose: set active state
}

# Modules to enforce (short names match module_path.split(".")[-1]).
# Excludes helper/internal modules: intent_common, reload_ladder, schema_registry.
TOOL_MODULES = [
    "unity_mcp.tools.animation",
    "unity_mcp.tools.animator_intent_tool",
    "unity_mcp.tools.ask_tool",
    "unity_mcp.tools.ask_user_tool",
    "unity_mcp.tools.asset",
    "unity_mcp.tools.auto_wire",
    "unity_mcp.tools.autobatch",
    "unity_mcp.tools.batch",
    "unity_mcp.tools.budget_tool",
    "unity_mcp.tools.code_intel",
    "unity_mcp.tools.codegen",
    "unity_mcp.tools.connection",
    "unity_mcp.tools.console",
    "unity_mcp.tools.debug_tool",
    "unity_mcp.tools.diagnose",
    "unity_mcp.tools.diagnostics",
    "unity_mcp.tools.do_tool",
    "unity_mcp.tools.editor_control",
    "unity_mcp.tools.gating",
    "unity_mcp.tools.meta",
    "unity_mcp.tools.metrics_tool",
    "unity_mcp.tools.objects",
    "unity_mcp.tools.permission_prompt_tool",
    "unity_mcp.tools.profiling",
    "unity_mcp.tools.rendering",
    "unity_mcp.tools.runtime",
    "unity_mcp.tools.scene",
    "unity_mcp.tools.scene_health",
    "unity_mcp.tools.screenshot",
    "unity_mcp.tools.skills",
    "unity_mcp.tools.spatial",
    "unity_mcp.tools.sync",
    "unity_mcp.tools.testing",
    "unity_mcp.tools.transaction",
    "unity_mcp.tools.ui",
    "unity_mcp.tools.ui_intent_tool",
    "unity_mcp.tools.verify",
    "unity_mcp.tools.vfx_intent_tool",
    "unity_mcp.tools.watch",
]


# Known _args(tcp_key=local_var) mismatches that exist in current code.
# Format: (module_short_name, fn_name, tcp_key, local_var_name)
ARGS_KEY_EXCEPTIONS = {
    # Pattern A′ encoded into local `wps` before passing
    ("objects", "transfer_object", "world_position_stays", "wps"),
    # run_playtest_suite / lint_playtest_suite use local var filepath for the for-loop path
    ("runtime", "run_playtest_suite", "path", "filepath"),
    ("runtime", "lint_playtest_suite", "path", "filepath"),
    # run_playtest uses computed _fresh local var
    ("runtime", "run_playtest", "fresh", "_fresh"),
    # watch uses watch_id locally but TCP key is id
    ("watch", "watch", "id", "watch_id"),
}


def _iter_tool_fns(modules):
    """Yield (module_short_name, function_name, function) for public async fns defined in the module."""
    for mod_path in modules:
        mod = importlib.import_module(mod_path)
        mod_name = mod_path.split(".")[-1]
        for fn_name, fn in inspect.getmembers(mod, inspect.isfunction):
            if fn_name.startswith("_") or not inspect.iscoroutinefunction(fn):
                continue
            # Skip functions imported from other modules
            if fn.__module__ != mod.__name__:
                continue
            yield mod_name, fn_name, fn


def test_boolean_params_default_false():
    """All bool params must default to False (Pattern A), with explicit exceptions."""
    violations = []
    for mod_name, fn_name, fn in _iter_tool_fns(TOOL_MODULES):
        sig = inspect.signature(fn)
        for param_name, param in sig.parameters.items():
            if param.annotation is not bool:
                continue
            if param.default is inspect.Parameter.empty:
                # Required bool — allowed only in whitelist
                if (mod_name, fn_name, param_name) not in BOOL_NO_DEFAULT_ALLOWED:
                    violations.append(
                        f"{mod_name}.{fn_name}({param_name}: bool) — required bool "
                        "not in BOOL_NO_DEFAULT_ALLOWED whitelist"
                    )
            elif param.default is not False:
                if (mod_name, fn_name, param_name) not in BOOL_TRUE_EXCEPTIONS:
                    violations.append(
                        f"{mod_name}.{fn_name}({param_name}: bool = {param.default!r}) "
                        "— not Pattern A and not in BOOL_TRUE_EXCEPTIONS"
                    )
    assert not violations, "Boolean Pattern A violations:\n" + "\n".join(violations)


def test_no_duplicate_docstrings():
    """No two tool functions may share identical docstrings (catches copy-paste additions)."""
    seen: dict[str, str] = {}
    dupes = []
    for mod_name, fn_name, fn in _iter_tool_fns(TOOL_MODULES):
        doc = " ".join((fn.__doc__ or "").strip().lower().split())
        if not doc:
            continue
        key = f"{mod_name}.{fn_name}"
        if doc in seen:
            dupes.append(f"{key} duplicates {seen[doc]}")
        else:
            seen[doc] = key
    assert not dupes, "Duplicate docstrings:\n" + "\n".join(dupes)


def test_all_tool_fns_have_toolspec():
    """Every public async function in TOOL_MODULES must have a ToolSpec entry."""
    from unity_mcp.tools.tool_specs import _SPECS  # noqa: PLC0415

    missing = []
    for mod_name, fn_name, fn in _iter_tool_fns(TOOL_MODULES):
        if fn_name not in _SPECS:
            missing.append(f"{mod_name}.{fn_name} — no ToolSpec entry in tool_specs.py")
    assert not missing, "Missing ToolSpec entries:\n" + "\n".join(missing)


def test_args_factory_keys_match_param_names():
    """In _args(key=value) calls, TCP key must equal the Python param name."""
    violations = []
    for mod_name, fn_name, fn in _iter_tool_fns(TOOL_MODULES):
        try:
            src = inspect.getsource(fn)
            tree = ast.parse(textwrap.dedent(src))
        except (OSError, IndentationError, SyntaxError):
            continue
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            if not (isinstance(node.func, ast.Name) and node.func.id == "_args"):
                continue
            for kw in node.keywords:
                if kw.arg is None:  # **kwargs splat — skip
                    continue
                if not isinstance(kw.value, ast.Name):
                    continue  # computed value — can't check statically
                if kw.arg != kw.value.id:
                    if (mod_name, fn_name, kw.arg, kw.value.id) in ARGS_KEY_EXCEPTIONS:
                        continue
                    violations.append(
                        f"{mod_name}.{fn_name}: _args({kw.arg}={kw.value.id}) "
                        f"— TCP key '{kw.arg}' != Python param '{kw.value.id}'"
                    )
    assert not violations, "Arg-name / TCP-key mismatches:\n" + "\n".join(violations)
