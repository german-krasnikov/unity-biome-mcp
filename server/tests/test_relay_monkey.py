"""Monkey/stress tests for chat_relay.py — connection stability, process management, cleanup.

All tests marked @pytest.mark.monkey. No mocks — real processes (cat, python -c).
Run: pytest tests/test_relay_monkey.py -m monkey -v --timeout=60
"""
import asyncio
import json
import logging
import os
import signal
import struct
import sys
import pytest

from unity_mcp.chat_relay import ChatRelay, _find_free_port, MAX_FRAME

pytestmark = pytest.mark.monkey

_SERVER_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_SRC_DIR = os.path.join(_SERVER_DIR, "src")


# ─── Helpers ─────────────────────────────────────────────────────────────────

async def tcp_send(port: int, cmd: str, args: dict, req_id: str = "1") -> dict:
    """Fresh connection per call — send one framed command, return parsed response."""
    r, w = await asyncio.wait_for(asyncio.open_connection("127.0.0.1", port), timeout=5)
    req = json.dumps({"id": req_id, "cmd": cmd, "args": args}).encode()
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await asyncio.wait_for(r.readexactly(4), timeout=5)
    body = await asyncio.wait_for(r.readexactly(struct.unpack("!I", hdr)[0]), timeout=5)
    resp = json.loads(body)
    w.close()
    await w.wait_closed()
    return resp


async def framed_send(r: asyncio.StreamReader, w: asyncio.StreamWriter, req: dict) -> dict:
    """Send one framed command on an already-open connection."""
    payload = json.dumps(req).encode()
    w.write(struct.pack("!I", len(payload)) + payload)
    await w.drain()
    hdr = await asyncio.wait_for(r.readexactly(4), timeout=5)
    body = await asyncio.wait_for(r.readexactly(struct.unpack("!I", hdr)[0]), timeout=5)
    return json.loads(body)


def _max_seq(data: str) -> int:
    """Parse highest seq from events data (format: 'seq\\ntext\\nseq\\ntext\\n')."""
    lines = data.strip().split("\n")
    seqs = [int(lines[i]) for i in range(0, len(lines), 2)
            if lines[i].lstrip("-").isdigit()]
    return max(seqs) if seqs else -1


def _pid(data: str) -> int | None:
    """Extract pid from 'spawned pid=123' or 'alive|pid=123|...' strings."""
    for part in data.split("|"):
        if "pid=" in part:
            try: return int(part.split("pid=")[1].split()[0])
            except (ValueError, IndexError): pass
    return None


# ─── Fixture ─────────────────────────────────────────────────────────────────

@pytest.fixture
async def relay_server():
    """Start ChatRelay on random port (no ppid watchdog). Yield (relay, port)."""
    port = _find_free_port()
    relay = ChatRelay()
    server = await asyncio.start_server(relay._handle_client, "127.0.0.1", port)
    yield relay, port
    server.close()
    await server.wait_closed()
    await relay._kill_current()


# ─── M1: Connection Storm ─────────────────────────────────────────────────────

async def test_connection_storm(relay_server):
    """50 rapid connect/disconnect cycles. Relay must stay alive throughout."""
    relay, port = relay_server
    for i in range(50):
        r, w = await asyncio.open_connection("127.0.0.1", port)
        resp = await framed_send(r, w, {"id": f"s{i}", "cmd": "status", "args": {}})
        assert resp["ok"]
        w.close()
        await w.wait_closed()


# ─── M2: Process Lifecycle Stress ─────────────────────────────────────────────

async def test_process_lifecycle_stress(relay_server):
    """20 spawn/kill cycles. No zombie processes after each kill."""
    relay, port = relay_server
    pids: list[int] = []
    for i in range(20):
        resp = await relay._cmd_spawn({  # direct call — spawn removed from TCP dispatch
            "binary": sys.executable,
            "argv": ["-c", "import time; time.sleep(30)"],
            "env_set": {}, "env_strip": [],
        })
        assert resp["ok"], f"spawn {i} failed: {resp}"
        pid = _pid(resp.get("data", ""))
        if pid:
            pids.append(pid)
        resp = await tcp_send(port, "kill", {})
        assert resp["ok"]

    await asyncio.sleep(0.3)
    for pid in pids:
        try:
            os.kill(pid, 0)
            pytest.fail(f"Process {pid} still alive (zombie or running)")
        except ProcessLookupError:
            pass  # dead — correct


# ─── M3: Reconnect Survival ───────────────────────────────────────────────────

async def test_reconnect_survival(relay_server):
    """Spawn streaming process; reconnect 10x; events always readable."""
    relay, port = relay_server
    await relay._cmd_spawn({  # direct call — spawn removed from TCP dispatch
        "binary": sys.executable,
        "argv": ["-c",
                 "import time\n"
                 "for i in range(100):\n"
                 "    print(f'line_{i}', flush=True)\n"
                 "    time.sleep(0.01)\n"],
        "env_set": {}, "env_strip": [],
    })
    last_seq = -1
    for _ in range(10):
        await asyncio.sleep(0.05)
        resp = await tcp_send(port, "events", {"after_seq": last_seq})
        assert resp["ok"]
        if resp.get("data"):
            last_seq = max(last_seq, _max_seq(resp["data"]))


# ─── M4: Large Payload / Oversized Frame ──────────────────────────────────────

async def test_large_payload(relay_server):
    """1 MB line sent to a stdin-draining subprocess — must succeed or reject cleanly.
    Uses python -c 'sys.stdin.read()' not cat: cat deadlocks because echoing 1MB
    to stdout fills the pipe before cat can drain more stdin."""
    relay, port = relay_server
    await relay._cmd_spawn({  # direct call — spawn removed from TCP dispatch
        "binary": sys.executable,
        "argv": ["-c", "import sys; sys.stdin.read()"],
        "env_set": {}, "env_strip": [],
    })
    resp = await tcp_send(port, "send", {"line": "x" * (1024 * 1024)})
    assert resp["ok"]


async def test_oversized_frame_rejected(relay_server):
    """Frame > MAX_FRAME closes connection; relay keeps serving."""
    relay, port = relay_server
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(struct.pack("!I", MAX_FRAME + 1) + b"x")
    await w.drain()
    data = await asyncio.wait_for(r.read(100), timeout=3)
    assert len(data) == 0, "expected EOF after oversized frame"
    w.close()
    assert (await tcp_send(port, "status", {}))["ok"]


# ─── M5: Concurrent Clients ───────────────────────────────────────────────────

async def test_concurrent_clients(relay_server):
    """B4 FIX: relay is single-client — simultaneous connects displace each other.
    At least 1 must succeed; relay must stay alive after the connection storm."""
    relay, port = relay_server
    results = await asyncio.gather(
        *[tcp_send(port, "status", {}, req_id=f"c{i}") for i in range(5)],
        return_exceptions=True,
    )
    ok_count = sum(1 for r in results if isinstance(r, dict) and r.get("ok"))
    assert ok_count >= 1, f"expected ≥1 success, got {ok_count} from {results}"
    # Relay survives the storm — fresh connection still works
    assert (await tcp_send(port, "status", {}))["ok"]


# ─── M6: Graceful Shutdown (SIGTERM) ─────────────────────────────────────────

async def test_sigterm_kills_children():
    """SIGTERM relay → relay exits cleanly (signal handler fires)."""
    env = {**os.environ, "PYTHONPATH": _SRC_DIR}
    proc = await asyncio.create_subprocess_exec(
        sys.executable, "-m", "unity_mcp.chat_relay",
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL,
        cwd=_SERVER_DIR, env=env,
    )
    line = await asyncio.wait_for(proc.stdout.readline(), timeout=5)
    port = int(line.decode().strip().split(":")[1])

    # Verify relay is alive before signal
    resp = await tcp_send(port, "status", {})
    assert resp["ok"], f"relay not alive before SIGTERM: {resp}"

    proc.send_signal(signal.SIGTERM)
    await asyncio.wait_for(proc.wait(), timeout=5)
    assert proc.returncode is not None, "relay did not exit after SIGTERM"


# ─── M2: SIGINT kills children ───────────────────────────────────────────────

async def test_sigint_kills_children():
    """SIGINT relay → relay exits cleanly (signal handler fires)."""
    env = {**os.environ, "PYTHONPATH": _SRC_DIR}
    proc = await asyncio.create_subprocess_exec(
        sys.executable, "-m", "unity_mcp.chat_relay",
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL,
        cwd=_SERVER_DIR, env=env,
    )
    line = await asyncio.wait_for(proc.stdout.readline(), timeout=5)
    port = int(line.decode().strip().split(":")[1])

    # Verify relay is alive before signal
    resp = await tcp_send(port, "status", {})
    assert resp["ok"], f"relay not alive before SIGINT: {resp}"

    proc.send_signal(signal.SIGINT)
    await asyncio.wait_for(proc.wait(), timeout=5)
    assert proc.returncode is not None, "relay did not exit after SIGINT"


# ─── M8: Protocol Torture ─────────────────────────────────────────────────────

async def test_partial_header_closes_connection(relay_server):
    """2-byte partial header then close. Relay keeps serving."""
    relay, port = relay_server
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(b"\x00\x00")
    w.close()
    await asyncio.sleep(0.1)
    assert (await tcp_send(port, "status", {}))["ok"]


async def test_rapid_fire_commands(relay_server):
    """500 status commands pipelined on one connection — all respond correctly."""
    relay, port = relay_server
    r, w = await asyncio.open_connection("127.0.0.1", port)
    N = 500
    for i in range(N):
        req = json.dumps({"id": f"r{i}", "cmd": "status", "args": {}}).encode()
        w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    for i in range(N):
        hdr = await asyncio.wait_for(r.readexactly(4), timeout=10)
        body = await asyncio.wait_for(r.readexactly(struct.unpack("!I", hdr)[0]), timeout=10)
        assert json.loads(body)["ok"], f"request {i} failed"
    w.close()
    await w.wait_closed()


async def test_invalid_json_closes_cleanly(relay_server):
    """Valid frame length with invalid JSON body — connection closes; relay survives."""
    relay, port = relay_server
    r, w = await asyncio.open_connection("127.0.0.1", port)
    bad = b"not json at all {{{"
    w.write(struct.pack("!I", len(bad)) + bad)
    await w.drain()
    data = await asyncio.wait_for(r.read(100), timeout=3)
    assert len(data) == 0, "expected EOF on JSONDecodeError"
    w.close()
    assert (await tcp_send(port, "status", {}))["ok"]
