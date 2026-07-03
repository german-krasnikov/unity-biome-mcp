from unity_mcp.server import batch


async def test_batch_passes_timeout_ms(mock_bridge, bridge_response):
    """Non-default timeout → timeout_ms present in args sent to bridge."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=60.0)
    args = mock_bridge.send.call_args[0][1]
    assert "timeout_ms" in args


async def test_batch_default_timeout_75s(mock_bridge, bridge_response):
    """Default timeout=75 (A4: clears Unity's 65s batch ceiling) → timeout_ms=70000
    is sent (differs from C#'s hardcoded internal default of 25000, so it's no
    longer omitted for token economy — Unity's internal batch-executor deadline
    now scales with the new, more patient client timeout)."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == 70000


async def test_batch_custom_timeout(mock_bridge, bridge_response):
    """timeout=60 → timeout_ms=55000."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=60.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == 55000


async def test_batch_default_timeout_exceeds_csharp_ceiling(mock_bridge, bridge_response):
    """M4 regression: default client timeout must clear Unity's 65s 'batch' ceiling
    (MCPServer.cs CommandTimeouts) with margin, or Python can give up while a
    legitimate slow batch is still executing on Unity's main thread."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    call_args = mock_bridge.send.call_args
    assert call_args[1]["timeout"] > 65.0
