"""Tests for unity_mcp.constants — shared constants, no circular imports."""
from unity_mcp.constants import DEFAULT_PORT


def test_default_port_value():
    assert DEFAULT_PORT == 9500
