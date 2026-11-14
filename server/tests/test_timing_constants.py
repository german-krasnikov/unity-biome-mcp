"""Parity tests for the Timing DSL data-contract (Task 1.4).

dsl/data/timing.data-contract / timing.core-data generate a single Timing
row (server/src/unity_mcp/generated/timing_constants.py) that replaces 9
bare-float constants previously hand-duplicated across bridge.py,
bridge_heartbeat.py, bridge_reload_state.py and phantom_cleaner.py.

Two kinds of check:
  - the generated row itself matches the pinned literals (values never change)
  - each consumer module is actually SOURCED from the generated singleton,
    not a coincidentally-equal bare literal (inspect.getsource scan) — this
    is what gives genuine red/green across the migration, since the values
    are identical before and after.
"""
import inspect

import unity_mcp.bridge as _bridge
import unity_mcp.bridge_heartbeat as _bridge_heartbeat
import unity_mcp.bridge_reload_state as _bridge_reload_state
import unity_mcp.phantom_cleaner as _phantom_cleaner
from unity_mcp.generated.timing_constants import Timing, TIMING_ROWS

# Captured at import time (module-level `from ... import`, not a live module
# attribute read): the autouse _fast_clock fixture (see tests/_fast_clock.py)
# monkeypatches unity_mcp.bridge_heartbeat's BACKOFF_MIN_S/RELOAD_BACKOFF_S/
# BUSY_TICK_S/IDLE_TICK_S attributes to 0.0 for every test unless marked
# @pytest.mark.real_clock. A live `_bridge_heartbeat.BACKOFF_MIN_S` read
# inside a test body would see the patched value, not the real default.
from unity_mcp.bridge_heartbeat import (  # noqa: E402
    BACKOFF_MAX_S as _REAL_BACKOFF_MAX_S,
    BACKOFF_MIN_S as _REAL_BACKOFF_MIN_S,
    BUSY_TICK_S as _REAL_BUSY_TICK_S,
    IDLE_TICK_S as _REAL_IDLE_TICK_S,
    ORPHAN_CHECK_INTERVAL_S as _REAL_ORPHAN_CHECK_INTERVAL_S,
    RELOAD_BACKOFF_S as _REAL_RELOAD_BACKOFF_S,
)

_EXPECTED = Timing(
    startupGraceS=90.0,
    domainReloadExpiryS=90.0,
    backoffMinS=5.0,
    backoffMaxS=60.0,
    reloadBackoffS=1.0,
    orphanCheckIntervalS=1.0,
    busyTickS=5.0,
    idleTickS=2.0,
    probeGraceS=30.0,
)


def test_timing_rows_match_core_data_literals():
    assert len(TIMING_ROWS) == 1
    assert TIMING_ROWS[0] == _EXPECTED


def test_bridge_startup_grace_s_is_sourced_from_generated_timing():
    src = inspect.getsource(_bridge)
    assert "str(_TIMING.startupGraceS)" in src
    assert _bridge.STARTUP_GRACE_S == 90.0


def test_bridge_heartbeat_constants_are_sourced_from_generated_timing():
    src = inspect.getsource(_bridge_heartbeat)
    for name, field in [
        ("BACKOFF_MIN_S", "backoffMinS"),
        ("BACKOFF_MAX_S", "backoffMaxS"),
        ("RELOAD_BACKOFF_S", "reloadBackoffS"),
        ("ORPHAN_CHECK_INTERVAL_S", "orphanCheckIntervalS"),
        ("BUSY_TICK_S", "busyTickS"),
        ("IDLE_TICK_S", "idleTickS"),
    ]:
        assert f"{name}: float = _TIMING.{field}" in src, f"{name} not sourced from _TIMING.{field}"
    assert _REAL_BACKOFF_MIN_S == 5.0
    assert _REAL_BACKOFF_MAX_S == 60.0
    assert _REAL_RELOAD_BACKOFF_S == 1.0
    assert _REAL_ORPHAN_CHECK_INTERVAL_S == 1.0
    assert _REAL_BUSY_TICK_S == 5.0
    assert _REAL_IDLE_TICK_S == 2.0


def test_bridge_reload_state_constant_is_sourced_from_generated_timing():
    src = inspect.getsource(_bridge_reload_state)
    assert "DOMAIN_RELOAD_EXPIRY_S: float = _TIMING.domainReloadExpiryS" in src
    assert _bridge_reload_state.DOMAIN_RELOAD_EXPIRY_S == 90.0


def test_phantom_cleaner_probe_grace_s_is_sourced_from_generated_timing():
    src = inspect.getsource(_phantom_cleaner)
    assert "PROBE_GRACE_S: float = _TIMING.probeGraceS" in src
    assert _phantom_cleaner.PROBE_GRACE_S == 30.0
    # Documented invariant: the sweep cadence and the probe-persistence grace
    # window stay numerically equal (phantom_cleaner.py's own derivation
    # rationale), now pinned as a test instead of a Python-level alias.
    assert _phantom_cleaner.PROBE_GRACE_S == _phantom_cleaner.PORT_SWEEP_INTERVAL_S
