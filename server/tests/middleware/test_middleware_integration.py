"""Integration tests for middleware cache pipeline: write → prefetch → cache hit."""
import asyncio
import os
os.environ.setdefault("UNITY_MCP_VALIDATE", "0")
os.environ.setdefault("UNITY_MCP_PREFETCH_CACHE", "1")


async def test_write_triggers_prefetch_then_read_serves_cache():
    """set_property fires bg prefetch; subsequent get_component hits cache."""
    from unity_mcp.middleware import wrap_send, Middleware

    mw = Middleware()
    call_log = []

    async def mock_send(cmd, args, timeout=30.0):
        call_log.append((cmd, dict(args)))
        return f"result-{cmd}"

    wrapped = wrap_send(mock_send, mw)

    # Write — triggers background prefetch for get_component
    await wrapped("set_property", {
        "path": "/A", "component": "Health", "prop": "hp", "value": "100"
    })

    # Wait for background task to complete
    await asyncio.sleep(0.05)

    # Subsequent read should hit cache (prefetch already populated it)
    result = await wrapped("get_component", {"path": "/A", "type": "Health"})

    assert "[CACHED]" in result, f"Expected cache hit, got: {result!r}"

    # Background fetched 1 get_component; cache served second — only 1 call to bridge
    gc_calls = [c for c in call_log if c[0] == "get_component"]
    assert len(gc_calls) == 1, f"Cache should serve 2nd call, got {len(gc_calls)} bridge calls"


async def test_cached_marker_no_timestamp():
    """Cache hit response uses [CACHED] without fake timestamp."""
    from unity_mcp.middleware import wrap_send, Middleware

    mw = Middleware()

    async def mock_send(cmd, args, timeout=30.0):
        return "some-data"

    wrapped = wrap_send(mock_send, mw)

    await wrapped("set_property", {
        "path": "/B", "component": "Stats", "prop": "x", "value": "1"
    })
    await asyncio.sleep(0.05)

    result = await wrapped("get_component", {"path": "/B", "type": "Stats"})
    assert "[CACHED]" in result
    assert "0.00s" not in result
