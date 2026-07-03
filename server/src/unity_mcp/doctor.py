"""Health diagnostics tool — 5 checks, optional auto-fix."""
import asyncio
import json
import struct
import sys
from pathlib import Path

from .lockfile import is_pid_alive
from .paths import ports_dir as _ports_dir_canonical, unity_mcp_dir
from .doctor_report import CheckResult, USER_MESSAGES, format_report  # re-exported
from .constants import DEFAULT_PORT

__all__ = ["CheckResult", "USER_MESSAGES", "format_report",
           "check_python_version", "check_port_file", "check_lockfile",
           "check_tcp_connection", "check_unity_state", "run_doctor"]


# Seams for testing (overridable via patch)
def _ports_dir() -> Path:
    return _ports_dir_canonical()


def _lock_dir() -> Path:
    return unity_mcp_dir()


async def check_python_version() -> CheckResult:
    major, minor = sys.version_info[:2]
    ok = (major, minor) >= (3, 10)
    detail = f"{major}.{minor}.{sys.version_info[2]}"
    return CheckResult(
        name="python_version",
        ok=ok,
        detail=detail if ok else f"{detail} (need ≥3.10)",
        fix_cmd="" if ok else "Install Python 3.10+",
    )


async def check_port_file(fix: bool = False) -> CheckResult:
    """Check ~/.unity-mcp/ports/*.port files; auto-fix stale PIDs."""
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return CheckResult("port_file", False, "No ports dir — Unity MCP plugin never ran")

    port_files = list(ports_dir.glob("*.port"))
    if not port_files:
        return CheckResult("port_file", False, "No .port files — Unity not running", auto_fixable=False)

    stale, live = [], []
    for f in port_files:
        try:
            pid = int(f.stem)
        except ValueError:
            stale.append(f)
            continue
        (live if is_pid_alive(pid) else stale).append(f)

    if fix and stale:
        for f in stale:
            try:
                f.unlink()
            except OSError:
                pass
        stale = []
        if not live:
            return CheckResult(
                "port_file", False,
                "Stale files cleaned but no Unity instance running",
            )

    if stale and not live:
        return CheckResult(
            "port_file", False,
            f"{len(stale)} stale .port file(s) with dead PIDs",
            fix_cmd="Run doctor(fix=True) to clean up",
            auto_fixable=True,
        )
    if live:
        return CheckResult("port_file", True, f"{len(live)} live .port file(s)")
    return CheckResult("port_file", True, "Stale files cleaned")


async def check_lockfile(fix: bool = False) -> CheckResult:
    """Check for zombie lockfiles with dead PIDs."""
    lock_dir = _lock_dir()
    if not lock_dir.exists():
        return CheckResult("lockfile", True, "No lock dir")

    stale = []
    for f in lock_dir.glob("server-*.lock"):
        try:
            pid = int(f.stem.rsplit("-", 1)[1])
        except (ValueError, IndexError):
            continue
        if not is_pid_alive(pid):
            stale.append(f)

    if not stale:
        return CheckResult("lockfile", True, "No stale lockfiles")

    if fix:
        for f in stale:
            try:
                f.unlink()
            except OSError:
                pass
        return CheckResult("lockfile", True, f"Cleaned {len(stale)} stale lockfile(s)")

    return CheckResult(
        "lockfile", False,
        f"{len(stale)} stale lockfile(s) with dead PIDs",
        fix_cmd="Run doctor(fix=True) to clean up",
        auto_fixable=True,
    )


def _resolve_port(port: int) -> int:
    if port:
        return port
    from .server_filtering import read_unity_port
    try:
        # B3: read_unity_port may return None when skip_probe=True and no live Unity.
        discovered = read_unity_port(skip_probe=True)
        return discovered if discovered is not None else DEFAULT_PORT
    except Exception:
        return DEFAULT_PORT


async def check_tcp_connection(port: int = 0) -> CheckResult:
    """TCP probe with 3s timeout."""
    port = _resolve_port(port)
    try:
        _, writer = await asyncio.wait_for(
            asyncio.open_connection("127.0.0.1", port), timeout=3.0
        )
        writer.close()
        try:
            await writer.wait_closed()
        except Exception:
            pass
        return CheckResult("tcp_connection", True, f"Connected to :{port}")
    except (ConnectionRefusedError, OSError):
        return CheckResult(
            "tcp_connection", False,
            f"Cannot connect to :{port} — Unity MCP plugin not running",
            fix_cmd=USER_MESSAGES["disconnected"],
        )
    except asyncio.TimeoutError:
        return CheckResult(
            "tcp_connection", False,
            f"Timeout connecting to :{port}",
            fix_cmd=USER_MESSAGES["frozen"],
        )


async def check_unity_state(port: int = 0) -> CheckResult:
    """Send 'diagnose' via TCP and parse response."""
    port = _resolve_port(port)
    try:
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection("127.0.0.1", port), timeout=3.0
        )
    except Exception:
        return CheckResult("unity_state", False, USER_MESSAGES["disconnected"])

    try:
        msg = json.dumps({"cmd": "diagnose", "args": {}}).encode()
        writer.write(struct.pack(">I", len(msg)) + msg)
        await writer.drain()
        length_bytes = await asyncio.wait_for(reader.readexactly(4), timeout=3.0)
        length = struct.unpack(">I", length_bytes)[0]
        data = await asyncio.wait_for(reader.readexactly(length), timeout=3.0)
        resp = json.loads(data)
    except Exception as e:
        return CheckResult("unity_state", False, f"Diagnose failed: {e}")
    finally:
        writer.close()
        try:
            await writer.wait_closed()
        except Exception:
            pass

    if not resp.get("ok"):
        return CheckResult("unity_state", False, resp.get("err", "unknown error"))

    info = resp.get("data", "")
    if "compiling=true" in info:
        return CheckResult("unity_state", False, USER_MESSAGES["compiling"])
    if "dlls=stale" in info:
        return CheckResult("unity_state", False, USER_MESSAGES["dlls_stale"],
                           fix_cmd="Assets → Reimport All", auto_fixable=False)
    return CheckResult("unity_state", True, info or "healthy")


async def run_doctor(fix: bool = False) -> list[CheckResult]:
    """Run all 5 checks concurrently."""
    return list(await asyncio.gather(
        check_python_version(),
        check_port_file(fix=fix),
        check_lockfile(fix=fix),
        check_tcp_connection(),
        check_unity_state(),
    ))
