
"""TCP fault-injection proxy for MCP server resilience testing.

Sits between the Python MCP server and Unity Editor:
    MCP Server → fault_proxy (listen_port) → Unity Editor (upstream_port)

Protocol: 4-byte big-endian length prefix + JSON payload.
"""

import argparse
import asyncio
import contextlib
import signal
import struct

MODES = ("passthrough", "drop_ack", "disconnect_mid_frame", "delay_beyond_timeout", "duplicate_frame")


async def read_frame(reader: asyncio.StreamReader) -> bytes:
    """Read one length-prefixed frame from reader."""
    header = await reader.readexactly(4)
    length = struct.unpack(">I", header)[0]
    return await reader.readexactly(length)


def write_frame(writer: asyncio.StreamWriter, payload: bytes) -> None:
    """Write one length-prefixed frame to writer (does not drain)."""
    writer.write(struct.pack(">I", len(payload)) + payload)


class FaultProxy:
    def __init__(
        self,
        upstream_host: str,
        upstream_port: int,
        listen_port: int,
        mode: str,
        fault_count: int = 1,
        delay: float = 30.0,
    ) -> None:
        self.upstream_host = upstream_host
        self.upstream_port = upstream_port
        self.listen_port = listen_port
        self.mode = mode
        self.fault_count = fault_count
        self.delay = delay
        self._faulted = 0
        self._total = 0

    def _should_fault(self) -> bool:
        return self._faulted < self.fault_count

    async def handle_client(self, client_r: asyncio.StreamReader, client_w: asyncio.StreamWriter) -> None:
        try:
            up_r, up_w = await asyncio.open_connection(self.upstream_host, self.upstream_port)
        except OSError:
            client_w.close()
            return
        try:
            await self._proxy_loop(client_r, client_w, up_r, up_w)
        finally:
            up_w.close()
            client_w.close()

    async def _proxy_loop(
        self,
        client_r: asyncio.StreamReader,
        client_w: asyncio.StreamWriter,
        up_r: asyncio.StreamReader,
        up_w: asyncio.StreamWriter,
    ) -> None:
        while True:
            try:
                request = await read_frame(client_r)
            except (asyncio.IncompleteReadError, ConnectionResetError, OSError):
                break

            self._total += 1
            write_frame(up_w, request)
            await up_w.drain()

            try:
                response = await read_frame(up_r)
            except (asyncio.IncompleteReadError, ConnectionResetError, OSError):
                break

            if self._should_fault():
                self._faulted += 1
                keep_going = await self._apply_fault(response, client_w)
                if not keep_going:
                    break
            else:
                write_frame(client_w, response)
                await client_w.drain()

    async def _apply_fault(self, response: bytes, writer: asyncio.StreamWriter) -> bool:
        """Apply the configured fault. Returns True if connection should stay open."""
        if self.mode == "passthrough":
            write_frame(writer, response)
            await writer.drain()
            return True

        if self.mode == "drop_ack":
            return False  # silently discard response, close connection

        if self.mode == "disconnect_mid_frame":
            frame = struct.pack(">I", len(response)) + response
            half = max(1, len(frame) // 2)
            writer.write(frame[:half])
            await writer.drain()
            return False

        if self.mode == "delay_beyond_timeout":
            await asyncio.sleep(self.delay)
            write_frame(writer, response)
            await writer.drain()
            return True

        if self.mode == "duplicate_frame":
            write_frame(writer, response)
            write_frame(writer, response)
            await writer.drain()
            return True

        return True  # unknown mode → passthrough

    def print_stats(self) -> None:
        print(f"\n[fault_proxy] total={self._total} faulted={self._faulted}", flush=True)

    async def run(self) -> None:
        server = await asyncio.start_server(self.handle_client, "127.0.0.1", self.listen_port)
        print(
            f"[fault_proxy] 127.0.0.1:{self.listen_port} → "
            f"{self.upstream_host}:{self.upstream_port}  mode={self.mode}  "
            f"fault_count={self.fault_count}",
            flush=True,
        )

        loop = asyncio.get_running_loop()
        stop = loop.create_future()

        def _sig(_: object) -> None:
            if not stop.done():
                stop.set_result(None)

        for sig in (signal.SIGINT, signal.SIGTERM):
            loop.add_signal_handler(sig, _sig, sig)

        async with server:
            await stop

        self.print_stats()


def _build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="TCP fault-injection proxy for MCP resilience testing")
    p.add_argument("--upstream-host", default="127.0.0.1")
    p.add_argument("--upstream-port", type=int, required=True)
    p.add_argument("--listen-port", type=int, required=True)
    p.add_argument("--mode", choices=MODES, default="passthrough")
    p.add_argument("--fault-count", type=int, default=1, dest="fault_count")
    p.add_argument("--delay", type=float, default=30.0, help="Delay seconds for delay_beyond_timeout mode")
    return p


def main() -> None:
    args = _build_parser().parse_args()
    proxy = FaultProxy(
        upstream_host=args.upstream_host,
        upstream_port=args.upstream_port,
        listen_port=args.listen_port,
        mode=args.mode,
        fault_count=args.fault_count,
        delay=args.delay,
    )
    with contextlib.suppress(KeyboardInterrupt):
        asyncio.run(proxy.run())


if __name__ == "__main__":
    main()
