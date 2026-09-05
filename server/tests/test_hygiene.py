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


def test_reset_gating_fixture_is_autouse(request):
    """test_gating_session_enabled_starts_clean is a tautology once
    _reset_gating_session_enabled exists and always runs first in a worker
    process -- it only catches drift if some OTHER test also leaks. This test
    independently guards the fixture's registration itself.

    request.fixturenames lists every fixture that actually applies to this
    test, including autouse ones nobody explicitly requested. Double-red: red
    if the fixture is deleted (name never appears), red if it loses
    autouse=True (then nothing requests it, so it still never appears)."""
    assert "_reset_gating_session_enabled" in request.fixturenames


def test_gating_session_enabled_starts_clean():
    """_reset_gating_session_enabled autouse must reset gating._session_enabled
    before each test. Without it, a test that calls enable_category(...) without
    a matching reset() (e.g. test_phase1_reorg.py::test_demoted_tools_visible_after_discover,
    which enables the SYSTEM category and never resets) leaks tool visibility
    into unrelated tests under xdist's --dist load ordering -- this is the real
    root cause of the A07a incidental finding (get_enabled_tools appearing in
    test_server_filtering.py::test_filter_tools_fallback_when_bridge_none)."""
    from unity_mcp.tools import gating
    assert gating._session_enabled == set(), (
        f"gating._session_enabled leaked from a prior test: {gating._session_enabled}"
    )
