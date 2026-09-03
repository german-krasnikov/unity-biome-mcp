"""A03: autouse fixture that zeroes bridge/heartbeat/retry backoff constants
before any bridge is constructed, so tests don't pay real wall-clock backoff.

Patches BOTH the source module (unity_mcp.bridge_heartbeat) and bridge.py's
own re-exported copies (`from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S,
RELOAD_BACKOFF_S`) -- these are independent name bindings; UnityBridge.__init__
and should_retry() read bridge.py's copy, while HeartbeatMixin's methods (which
live in bridge_heartbeat.py) read the original. Also patches bridge_retry.py's
named backoff constants (_capped_backoff's inputs) -- the actual source of the
14-15s real-wall-clock retry sequences measured in the A02 baseline.

Opt out with @pytest.mark.real_clock for a test that legitimately asserts a
real backoff/timer value (see server/pyproject.toml for the marker doc).
"""
import importlib

import pytest

_FAST_S: float = 0.0

_PATCH_TARGETS: tuple[tuple[str, str], ...] = (
    ("unity_mcp.bridge_heartbeat", "BACKOFF_MIN_S"),
    ("unity_mcp.bridge_heartbeat", "RELOAD_BACKOFF_S"),
    ("unity_mcp.bridge_heartbeat", "BUSY_TICK_S"),
    ("unity_mcp.bridge_heartbeat", "IDLE_TICK_S"),
    ("unity_mcp.bridge", "BACKOFF_MIN_S"),
    ("unity_mcp.bridge", "RELOAD_BACKOFF_S"),
    ("unity_mcp.bridge", "_CAPACITY_RETRY_AFTER_DEFAULT_S"),
    ("unity_mcp.bridge_retry", "_RETRY_BACKOFF_BASE_S"),
    ("unity_mcp.bridge_retry", "_RETRY_BACKOFF_CAP_S"),
    ("unity_mcp.bridge_retry", "_TRANSIENT_RETRY_DELAY_S"),
)


@pytest.fixture(autouse=True)
def _fast_clock(monkeypatch, request):
    if request.node.get_closest_marker("real_clock"):
        yield
        return
    for mod_name, attr in _PATCH_TARGETS:
        mod = importlib.import_module(mod_name)
        monkeypatch.setattr(mod, attr, _FAST_S)
    yield
