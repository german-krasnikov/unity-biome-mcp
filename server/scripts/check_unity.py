#!/usr/bin/env python3
"""Unity health diagnostic — stdlib only, single-line output."""

import json
import os
import re
import socket
import struct
import sys
from pathlib import Path

_PORTS_DIR = Path.home() / ".unity-biome-mcp/ports"
_TAIL = 2 * 1024 * 1024  # 2MB — covers large compile error bursts
_TCP_TIMEOUT = 2


def _editor_log_path() -> Path:
    """Cross-platform Editor.log path."""
    import platform
    s = platform.system()
    if s == "Darwin":
        return Path.home() / "Library/Logs/Unity/Editor.log"
    if s == "Windows":
        local = os.environ.get("LOCALAPPDATA", "")
        return Path(local) / "Unity/Editor/Editor.log" if local else Path("Editor.log")
    # Linux
    return Path.home() / ".config/unity3d/Editor.log"


def _read_log(path: str | Path) -> str:
    """Read enough of Editor.log to find the last compile error anchor.
    Searches backwards in 2MB chunks — handles huge logs without loading all."""
    p = Path(path)
    if not p.exists():
        return ""
    FAIL_ANCHOR = b"## Script Compilation Error for:"
    RELOAD_ANCHOR = b"Mono: successfully reloaded assembly"
    CHUNK = _TAIL
    with open(p, "rb") as f:
        f.seek(0, 2)
        size = f.tell()
        # Fast path: last 2MB has both anchors we need
        f.seek(max(0, size - CHUNK))
        tail = f.read()
        if FAIL_ANCHOR in tail or RELOAD_ANCHOR in tail:
            return tail.decode("utf-8", errors="replace")
        # Slow path: scan backwards for FAIL_ANCHOR (rare, only huge logs)
        offset = CHUNK
        while offset < size:
            offset = min(offset + CHUNK, size)
            f.seek(max(0, size - offset))
            chunk = f.read(min(CHUNK + 256, offset))  # overlap for split anchors
            if FAIL_ANCHOR in chunk:
                # Found it — read from this point to end
                pos = size - offset + chunk.find(FAIL_ANCHOR)
                f.seek(max(0, pos - 1024))  # include context before anchor
                return f.read().decode("utf-8", errors="replace")
        # No anchors found at all
        return tail.decode("utf-8", errors="replace")


def parse_log(text: str) -> dict:
    """Return {status: ok|error|compiling, detail: str}."""
    FAIL_ANCHOR = "## Script Compilation Error for:"
    RELOAD_ANCHOR = "Mono: successfully reloaded assembly"
    COMPILING_ANCHOR = "Compiling script assemblies"

    last_fail = text.rfind(FAIL_ANCHOR)
    last_reload = text.rfind(RELOAD_ANCHOR)

    if last_fail >= 0 and last_fail > last_reload:
        snippet = text[last_fail:]
        errors = re.findall(r"^(.*?\(\d+,\d+\):\s+error CS\d+:.*)$", snippet, re.MULTILINE)
        unique = list(dict.fromkeys(errors))[:20]
        detail = "\n".join(unique) if unique else "CS error found"
        return {"status": "error", "detail": detail}

    # Check for active compilation in last 4KB
    # Use absolute position of COMPILING_ANCHOR to compare against last reload
    last_compiling = text.rfind(COMPILING_ANCHOR)
    if last_compiling >= 0 and last_compiling > last_reload:
        return {"status": "compiling"}

    return {"status": "ok"}


def _is_pid_alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True  # process exists, not ours


def _discover_ports(ports_dir: str | Path) -> tuple[int | None, int | None]:
    """Return (main_port, reload_port) from live PID lockfiles."""
    d = Path(ports_dir)
    main_port = reload_port = None
    if not d.exists():
        return None, None
    for f in d.iterdir():
        name = f.name
        try:
            if name.endswith(".port") and not name.endswith(".reload-port"):
                pid = int(name[: -len(".port")])
                if _is_pid_alive(pid):
                    main_port = int(f.read_text(encoding="utf-8").splitlines()[0].strip())
            elif name.endswith(".reload-port"):
                pid = int(name[: -len(".reload-port")])
                if _is_pid_alive(pid):
                    reload_port = int(f.read_text(encoding="utf-8").splitlines()[0].strip())
        except (ValueError, OSError, IndexError):
            pass
    return main_port, reload_port


def tcp_probe(
    port: int,
    cmd: str = "diagnose",
    args: dict | None = None,
    timeout: float = _TCP_TIMEOUT,
    retries: int = 3,
) -> dict | None:
    """Probe Unity TCP port with retry (parsed dict / {} busy / None dead).
    Default cmd="diagnose", args=None sends the exact prior frame, untouched."""
    import time
    msg = json.dumps({"cmd": cmd, "args": args or {}, "id": "chk"}).encode()
    frame = struct.pack(">I", len(msg)) + msg
    for attempt in range(retries):
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=timeout) as s:
                s.sendall(frame)
                raw_len = _recvexactly(s, 4)
                if raw_len is None:
                    if attempt < retries - 1:
                        time.sleep(0.3)
                        continue
                    return {}  # all retries kicked — port alive but busy
                n = struct.unpack(">I", raw_len)[0]
                body = _recvexactly(s, n)
                if body is None:
                    if attempt < retries - 1:
                        time.sleep(0.3)
                        continue
                    return {}
                text = body.decode("utf-8", errors="replace")
                break
        except ConnectionRefusedError:
            return None  # truly dead
        except OSError:
            if attempt < retries - 1:
                time.sleep(0.3)
                continue
            return {}  # connected but error — port alive
    else:
        return {}

    result: dict = {}
    try:
        result = json.loads(text)
        data = result.get("data", "")
    except json.JSONDecodeError:
        data = text

    for line in data.splitlines():
        if "=" in line:
            k, _, v = line.partition("=")
            result[k.strip()] = v.strip()
    return result


def _recvexactly(s: socket.socket, n: int) -> bytes | None:
    buf = b""
    while len(buf) < n:
        chunk = s.recv(n - len(buf))
        if not chunk:
            return None
        buf += chunk
    return buf


def _probe_guard_locked(port: int, timeout: float = 2.0) -> bool | None:
    """Returns True=wedged, False=clear, None=unable to query."""
    code = 'UnityEditor.SessionState.GetBool("MCP_ReloadGuardLocked", false).ToString();'
    msg = json.dumps({"cmd": "execute_code", "args": {"code": code}, "id": "chk-g"}).encode()
    frame = struct.pack(">I", len(msg)) + msg
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout) as s:
            s.sendall(frame)
            raw_len = _recvexactly(s, 4)
            if raw_len is None:
                return None
            n = struct.unpack(">I", raw_len)[0]
            body = _recvexactly(s, n)
            if body is None:
                return None
            text = json.loads(body.decode("utf-8", errors="replace")).get("data", "")
            return text.strip().lower() == "true"
    except OSError:
        return None


def _parse_stale_dlls(probe: dict) -> list[str]:
    """Parse dlls= field from diagnose probe. Returns names of stale assemblies.

    Known gap: UnityMCP.Editor.Tests.dll is NOT tracked here.
    diagnose only reports UnityMCP.Editor (main_mvid). Tests assembly compiles
    separately — a test-only compile failure returns clean main_mvid but 0 tests run.
    Real proof of test assembly health = run_tests() returning a non-zero test count,
    NOT this MVID check. See: feedback_compile_verify_test_assembly.md
    """
    raw = probe.get("dlls", "")
    if not raw:
        return []
    stale = []
    for entry in raw.split(","):
        parts = entry.split(":")
        if len(parts) >= 3 and parts[-1] == "stale":
            stale.append(parts[0])
    return stale


# check_unity_probe.py owns the status/scenes CLI dispatch (300-line budget);
# it imports tcp_probe/_discover_ports/_PORTS_DIR back lazily (function-local).
sys.path.insert(0, str(Path(__file__).resolve().parent))
from check_unity_probe import (  # noqa: E402
    _DIAG_COMPILING_KEY,  # noqa: F401
    _parse_read_args,
    _run_read_subcommand,
    probe_open_scenes,  # noqa: F401
    probe_status,  # noqa: F401
)


def main(argv: list[str] | None = None) -> None:
    ns = _parse_read_args(argv if argv is not None else [])
    if ns.subcommand:
        raise SystemExit(_run_read_subcommand(ns.subcommand))
    try:
        log_text = _read_log(_editor_log_path())
        log_result = parse_log(log_text)

        if log_result["status"] == "error":
            errors = log_result["detail"].splitlines()
            print(f"COMPILE_ERROR  count={len(errors)}")
            for e in errors:
                print(e)
            print("ACTION: Read+Edit the files above. No MCP tools. No TCP. No investigation.")
            raise SystemExit(1)

        main_port, reload_port = _discover_ports(_PORTS_DIR)

        if main_port:
            probe = tcp_probe(main_port)
            if probe is not None:
                mvid = probe.get("main_mvid")
                if mvid:
                    guard = _probe_guard_locked(main_port)
                    if guard is True:
                        print(f"GUARD-WEDGED  mvid={mvid}  port={main_port}")
                        print("ACTION: ReloadGuard permanently locked. Run ladder with T5 (play/stop), or wait 120s for watchdog.")
                        raise SystemExit(3)
                    stale = _parse_stale_dlls(probe)
                    if stale:
                        print(f"STALE  assemblies={','.join(stale)}  port={main_port}")
                        raise SystemExit(2)
                    print(f"HEALTHY  mvid={mvid}  port={main_port}")
                else:
                    print(f"BUSY  port={main_port}  (another client connected)")
                    print("ACTION: Another session holds TCP. MCP tools will fail. Work locally or close other session.")
                raise SystemExit(0)

        if reload_port:
            probe = tcp_probe(reload_port)
            if probe is not None:
                mvid = probe.get("main_mvid", "unknown")
                print(f"RELOAD_PORT  mvid={mvid}  reload_port={reload_port}  main_dead")
                print("ACTION: Report to orchestrator and STOP. Do not investigate.")
                raise SystemExit(0)

        if log_result["status"] == "compiling":
            print("COMPILING  Editor.log shows active compilation")
            print("ACTION: Wait 10s, re-run this script.")
            raise SystemExit(0)

        print("UNREACHABLE  Editor.log clean, TCP dead")
        print("ACTION: Report to orchestrator and STOP. Do not investigate.")
        raise SystemExit(0)

    except SystemExit:
        raise
    except Exception as exc:
        print(f"SCRIPT_ERROR  {type(exc).__name__}: {exc}")
        raise SystemExit(5) from exc


if __name__ == "__main__":
    main(sys.argv[1:])
