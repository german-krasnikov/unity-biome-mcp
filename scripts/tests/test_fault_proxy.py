
import asyncio
import socket
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from fault_proxy import FaultProxy, read_frame, write_frame


def find_free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


async def echo_once(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    try:
        payload = await read_frame(reader)
        write_frame(writer, payload)
        await writer.drain()
    except Exception:
        pass
    finally:
        writer.close()


def _make_proxy(echo_port: int, proxy_port: int, mode: str, **kwargs: object) -> FaultProxy:
    return FaultProxy("127.0.0.1", echo_port, proxy_port, mode, **kwargs)


# --- frame helpers ---


def test_read_frame_and_write_frame() -> None:
    async def _inner() -> None:
        port = find_free_port()
        captured: list[bytes] = []

        async def handler(r: asyncio.StreamReader, w: asyncio.StreamWriter) -> None:
            captured.append(await read_frame(r))
            w.close()

        srv = await asyncio.start_server(handler, "127.0.0.1", port)
        async with srv:
            r, w = await asyncio.open_connection("127.0.0.1", port)
            write_frame(w, b"hello-world")
            await w.drain()
            await asyncio.sleep(0.05)

        assert captured == [b"hello-world"]

    asyncio.run(_inner())


# --- proxy mode tests ---


def test_passthrough_forwards_frame() -> None:
    async def _inner() -> None:
        echo_port, proxy_port = find_free_port(), find_free_port()
        proxy = _make_proxy(echo_port, proxy_port, "passthrough")

        echo_srv = await asyncio.start_server(echo_once, "127.0.0.1", echo_port)
        proxy_srv = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)

        async with echo_srv, proxy_srv:
            r, w = await asyncio.open_connection("127.0.0.1", proxy_port)
            payload = b'{"cmd":"ping"}'
            write_frame(w, payload)
            await w.drain()
            response = await asyncio.wait_for(read_frame(r), timeout=2)
            w.close()

        assert response == payload

    asyncio.run(_inner())


def test_drop_ack_no_response() -> None:
    async def _inner() -> None:
        echo_port, proxy_port = find_free_port(), find_free_port()
        proxy = _make_proxy(echo_port, proxy_port, "drop_ack")

        echo_srv = await asyncio.start_server(echo_once, "127.0.0.1", echo_port)
        proxy_srv = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)

        async with echo_srv, proxy_srv:
            r, w = await asyncio.open_connection("127.0.0.1", proxy_port)
            write_frame(w, b'{"cmd":"ping"}')
            await w.drain()

            with pytest.raises((asyncio.TimeoutError, asyncio.IncompleteReadError)):
                await asyncio.wait_for(read_frame(r), timeout=0.5)
            w.close()

    asyncio.run(_inner())


def test_disconnect_mid_frame_partial() -> None:
    async def _inner() -> None:
        echo_port, proxy_port = find_free_port(), find_free_port()
        proxy = _make_proxy(echo_port, proxy_port, "disconnect_mid_frame")

        echo_srv = await asyncio.start_server(echo_once, "127.0.0.1", echo_port)
        proxy_srv = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)

        async with echo_srv, proxy_srv:
            r, w = await asyncio.open_connection("127.0.0.1", proxy_port)
            write_frame(w, b"x" * 100)  # 104-byte frame; half = 52 → client gets partial payload
            await w.drain()

            with pytest.raises(asyncio.IncompleteReadError):
                await asyncio.wait_for(read_frame(r), timeout=2)
            w.close()

    asyncio.run(_inner())


def test_duplicate_frame_sends_twice() -> None:
    async def _inner() -> None:
        echo_port, proxy_port = find_free_port(), find_free_port()
        proxy = _make_proxy(echo_port, proxy_port, "duplicate_frame")

        echo_srv = await asyncio.start_server(echo_once, "127.0.0.1", echo_port)
        proxy_srv = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)

        async with echo_srv, proxy_srv:
            r, w = await asyncio.open_connection("127.0.0.1", proxy_port)
            payload = b'{"cmd":"ping"}'
            write_frame(w, payload)
            await w.drain()

            frame1 = await asyncio.wait_for(read_frame(r), timeout=2)
            frame2 = await asyncio.wait_for(read_frame(r), timeout=2)
            w.close()

        assert frame1 == payload
        assert frame2 == payload

    asyncio.run(_inner())


def test_fault_count_limits_faults() -> None:
    """First request is faulted; second connection (after fault_count exhausted) passes through."""

    async def _inner() -> None:
        echo_port, proxy_port = find_free_port(), find_free_port()
        proxy = _make_proxy(echo_port, proxy_port, "drop_ack", fault_count=1)

        echo_srv = await asyncio.start_server(echo_once, "127.0.0.1", echo_port)
        proxy_srv = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)

        async with echo_srv, proxy_srv:
            # First request → faulted (no response)
            r1, w1 = await asyncio.open_connection("127.0.0.1", proxy_port)
            write_frame(w1, b'{"cmd":"first"}')
            await w1.drain()
            with pytest.raises((asyncio.TimeoutError, asyncio.IncompleteReadError)):
                await asyncio.wait_for(read_frame(r1), timeout=0.5)
            w1.close()
            await asyncio.sleep(0.1)

            # Second request → passthrough
            r2, w2 = await asyncio.open_connection("127.0.0.1", proxy_port)
            payload = b'{"cmd":"second"}'
            write_frame(w2, payload)
            await w2.drain()
            response = await asyncio.wait_for(read_frame(r2), timeout=2)
            w2.close()

        assert response == payload

    asyncio.run(_inner())
