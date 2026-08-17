"""TDD tests for reflect/coverage.py — coverage() metric."""
import pytest


def test_coverage_minimum():
    """Reflect coverage must be >= 80% of verifiable write commands."""
    from unity_mcp.reflect.coverage import coverage
    c = coverage()
    assert c["pct"] >= 80.0, (
        f"Reflect coverage dropped to {c['pct']}%: missing={c['missing']}"
    )


def test_coverage_returns_required_keys():
    from unity_mcp.reflect.coverage import coverage
    c = coverage()
    assert "pct" in c
    assert "covered" in c
    assert "total" in c
    assert "missing" in c


def test_coverage_types():
    from unity_mcp.reflect.coverage import coverage
    c = coverage()
    assert isinstance(c["pct"], float)
    assert isinstance(c["covered"], int)
    assert isinstance(c["total"], int)
    assert isinstance(c["missing"], list)


def test_coverage_math():
    """covered + missing <= total (may differ if _RULES has non-WRITE_CMDS rules)."""
    from unity_mcp.reflect.coverage import coverage
    c = coverage()
    assert c["covered"] <= c["total"]
    assert c["covered"] >= 0


def test_coverage_missing_not_in_rules():
    """Commands in 'missing' have no registered reflect rule."""
    from unity_mcp.reflect.coverage import coverage
    from unity_mcp.reflect import _RULES
    c = coverage()
    for cmd in c["missing"]:
        assert cmd not in _RULES, f"'{cmd}' in missing but also in _RULES"


def test_coverage_known_rules_counted():
    """Well-known rules from existing modules appear in covered set."""
    from unity_mcp.reflect.coverage import coverage
    from unity_mcp.middleware_types import WRITE_CMDS
    from unity_mcp.reflect import _RULES
    c = coverage()
    # set_property and create_object are always registered WRITE_CMDS
    for cmd in ("set_property", "create_object", "delete_object"):
        if cmd in WRITE_CMDS:
            assert cmd in _RULES
            assert cmd not in c["missing"]
