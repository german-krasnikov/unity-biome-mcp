"""Tests for check_unity.py diagnostic script."""
import importlib.util
import json
import os
import struct
import sys
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

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


# --- A11a: direct-TCP read surface (status/scenes) ---
# Mocked at the socket.create_connection layer -- no live Unity worker
# required. See Plans/Reviews/ARCH-STF-unity-access-policy.md §probe_script_spec.


class _FakeUnitySocket:
    """Records every frame sent via sendall(); replies with one canned
    length-prefixed payload (the same wire shape check_unity.py's own
    tcp_probe() sends/reads)."""

    def __init__(self, response_text: str = "") -> None:
        self.sent_frames: list[bytes] = []
        payload = response_text.encode("utf-8")
        self._wire = bytearray(struct.pack(">I", len(payload)) + payload)

    def __enter__(self):
        return self

    def __exit__(self, *_args) -> None:
        return None

    def settimeout(self, _timeout: float) -> None:
        return None

    def sendall(self, payload: bytes) -> None:
        self.sent_frames.append(payload)

    def recv(self, count: int) -> bytes:
        chunk = bytes(self._wire[:count])
        del self._wire[:count]
        return chunk


def _decode_sent_frame(frame: bytes) -> dict:
    n = struct.unpack(">I", frame[:4])[0]
    return json.loads(frame[4 : 4 + n])


def test_tcp_probe_default_cmd_unchanged(monkeypatch):
    """Generalizing tcp_probe() to accept cmd/args must not change its
    default wire frame -- every existing call site and test keeps working
    untouched."""
    fake = _FakeUnitySocket("main_mvid=abc\n")
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: fake)

    cu.tcp_probe(9500)

    assert len(fake.sent_frames) == 1
    assert _decode_sent_frame(fake.sent_frames[0]) == {
        "cmd": "diagnose",
        "args": {},
        "id": "chk",
    }


def test_tcp_probe_explicit_cmd_and_args(monkeypatch):
    fake_status = _FakeUnitySocket("")
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: fake_status)
    cu.tcp_probe(9500, cmd="get_status")
    assert _decode_sent_frame(fake_status.sent_frames[0]) == {
        "cmd": "get_status",
        "args": {},
        "id": "chk",
    }

    fake_scene = _FakeUnitySocket("")
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: fake_scene)
    cu.tcp_probe(9500, cmd="scene", args={"action": "list"})
    assert _decode_sent_frame(fake_scene.sent_frames[0]) == {
        "cmd": "scene",
        "args": {"action": "list"},
        "id": "chk",
    }


def test_probe_status_parses_fields(monkeypatch):
    """get_status wire format (CommandRouter.Registration.cs:82-97) folds
    through tcp_probe's existing key=value parser -- no new parsing logic."""
    text = (
        "scene=SampleScene\n"
        "dirty=False\n"
        "playing=False\n"
        "compiling=False\n"
        "port=9500\n"
    )
    fake = _FakeUnitySocket(text)
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: fake)

    result = cu.probe_status(9500)

    assert result == {
        "scene": "SampleScene",
        "dirty": "False",
        "playing": "False",
        "compiling": "False",
        "port": "9500",
    }


def test_probe_status_excludes_raw_envelope_keys(monkeypatch):
    """Real Unity responses are JSON-enveloped (JsonHelper.FormatResponse),
    not the plain text used above -- tcp_probe's existing folding logic
    keeps 'id'/'ok'/'data' alongside the folded fields in that case (by
    design, unchanged here). probe_status() must strip those envelope-only
    keys so a CLI caller printing every dict entry doesn't dump a redundant
    raw blob next to the real fields (found via the live acceptance smoke).

    Double-red: red today (envelope keys leak into the returned dict), red
    if the filter is widened/narrowed to drop or keep the wrong keys."""
    envelope = json.dumps(
        {"id": "chk", "ok": True, "data": "scene=SampleScene\ndirty=False\n"}
    )
    fake = _FakeUnitySocket(envelope)
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: fake)

    result = cu.probe_status(9500)

    assert result == {"scene": "SampleScene", "dirty": "False"}


def test_probe_open_scenes_parses_leftover(monkeypatch):
    """Canned two-line SceneHelper.ListScenes()-shaped payload (SceneHelper.cs:64-78):
    one active ('* '-prefixed) scene, one additive/leftover scene.

    The scene response is JSON-enveloped ({"data": "..."}) -- the real wire
    shape (JsonHelper.FormatResponse) -- since ListScenes()'s lines carry no
    '=' characters and would otherwise fold to nothing (only a JSON envelope
    exposes the raw text via result['data'], per
    Plans/Reviews/ARCH-STF-unity-access-policy.md's probe_script_spec)."""
    diag_socket = _FakeUnitySocket("iscompiling=False\n")
    scene_socket = _FakeUnitySocket(
        json.dumps(
            {
                "id": "chk",
                "ok": True,
                "data": (
                    "* SampleScene  Assets/Scenes/SampleScene.unity  9 objs\n"
                    "  GridTest  Assets/TestsTemp/foo/GridTest.unity  22 objs [dirty]"
                ),
            }
        )
    )
    sockets = iter([diag_socket, scene_socket])
    monkeypatch.setattr(cu.socket, "create_connection", lambda *a, **kw: next(sockets))

    result = cu.probe_open_scenes(9500)

    assert result == [
        "* SampleScene  Assets/Scenes/SampleScene.unity  9 objs",
        "  GridTest  Assets/TestsTemp/foo/GridTest.unity  22 objs [dirty]",
    ]
    active = [line for line in result if line.startswith("* ")]
    leftover = [line for line in result if not line.startswith("* ")]
    assert active == ["* SampleScene  Assets/Scenes/SampleScene.unity  9 objs"]
    assert leftover == ["  GridTest  Assets/TestsTemp/foo/GridTest.unity  22 objs [dirty]"]


def test_probe_open_scenes_skips_during_compile(monkeypatch):
    """'scene' defaults allowedDuringCompile=false (CommandRouter.Registration.cs:562-563)
    -- probe_open_scenes must never send it once a fresh diagnose shows
    Unity mid-compile.

    Double-red: red today (AttributeError -- probe_open_scenes doesn't
    exist); red again if the compile gate is removed and a second
    (scene) connection is opened."""
    connections: list[_FakeUnitySocket] = []

    def _fake_create_connection(*_args, **_kwargs):
        fake = _FakeUnitySocket(f"{cu._DIAG_COMPILING_KEY}=True\n")
        connections.append(fake)
        return fake

    monkeypatch.setattr(cu.socket, "create_connection", _fake_create_connection)

    result = cu.probe_open_scenes(9500)

    assert result is None
    assert len(connections) == 1  # only the diagnose gate; scene never sent


def test_no_forbidden_wire_commands():
    """Static regression guard: the closed direct-TCP allowlist must never
    quietly widen to include the five banned wire commands, and the CLI
    must never grow a generic --cmd/--args passthrough. Scans both
    check_unity.py and its sibling check_unity_probe.py (the CLI dispatch
    lives there, split out for the 300-line budget).

    Double-red: red today (cu._parse_read_args doesn't exist yet --
    AttributeError); red again if a future edit adds a forbidden literal,
    a passthrough flag, or drops the closed argparse `choices=`."""
    probe_script = _SCRIPT.parent / "check_unity_probe.py"
    source = _SCRIPT.read_text(encoding="utf-8") + probe_script.read_text(encoding="utf-8")
    for literal in ("force_refresh", "recompile", "force_play_stop", "warm_type_cache"):
        assert literal not in source, f"forbidden wire command literal found: {literal}"
    assert '"cmd": "sync"' not in source
    assert "'cmd': 'sync'" not in source
    assert "--cmd" not in source
    assert "--args" not in source

    with pytest.raises(SystemExit):
        cu._parse_read_args(["not-a-real-subcommand"])
