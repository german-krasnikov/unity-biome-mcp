"""Completeness guard for tests/_fast_clock.py's autouse zero-patch list.

Arch 03 M1 / 05 M2: _RETRY_BACKOFF_CAP_S feeds bridge_retry's capped-backoff
formula (min(BASE**(attempt+1), CAP)) but was missing from _PATCH_TARGETS.
Harmless today because _RETRY_BACKOFF_BASE_S is patched to 0.0 (0**n == 0 for
all n > 0, so the cap never binds) -- but any test that patches BASE_S to a
non-zero "fast" value would silently reintroduce a real 8.0s wall-clock cap
without this guard.
"""
import _fast_clock

import unity_mcp.bridge_retry as bridge_retry


def test_retry_backoff_cap_s_is_a_fast_clock_patch_target():
    assert ("unity_mcp.bridge_retry", "_RETRY_BACKOFF_CAP_S") in _fast_clock._PATCH_TARGETS


def test_retry_backoff_cap_s_is_zeroed_by_the_autouse_fixture():
    # No @pytest.mark.real_clock on this test -- the autouse fixture must have
    # already patched the module attribute to 0.0 by the time this test body runs.
    assert bridge_retry._RETRY_BACKOFF_CAP_S == 0.0
