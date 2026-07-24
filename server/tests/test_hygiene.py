"""Test-infrastructure regression guards. Verify autouse fixtures actually fire."""
from pathlib import Path


def test_home_is_isolated():
    """_isolate_home fixture must redirect Path.home() away from real ~/.unity-biome-mcp."""
    home = Path.home()
    assert ".unity-mcp" not in str(home), f"Path.home() leaks to real home: {home}"
    home_str = str(home)
    assert "pytest" in home_str or home_str.startswith(("/private/", "/tmp/")), \
        f"Path.home() should be a pytest tmp dir, got {home}"


def test_metrics_starts_clean():
    """_reset_metrics autouse must reset METRICS counters before each test."""
    from unity_mcp.metrics import METRICS
    snap = METRICS.snapshot()
    assert snap["counters"] == {}, f"METRICS leaked from prior test: {snap['counters']}"


def test_unity_env_defaults_disabled(monkeypatch):
    """_clean_unity_env must default UNITY_MCP_HINTS and UNITY_MCP_VALIDATE to '0'."""
    import os
    assert os.environ.get("UNITY_MCP_HINTS") == "0"
    assert os.environ.get("UNITY_MCP_VALIDATE") == "0"
