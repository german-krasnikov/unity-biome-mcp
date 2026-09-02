from unity_mcp.server import batch
from unity_mcp.tools.batch import _TIMEOUT_MS_CEILING, _BATCH_DISPATCH_GUARD_S
from helpers import CSHARP_TIMEOUT_OVERRIDES as _CSHARP_OVERRIDES


async def test_batch_default_timeout_75s(mock_bridge, bridge_response):
    """Default timeout=75 → raw formula gives 70000, but DEV-55 clamps
    timeout_ms to 60000 (C#'s 'batch' outer dispatch watchdog is 65s —
    MCPServer.cs CommandTimeouts — so the inner soft-timeout sent to Unity
    must stay below it with margin, or the outer watchdog kills the command
    before Unity's own batch executor can return a graceful partial result)."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == _TIMEOUT_MS_CEILING


async def test_batch_custom_timeout(mock_bridge, bridge_response):
    """timeout=60 → timeout_ms=55000 (below the 60000 ceiling, unclamped)."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=60.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == int((60 - _BATCH_DISPATCH_GUARD_S) * 1000)


async def test_batch_default_timeout_exceeds_csharp_ceiling(mock_bridge, bridge_response):
    """M4 regression: default client timeout must clear Unity's 65s 'batch' ceiling
    (MCPServer.cs CommandTimeouts) with margin, or Python can give up while a
    legitimate slow batch is still executing on Unity's main thread."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    call_args = mock_bridge.send.call_args
    assert call_args[1]["timeout"] > _CSHARP_OVERRIDES["batch"]


async def test_batch_timeout_ms_never_exceeds_csharp_ceiling(mock_bridge, bridge_response):
    """DEV-55 [B3-#11]: the caller-tunable inner timeout_ms must never reach
    C#'s hardcoded outer 'batch' dispatch watchdog (65s, MCPServer.cs
    CommandTimeouts). Without the clamp, timeout=75 → timeout_ms=70000 >
    65000 -- the outer watchdog kills the whole command before Unity's own
    soft-timeout can return a partial result. Red before the fix (70000)."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=75.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] <= _TIMEOUT_MS_CEILING


async def test_batch_low_timeout_ms_unaffected_by_ceiling_clamp(mock_bridge, bridge_response):
    """Sanity/regression guard: the 60000 ceiling must not disturb the
    existing formula for ordinary (well-below-ceiling) timeout values."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=10.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == 5000
