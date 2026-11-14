"""BridgeState FSM — single-writer ratchet + _transition() behavior.

`self._state` must be written only from `__init__` (construction) or from
`UnityBridge._transition()` (the FSM's single writer). This file pins both:

1. A regex source-scan ratchet (fails if a new raw `self._state = ...`
   assignment appears anywhere outside those two places).
2. Behavioral scenarios for `_transition()` itself: unguarded targets
   (FAILED/DOMAIN_RELOADING/CONNECTED reachable from any state), guarded
   edges (CONNECTED->DORMANT, DORMANT->WAKING only), and the folded
   DomainReloadTracker mark()/clear() side effects.
"""
import re
from pathlib import Path

import pytest
from helpers import make_idle_probe

from unity_mcp.bridge import BridgeState, UnityBridge

_ROOT = Path(__file__).resolve().parents[2]
_BRIDGE_PY = _ROOT / "server" / "src" / "unity_mcp" / "bridge.py"
_ASSIGN_RE = re.compile(r"^\s*self\._state\s*=(?!=)")  # exclude `==`
_INIT_LINE_RE = re.compile(r"self\._state:\s*BridgeState\s*=\s*BridgeState\.DISCONNECTED")


def test_only_transition_and_init_write_self_state():
    """Ratchet: every `self._state = ...` line in bridge.py must be either the
    __init__ construction line or inside the _transition() method body."""
    lines = _BRIDGE_PY.read_text(encoding="utf-8").splitlines()

    def_start = None
    for i, line in enumerate(lines):
        if line.startswith("    def _transition("):
            def_start = i
            break
    assert def_start is not None, "_transition() method not found in bridge.py"

    def_end = len(lines)
    for i in range(def_start + 1, len(lines)):
        line = lines[i]
        if line and not line[0].isspace():
            def_end = i
            break
        if line.startswith("    def ") or line.startswith("    async def "):
            def_end = i
            break

    offenders = [
        i for i, line in enumerate(lines)
        if _ASSIGN_RE.match(line) and not (def_start <= i < def_end) and not _INIT_LINE_RE.search(line)
    ]
    assert not offenders, (
        f"self._state assigned outside _transition()/__init__ at line(s): "
        f"{[n + 1 for n in offenders]}"
    )


def _bridge() -> UnityBridge:
    return UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())


def test_transition_sets_state():
    bridge = _bridge()
    assert bridge._state == BridgeState.DISCONNECTED
    bridge._transition(BridgeState.CONNECTED)
    assert bridge._state == BridgeState.CONNECTED


def test_transition_domain_reloading_marks_tracker_atomically():
    bridge = _bridge()
    bridge._transition(BridgeState.DOMAIN_RELOADING)
    assert bridge._reload.is_active() is True


def test_transition_connected_clears_tracker_atomically():
    bridge = _bridge()
    bridge._reload.mark()
    bridge._transition(BridgeState.CONNECTED)
    assert bridge._reload.is_active() is False


def test_transition_dormant_requires_connected_source():
    bridge = _bridge()
    bridge._state = BridgeState.DISCONNECTED  # test-only poke, bypasses _transition
    with pytest.raises(AssertionError):
        bridge._transition(BridgeState.DORMANT)


def test_transition_connected_to_dormant_allowed():
    bridge = _bridge()
    bridge._state = BridgeState.CONNECTED
    bridge._transition(BridgeState.DORMANT)  # must not raise
    assert bridge._state == BridgeState.DORMANT


def test_transition_waking_requires_dormant_source():
    bridge = _bridge()
    bridge._state = BridgeState.CONNECTED
    with pytest.raises(AssertionError):
        bridge._transition(BridgeState.WAKING)


def test_transition_dormant_to_waking_allowed():
    bridge = _bridge()
    bridge._state = BridgeState.DORMANT
    bridge._transition(BridgeState.WAKING)  # must not raise
    assert bridge._state == BridgeState.WAKING
