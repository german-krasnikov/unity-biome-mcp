"""Tests for resolve_tool_schema — effects and recovery fields (MCP-SCHEMA-031).

Discovery reports surface/mutability but effects/prerequisites are incomplete.
These tests assert that mutation tools document their effects and that
long-running tools document their on_timeout recovery behavior.

No live Unity — tests inspect docstrings and the SchemaRegistry pipeline.
"""
import inspect
from unity_mcp.tools.schema_registry import SchemaRegistry


def test_resolve_tool_schema_returns_effects_field_for_mutation_tools():
    """set_property schema output must contain 'effects:' section.

    Mutation tools must document what side-effects they produce so agents
    can predict impact without executing. This is a regression guard for
    MCP-SCHEMA-031.
    """
    from unity_mcp.tools.objects import set_property
    doc = inspect.getdoc(set_property) or ""
    reg = SchemaRegistry()
    reg.capture("set_property", {}, doc)
    result = reg.format_text(["set_property"])
    assert "effects:" in result.lower(), (
        "set_property schema must contain 'effects:' section. "
        f"Current description: {doc[:200]}"
    )


def test_resolve_tool_schema_returns_recovery_hint_on_failure_tools():
    """run_tests_wait schema output must contain 'on_timeout:' recovery guidance.

    Long-running tools must document what callers receive on timeout so
    agents can recover identities without dispatching a duplicate run.
    This is a regression guard for MCP-SCHEMA-031.
    """
    from unity_mcp.tools.testing import run_tests_wait
    doc = inspect.getdoc(run_tests_wait) or ""
    reg = SchemaRegistry()
    reg.capture("run_tests_wait", {}, doc)
    result = reg.format_text(["run_tests_wait"])
    assert "on_timeout:" in result.lower(), (
        "run_tests_wait schema must contain 'on_timeout:' guidance. "
        f"Current description: {doc[:300]}"
    )
