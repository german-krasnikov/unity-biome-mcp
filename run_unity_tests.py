#!/usr/bin/env python3
"""Standalone Unity NUnit test runner.

Usage:
    python run_unity_tests.py [EditMode|PlayMode] [--filter=TestClass1|TestClass2]
    UNITY_MCP_PORT=9501 python run_unity_tests.py PlayMode
"""

import asyncio
import json
import pathlib
import os
import re
import struct
import sys
import time

# ── port discovery ────────────────────────────────────────────────────────────

def find_port() -> int:
    p = int(os.environ.get("UNITY_MCP_PORT", "0"))
    if p:
        return p
    for f in pathlib.Path.home().glob(".unity-biome-mcp/ports/*.port"):
        try:
            return int(f.read_text().split("\n")[0])
        except Exception:
            pass
    return 9500


# ── low-level TCP helpers ─────────────────────────────────────────────────────

CONNECT_TIMEOUT = 10.0
_counter = 0


def _next_id() -> str:
    global _counter
    _counter += 1
    return f"{_counter:04x}"


async def _send_raw(writer: asyncio.StreamWriter, cmd: str, args: dict) -> None:
    msg_id = _next_id()
    payload = json.dumps({"id": msg_id, "cmd": cmd, "args": args},
                         ensure_ascii=False).encode("utf-8")
    writer.write(struct.pack("!I", len(payload)) + payload)
    await writer.drain()
    return msg_id


async def _recv_raw(reader: asyncio.StreamReader) -> dict:
    header = await asyncio.wait_for(reader.readexactly(4), timeout=30.0)
    length = struct.unpack("!I", header)[0]
    if length == 0 or length > 10_000_000:
        raise ConnectionError(f"Protocol desync: length={length}")
    payload = await asyncio.wait_for(reader.readexactly(length), timeout=30.0)
    data = json.loads(payload.decode("utf-8"))
    if data.get("ev") == "going_away":
        raise DomainReloadEvent(data.get("reason", "unknown"))
    return data


class DomainReloadEvent(Exception):
    pass


# ── connection ────────────────────────────────────────────────────────────────

async def connect(port: int) -> tuple[asyncio.StreamReader, asyncio.StreamWriter]:
    """Connect and ping — raises on failure."""
    reader, writer = await asyncio.wait_for(
        asyncio.open_connection("127.0.0.1", port),
        timeout=CONNECT_TIMEOUT,
    )
    # Handshake ping (mirrors bridge._reconnect)
    msg_id = await _send_raw(writer, "ping", {})
    pong = await _recv_raw(reader)
    if not pong.get("ok"):
        writer.close()
        raise ConnectionError(f"Ping failed: {pong}")
    print(f"  Connected to Unity on port {port}")
    return reader, writer


async def call(reader, writer, cmd: str, args: dict, timeout: float = 15.0) -> dict:
    msg_id = await _send_raw(writer, cmd, args)
    while True:
        resp = await asyncio.wait_for(_recv_raw(reader), timeout=timeout)
        if resp.get("id") == msg_id:
            return resp
        # Discard responses for other IDs (shouldn't happen in serial use)


# ── main logic ────────────────────────────────────────────────────────────────

POLL_INTERVAL = 2.0
MAX_WAIT = 300.0  # 5 minutes
DOMAIN_RELOAD_WAIT = 15.0  # wait before reconnecting after going_away


async def run(mode: str, filter_: str | None) -> int:
    port = find_port()
    print(f"Unity port: {port}")

    # 1. connect
    reader, writer = await connect(port)

    # 2. fire run_tests (fire-and-forget; TCP may drop — that's expected)
    print(f"  Sending run_tests mode={mode}" +
          (f" filter={filter_}" if filter_ else ""))
    try:
        args = {"mode": mode}
        if filter_:
            args["filter"] = filter_
        resp = await asyncio.wait_for(call(reader, writer, "run_tests", args, timeout=12.0),
                                      timeout=13.0)
        if resp.get("ok") and resp.get("data") not in (None, ""):
            data = resp["data"]
            if isinstance(data, str) and not data.startswith("tests-started"):
                # Got immediate result (rare, but handle it)
                print(f"\nResult: {data}")
                return _exit_code(data)
    except (asyncio.TimeoutError, ConnectionError, DomainReloadEvent):
        pass  # expected — fire-and-forget, domain reload starts test domain
    except Exception as e:
        print(f"  run_tests error (non-fatal): {e}")

    print(f"  Polling get_test_results every {POLL_INTERVAL:.0f}s (max {MAX_WAIT:.0f}s)…")

    # 3. poll loop
    deadline = time.monotonic() + MAX_WAIT
    last_status = ""

    while time.monotonic() < deadline:
        await asyncio.sleep(POLL_INTERVAL)

        # Reconnect if needed
        if writer.is_closing() or reader.at_eof():
            writer, reader = await _reconnect(port, deadline)
            if writer is None:
                print("ERROR: could not reconnect before deadline")
                return 1

        try:
            resp = await asyncio.wait_for(
                call(reader, writer, "get_test_results", {}, timeout=10.0),
                timeout=11.0,
            )
        except DomainReloadEvent as e:
            print(f"  Domain reload: {e} — waiting {DOMAIN_RELOAD_WAIT:.0f}s…")
            writer.close()
            await asyncio.sleep(DOMAIN_RELOAD_WAIT)
            reader, writer = await _reconnect(port, deadline)
            if reader is None:
                return 1
            continue
        except (asyncio.TimeoutError, ConnectionError, OSError) as e:
            print(f"  Connection lost ({type(e).__name__}) — reconnecting…")
            try:
                writer.close()
            except Exception:
                pass
            await asyncio.sleep(3.0)
            reader, writer = await _reconnect(port, deadline)
            if reader is None:
                return 1
            continue
        except Exception as e:
            print(f"  Unexpected error: {e}")
            continue

        data = resp.get("data", "")
        if not isinstance(data, str):
            data = str(data)

        if data in ("pending", ""):
            if data != last_status:
                print(f"  … pending")
                last_status = data
            continue

        if data == "none":
            # Could mean: no run started, or domain reload cleared results.
            # Keep polling — if tests were running they'll finish soon.
            if data != last_status:
                print(f"  … none (waiting for results)")
                last_status = data
            continue

        # Got actual results
        print(f"\nResults:\n  {data.strip()}")
        return _exit_code(data)

    print(f"\nERROR: timed out after {MAX_WAIT:.0f}s waiting for test results")
    return 1


async def _reconnect(port: int, deadline: float):
    """Retry connect until deadline. Returns (reader, writer) or (None, None)."""
    attempt = 0
    while time.monotonic() < deadline:
        attempt += 1
        try:
            reader, writer = await connect(port)
            return reader, writer
        except Exception as e:
            wait = min(5.0 * attempt, 30.0)
            remaining = deadline - time.monotonic()
            if remaining < wait:
                break
            print(f"  Reconnect attempt {attempt} failed ({e}), retry in {wait:.0f}s…")
            await asyncio.sleep(wait)
    return None, None


def _exit_code(result: str) -> int:
    """0 = all passed, 1 = failures or parse error."""
    if "FAILED" in result:
        return 1
    m = re.search(r"(\d+) tests:", result)
    if m and int(m.group(1)) == 0:
        # 0 tests ran — something wrong
        return 1
    return 0


# ── CLI entry point ───────────────────────────────────────────────────────────

def main():
    mode = "EditMode"
    filter_ = None

    for arg in sys.argv[1:]:
        if arg in ("EditMode", "PlayMode"):
            mode = arg
        elif arg.startswith("--filter="):
            filter_ = arg.split("=", 1)[1]
        elif arg in ("-h", "--help"):
            print(__doc__)
            sys.exit(0)
        else:
            print(f"Unknown argument: {arg}")
            sys.exit(1)

    try:
        code = asyncio.run(run(mode, filter_))
    except KeyboardInterrupt:
        print("\nAborted.")
        code = 1

    sys.exit(code)


if __name__ == "__main__":
    main()
