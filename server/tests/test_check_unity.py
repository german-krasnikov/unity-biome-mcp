"""Tests for check_unity.py diagnostic script."""
import importlib.util
import os
import sys
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch

# Load script as module without package imports
_SCRIPT = Path(__file__).parent.parent.parent / "server" / "scripts" / "check_unity.py"
spec = importlib.util.spec_from_file_location("check_unity", _SCRIPT)
cu = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cu)

FIXTURES = Path(__file__).parent / "fixtures"


def _log(name: str) -> str:
    return (FIXTURES / name).read_text(encoding="utf-8")


# --- parse_log ---

def test_parse_log_wedge_returns_compile_error():
    log = _log("fm26_reload_wedge.log")
    result = cu.parse_log(log)
    assert result["status"] == "error"
    assert "CS0535" in result["detail"]


def test_parse_log_clean_returns_ok():
    log = _log("fm26_reload_clean.log")
    result = cu.parse_log(log)
    assert result["status"] == "ok"


def test_parse_log_clean_after_error_returns_ok():
    # reload marker after error = clean
    log = _log("fm26_reload_wedge.log") + "\nMono: successfully reloaded assembly\n"
    result = cu.parse_log(log)
    assert result["status"] == "ok"


def test_parse_log_compiling_only():
    log = "Compiling script assemblies\n"
    result = cu.parse_log(log)
    assert result["status"] == "compiling"


def test_parse_log_empty_returns_ok():
    result = cu.parse_log("")
    assert result["status"] == "ok"


# --- _is_pid_alive ---

def test_is_pid_alive_current_pid():
    assert cu._is_pid_alive(os.getpid()) is True


def test_is_pid_alive_dead_pid(monkeypatch):
    # os.kill(pid, 0) raises OSError: [WinError 87] on Windows (signal 0 is POSIX-only),
    # so we mock it to simulate the dead-PID signal consistently on all platforms.
    monkeypatch.setattr("os.kill", lambda pid, sig: (_ for _ in ()).throw(ProcessLookupError()))
    assert cu._is_pid_alive(99999) is False


# --- _discover_ports ---

def test_discover_ports_finds_alive_port():
    pid = os.getpid()
    with tempfile.TemporaryDirectory() as d:
        Path(d, f"{pid}.port").write_text("9500", encoding="utf-8")
        main, reload = cu._discover_ports(d)
    assert main == 9500
    assert reload is None


def test_discover_ports_finds_reload_port():
    pid = os.getpid()
    with tempfile.TemporaryDirectory() as d:
        Path(d, f"{pid}.reload-port").write_text("9600", encoding="utf-8")
        main, reload = cu._discover_ports(d)
    assert main is None
    assert reload == 9600


def test_discover_ports_ignores_dead_pid():
    with tempfile.TemporaryDirectory() as d:
        Path(d, "99999.port").write_text("9500", encoding="utf-8")
        main, reload = cu._discover_ports(d)
    assert main is None
    assert reload is None


def test_discover_ports_empty_dir():
    with tempfile.TemporaryDirectory() as d:
        main, reload = cu._discover_ports(d)
    assert main is None
    assert reload is None


# --- tcp_probe ---

def test_tcp_probe_returns_none_on_connection_refused(monkeypatch):
    # Port 1 refuses on Linux/macOS but may timeout on Windows (firewall filtered).
    # Mock create_connection to raise ConnectionRefusedError consistently on all platforms.
    import socket
    monkeypatch.setattr(socket, "create_connection",
                        lambda *a, **kw: (_ for _ in ()).throw(ConnectionRefusedError()))
    result = cu.tcp_probe(1)
    assert result is None


def test_tcp_probe_returns_empty_dict_on_disconnect():
    """Server accepts then immediately closes — simulates single-client kick (all retries)."""
    import socket
    import threading

    def _serve(srv):
        try:
            for _ in range(3):  # handle all retry attempts
                conn, _ = srv.accept()
                conn.recv(128)
                conn.close()
        except Exception:
            pass
        finally:
            srv.close()

    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", 0))
    port = srv.getsockname()[1]
    srv.listen(3)
    t = threading.Thread(target=_serve, args=(srv,), daemon=True)
    t.start()

    result = cu.tcp_probe(port, retries=2)  # fewer retries for speed
    t.join(timeout=3)
    assert result == {}  # alive but busy, not None


def test_tcp_probe_parses_mvid(tmp_path):
    """Mock a TCP server that returns diagnose-style response."""
    import socket
    import struct
    import threading

    response_data = "main_mvid=abc123\nstatus=ok\n"
    payload = response_data.encode()
    frame = struct.pack(">I", len(payload)) + payload

    def _serve(srv):
        try:
            conn, _ = srv.accept()
            # read 4-byte length + body (ignore)
            hdr = conn.recv(4)
            if len(hdr) == 4:
                n = struct.unpack(">I", hdr)[0]
                conn.recv(n)
            conn.sendall(frame)
            conn.close()
        except Exception:
            pass
        finally:
            srv.close()

    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", 0))
    port = srv.getsockname()[1]
    srv.listen(1)
    t = threading.Thread(target=_serve, args=(srv,), daemon=True)
    t.start()

    result = cu.tcp_probe(port)
    t.join(timeout=3)
    assert result is not None
    assert result.get("main_mvid") == "abc123"


# --- verdict routing (integration) ---

def _make_verdict(parse_status, main_port, reload_port, compiling_in_log=False):
    """Drive check_unity.main() with mocked internals, capture stdout + exit code."""
    import io
    from contextlib import redirect_stdout

    buf = io.StringIO()
    exit_code = None

    def mock_parse(text):
        if parse_status == "error":
            return {"status": "error", "detail": "/foo.cs(1,1): error CS0001: bad"}
        if parse_status == "compiling":
            return {"status": "compiling"}
        return {"status": "ok"}

    def mock_probe(port, timeout=2):
        if port == main_port:
            return {"main_mvid": "deadbeef"}
        if port == reload_port:
            return {"main_mvid": "cafe"}
        return None

    def mock_discover(d):
        return (main_port, reload_port)

    def mock_read_log(path):
        return "Compiling script assemblies\n" if compiling_in_log else ""

    def mock_exit(code):
        nonlocal exit_code
        exit_code = code
        raise SystemExit(code)

    with patch.object(cu, "parse_log", mock_parse), \
         patch.object(cu, "tcp_probe", mock_probe), \
         patch.object(cu, "_discover_ports", mock_discover), \
         patch.object(cu, "_read_log", mock_read_log), \
         redirect_stdout(buf):
        try:
            cu.main()
        except SystemExit as e:
            exit_code = e.code

    return buf.getvalue().strip(), exit_code


def test_verdict_compile_error_exits_1():
    out, code = _make_verdict("error", None, None)
    assert code == 1
    assert out.startswith("COMPILE_ERROR  count=1")
    assert "error CS0001" in out


def test_verdict_compile_error_multiple():
    """Multiple errors: count on first line, details below."""
    import io
    from contextlib import redirect_stdout

    def mock_parse(text):
        return {"status": "error", "detail": "a.cs(1,1): error CS0001: x\nb.cs(2,2): error CS0002: y"}

    buf = io.StringIO()
    with patch.object(cu, "parse_log", mock_parse), \
         patch.object(cu, "_discover_ports", lambda d: (None, None)), \
         patch.object(cu, "_read_log", lambda p: ""), \
         redirect_stdout(buf):
        try:
            cu.main()
        except SystemExit:
            pass
    lines = buf.getvalue().strip().splitlines()
    assert lines[0] == "COMPILE_ERROR  count=2"
    assert "error CS0001" in lines[1]
    assert "ACTION:" in lines[-1]


def test_verdict_script_error_exits_5():
    """Unhandled exception → SCRIPT_ERROR, exit 5."""
    import io
    from contextlib import redirect_stdout

    def mock_read_log(path):
        raise PermissionError("denied")

    buf = io.StringIO()
    with patch.object(cu, "_read_log", mock_read_log), \
         redirect_stdout(buf):
        try:
            cu.main()
        except SystemExit as e:
            code = e.code
    assert code == 5
    assert "SCRIPT_ERROR" in buf.getvalue()


def test_verdict_healthy_exits_0():
    out, code = _make_verdict("ok", 9500, None)
    assert code == 0
    assert "HEALTHY" in out
    assert "mvid=" in out


def test_verdict_busy_when_probe_returns_empty_dict():
    """Port alive but no mvid (another client holds connection) → BUSY."""
    import io
    from contextlib import redirect_stdout

    buf = io.StringIO()

    with patch.object(cu, "parse_log", lambda t: {"status": "ok"}), \
         patch.object(cu, "tcp_probe", lambda p, timeout=2: {}), \
         patch.object(cu, "_discover_ports", lambda d: (9500, None)), \
         patch.object(cu, "_read_log", lambda p: ""), \
         redirect_stdout(buf):
        try:
            cu.main()
        except SystemExit as e:
            code = e.code
    assert code == 0
    assert "BUSY" in buf.getvalue()
    assert "ACTION:" in buf.getvalue()


def test_verdict_reload_only_exits_0():
    out, code = _make_verdict("ok", None, 9600)
    assert code == 0
    assert "RELOAD_PORT" in out


def test_verdict_compiling_exits_0():
    out, code = _make_verdict("compiling", None, None)
    assert code == 0
    assert "COMPILING" in out


def test_verdict_unreachable_exits_0():
    out, code = _make_verdict("ok", None, None)
    assert code == 0
    assert "UNREACHABLE" in out


# --- _parse_stale_dlls ---

def test_parse_stale_dlls_all_fresh():
    probe = {"dlls": "UnityMCP.Editor:638000:fresh,UnityMCP.Editor.Chat:638001:fresh"}
    assert cu._parse_stale_dlls(probe) == []


def test_parse_stale_dlls_one_stale():
    probe = {"dlls": "UnityMCP.Editor:638000:fresh,UnityMCP.Editor.Chat.View:638001:stale"}
    result = cu._parse_stale_dlls(probe)
    assert result == ["UnityMCP.Editor.Chat.View"]


def test_parse_stale_dlls_multiple_stale():
    probe = {"dlls": "A:638000:stale,B:638001:fresh,C:638002:stale"}
    result = cu._parse_stale_dlls(probe)
    assert result == ["A", "C"]


def test_parse_stale_dlls_empty_field():
    probe = {"dlls": ""}
    assert cu._parse_stale_dlls(probe) == []


def test_parse_stale_dlls_missing_field():
    probe = {}
    assert cu._parse_stale_dlls(probe) == []


def test_parse_stale_dlls_unknown_no_src_ignored():
    """unknown(no-src) = Unity built-in assemblies, not stale."""
    probe = {"dlls": "UnityEngine:638000:unknown(no-src),UnityEditor:638001:unknown(missing)"}
    assert cu._parse_stale_dlls(probe) == []


def test_parse_stale_dlls_mixed_unknown_and_stale():
    probe = {"dlls": "UnityEngine:638000:unknown(no-src),MyPlugin:638001:stale"}
    result = cu._parse_stale_dlls(probe)
    assert result == ["MyPlugin"]


# --- verdict routing with stale assemblies ---

def _make_verdict_with_dlls(dlls_value: str | None):
    """Like _make_verdict but probe returns dlls= field."""
    import io
    from contextlib import redirect_stdout

    probe_result: dict = {"main_mvid": "deadbeef"}
    if dlls_value is not None:
        probe_result["dlls"] = dlls_value

    buf = io.StringIO()
    exit_code = None

    with patch.object(cu, "parse_log", lambda t: {"status": "ok"}), \
         patch.object(cu, "tcp_probe", lambda p, timeout=2: probe_result), \
         patch.object(cu, "_discover_ports", lambda d: (9500, None)), \
         patch.object(cu, "_read_log", lambda p: ""), \
         redirect_stdout(buf):
        try:
            cu.main()
        except SystemExit as e:
            exit_code = e.code

    return buf.getvalue().strip(), exit_code


def test_verdict_stale_exits_2():
    out, code = _make_verdict_with_dlls("A:638000:stale")
    assert code == 2
    assert out.startswith("STALE")
    assert "assemblies=A" in out


def test_verdict_stale_multiple_names():
    out, code = _make_verdict_with_dlls("A:638000:stale,B:638001:fresh,C:638002:stale")
    assert code == 2
    assert "assemblies=A,C" in out


def test_verdict_stale_includes_port():
    out, code = _make_verdict_with_dlls("X:638000:stale")
    assert "port=9500" in out


def test_verdict_fresh_dlls_healthy():
    out, code = _make_verdict_with_dlls("A:638000:fresh,B:638001:fresh")
    assert code == 0
    assert "HEALTHY" in out


def test_verdict_no_dlls_field_healthy():
    out, code = _make_verdict_with_dlls(None)
    assert code == 0
    assert "HEALTHY" in out
