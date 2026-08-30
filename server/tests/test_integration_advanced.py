"""Advanced integration tests with real TCP server."""
import asyncio
import json
import struct
from unity_mcp.bridge import UnityBridge


async def test_ten_sequential_commands(mock_unity_server):
    """Send 10 different commands sequentially, all return correctly."""
    for i in range(10):
        mock_unity_server.set_response(f"cmd{i}", f"result{i}")

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    for i in range(10):
        result = await bridge.send(f"cmd{i}", {"index": i})
        assert result["ok"] is True
        assert result["data"] == f"result{i}"

    await bridge.close()


async def test_concurrent_commands(mock_unity_server):
    """Send 5 commands concurrently via asyncio.gather."""
    for i in range(5):
        mock_unity_server.set_response(f"parallel{i}", f"concurrent_result{i}")

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    tasks = [bridge.send(f"parallel{i}", {}) for i in range(5)]
    results = await asyncio.gather(*tasks)

    assert len(results) == 5
    for i, result in enumerate(results):
        assert result["ok"] is True
        assert result["data"] == f"concurrent_result{i}"

    await bridge.close()


async def test_slow_server_response():
    """Server waits 0.5s before responding, bridge handles correctly."""
    async def slow_handler(reader, writer):
        try:
            header = await reader.readexactly(4)
            length = struct.unpack("!I", header)[0]
            payload = await reader.readexactly(length)
            request = json.loads(payload.decode("utf-8"))

            await asyncio.sleep(0.5)  # Simulate slow processing

            msg_id = request["id"]
            response = {"id": msg_id, "ok": True, "data": "slow_response"}

            resp_payload = json.dumps(response).encode("utf-8")
            resp_header = struct.pack("!I", len(resp_payload))
            writer.write(resp_header + resp_payload)
            await writer.drain()
        except asyncio.IncompleteReadError:
            pass
        writer.close()

    server = await asyncio.start_server(slow_handler, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]

    bridge = UnityBridge(port=port)
    await bridge.connect()

    start = asyncio.get_running_loop().time()
    result = await bridge.send("slow_cmd", {})
    elapsed = asyncio.get_running_loop().time() - start

    assert result["data"] == "slow_response"
    assert elapsed >= 0.5

    await bridge.close()
    server.close()
    await server.wait_closed()


async def test_chunked_response():
    """Server sends header and payload separately (2 drain calls)."""
    async def chunked_handler(reader, writer):
        try:
            header = await reader.readexactly(4)
            length = struct.unpack("!I", header)[0]
            payload = await reader.readexactly(length)
            request = json.loads(payload.decode("utf-8"))

            msg_id = request["id"]
            response = {"id": msg_id, "ok": True, "data": "chunked_data"}

            resp_payload = json.dumps(response).encode("utf-8")
            resp_header = struct.pack("!I", len(resp_payload))

            # Send header
            writer.write(resp_header)
            await writer.drain()

            # Small delay
            await asyncio.sleep(0.1)

            # Send payload
            writer.write(resp_payload)
            await writer.drain()
        except asyncio.IncompleteReadError:
            pass
        writer.close()

    server = await asyncio.start_server(chunked_handler, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]

    bridge = UnityBridge(port=port)
    await bridge.connect()

    result = await bridge.send("chunked_cmd", {})

    assert result["ok"] is True
    assert result["data"] == "chunked_data"

    await bridge.close()
    server.close()
    await server.wait_closed()


async def test_unicode_data(mock_unity_server):
    """Send and receive unicode (cyrillic, emoji) in data."""
    unicode_data = "Привет мир 🎉 🚀"
    mock_unity_server.set_response("unicode_cmd", unicode_data)

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    result = await bridge.send("unicode_cmd", {"text": "Тест 😀"})

    assert result["ok"] is True
    assert result["data"] == unicode_data

    await bridge.close()


async def test_empty_data(mock_unity_server):
    """Server returns empty string in data field."""
    mock_unity_server.set_response("empty_cmd", "")

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    result = await bridge.send("empty_cmd", {})

    assert result["ok"] is True
    assert result["data"] == ""

    await bridge.close()


async def test_binary_like_data(mock_unity_server):
    """Server returns base64 string (simulates screenshot)."""
    base64_data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ"
    mock_unity_server.set_response("screenshot", base64_data)

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    result = await bridge.send("screenshot", {"width": 1920, "height": 1080})

    assert result["ok"] is True
    assert result["data"] == base64_data

    await bridge.close()


async def test_large_args_and_response(mock_unity_server):
    """Send large args dict and receive large data response."""
    large_args = {f"param_{i}": f"value_{i}" * 50 for i in range(50)}
    large_response = {f"key_{i}": f"data_{i}" * 100 for i in range(50)}

    mock_unity_server.set_response("large_cmd", large_response)

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    result = await bridge.send("large_cmd", large_args)

    assert result["ok"] is True
    assert result["data"] == large_response

    await bridge.close()


async def test_mixed_sequential_and_concurrent(mock_unity_server):
    """Mix of sequential and concurrent commands."""
    for i in range(10):
        mock_unity_server.set_response(f"cmd{i}", f"result{i}")

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    # Sequential
    r1 = await bridge.send("cmd0", {})
    assert r1["data"] == "result0"

    # Concurrent batch
    batch1 = await asyncio.gather(
        bridge.send("cmd1", {}),
        bridge.send("cmd2", {}),
        bridge.send("cmd3", {})
    )
    assert batch1[0]["data"] == "result1"
    assert batch1[1]["data"] == "result2"

    # Sequential
    r2 = await bridge.send("cmd4", {})
    assert r2["data"] == "result4"

    # Another concurrent batch
    batch2 = await asyncio.gather(
        bridge.send("cmd5", {}),
        bridge.send("cmd6", {})
    )
    assert batch2[0]["data"] == "result5"
    assert batch2[1]["data"] == "result6"

    await bridge.close()


async def test_message_id_increments_correctly(mock_unity_server):
    """Message IDs increment sequentially across multiple commands."""
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    for i in range(5):
        await bridge.send(f"cmd{i}", {})

    assert bridge._counter == 5

    await bridge.close()


async def test_reconnect_during_commands(mock_unity_server):
    """A retry-safe command reconnects after a lost ACK without losing identity.

    The explicit predicate models a read-only/idempotent public annotation;
    unknown commands remain fail-closed in production.
    """
    connection_count = 0
    received_requests = []

    async def unstable_handler(reader, writer):
        nonlocal connection_count
        connection_count += 1

        try:
            while True:
                header = await reader.readexactly(4)
                length = struct.unpack("!I", header)[0]
                payload = await reader.readexactly(length)
                request = json.loads(payload.decode("utf-8"))
                received_requests.append(request)

                # Drop connection on first command
                if connection_count == 1:
                    writer.close()
                    return

                msg_id = request["id"]
                response = {"id": msg_id, "ok": True, "data": "reconnected"}

                resp_payload = json.dumps(response).encode("utf-8")
                resp_header = struct.pack("!I", len(resp_payload))
                writer.write(resp_header + resp_payload)
                await writer.drain()
        except asyncio.IncompleteReadError:
            pass
        writer.close()

    server = await asyncio.start_server(unstable_handler, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]

    bridge = UnityBridge(
        port=port,
        is_retry_safe=lambda name: name == "cmd",
    )
    await bridge.connect()

    # The opted-in command may reconnect and resend after the server received
    # the first frame but its response was lost.
    result = await bridge.send("cmd", {})
    assert result["ok"] is True
    assert connection_count >= 2, "Bridge should have reconnected automatically"
    assert result["data"] == "reconnected"
    assert connection_count == 2  # Initial + reconnect
    command_requests = [
        request for request in received_requests if request.get("cmd") == "cmd"
    ]
    assert len(command_requests) == 2
    assert command_requests[1]["op_id"] == command_requests[0]["op_id"]
    assert command_requests[1]["retry_op_id"] == command_requests[0]["op_id"]

    await bridge.close()
    server.close()
    await server.wait_closed()
