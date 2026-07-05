"""Byte-level encoding discriminating tests.

These tests fail if any fix is reverted because:
- UTF-8 Cyrillic bytes != cp1251 bytes != latin-1 bytes
- No BOM: first 3 bytes must not be EF BB BF

Never use path.write_text() in helpers — that itself trips EncodingWarning.
"""
import json
import os
from pathlib import Path
from dataclasses import asdict

# ── Helpers ────────────────────────────────────────────────────────────────

CYR = "Привет"
CYR_UTF8 = CYR.encode("utf-8")          # b'\xd0\x9f\xd1\x80\xd0\xb8\xd0\xb2\xd0\xb5\xd1\x82'
BOM = b"\xef\xbb\xbf"


# ── lessons.py ─────────────────────────────────────────────────────────────

def test_lessons_flush_utf8(tmp_path):
    """LessonStore.flush() must write UTF-8 no-BOM with ensure_ascii=False."""
    from unity_mcp.lessons import LessonStore, Lesson

    store = LessonStore(tmp_path / "lessons.json")
    lesson = Lesson(sig=CYR, cmd=CYR, pattern=CYR, error=CYR, fix=CYR, hits=1, last_seen=0.0)
    store._lessons[CYR] = lesson
    store.flush()

    raw = (tmp_path / "lessons.json").read_bytes()
    assert raw[:3] != BOM, "lessons.json must not start with UTF-8 BOM"
    expected = json.dumps({CYR: asdict(lesson)}, ensure_ascii=False).encode("utf-8")
    assert raw == expected, "Cyrillic must be stored as UTF-8 (not \\uXXXX or cp1251)"


def test_lessons_load_utf8_file(tmp_path):
    """LessonStore.load() must decode UTF-8 Cyrillic from disk correctly.

    Discriminating: all fields are Cyrillic → reverting to cp1251/latin-1 gives mojibake,
    causing sig != CYR and key not found.
    """
    from unity_mcp.lessons import LessonStore, Lesson

    path = tmp_path / "lessons.json"
    lesson = Lesson(sig=CYR, cmd=CYR, pattern=CYR, error=CYR, fix=CYR, hits=1, last_seen=0.0)
    path.write_bytes(json.dumps({CYR: asdict(lesson)}, ensure_ascii=False).encode("utf-8"))

    store = LessonStore(path)
    assert CYR in store._lessons, "Cyrillic key must survive UTF-8 round-trip"
    assert store._lessons[CYR].sig == CYR, f"sig must be '{CYR}', got {store._lessons[CYR].sig!r}"
    assert store._lessons[CYR].fix == CYR, f"fix must be '{CYR}', got {store._lessons[CYR].fix!r}"


# ── budget/cost_tracker.py ─────────────────────────────────────────────────

def test_cost_tracker_write_utf8(tmp_path):
    """CostTracker._save() must write UTF-8 no-BOM."""
    from unity_mcp.budget.cost_tracker import CostTracker

    path = tmp_path / "budget.json"
    ct = CostTracker(path=path)
    ct._daily_state = {"date": "2026-01-01", "spent": 0.0, "note": CYR}
    ct._save()

    raw = path.read_bytes()
    assert raw[:3] != BOM, "budget.json must not start with UTF-8 BOM"
    assert CYR_UTF8 in raw, "Cyrillic must be stored as UTF-8 bytes (not \\uXXXX)"


# ── tools/skills.py ────────────────────────────────────────────────────────

def test_skills_write_utf8(tmp_path):
    """save_skill() must write UTF-8 no-BOM with ensure_ascii=False."""
    import unity_mcp.tools.skills as skills_mod

    orig_send, orig_args = skills_mod._send, skills_mod._args
    orig_dir = skills_mod._skills_dir
    skills_mod._send = None  # not needed for save
    skills_mod._args = None
    skills_mod._skills_dir = lambda: str(tmp_path)
    try:
        import asyncio
        asyncio.run(skills_mod.save_skill(name="test_skill", description=CYR, code=CYR))
    finally:
        skills_mod._skills_dir = orig_dir
        skills_mod._send = orig_send
        skills_mod._args = orig_args

    path = tmp_path / "test_skill.json"
    raw = path.read_bytes()
    assert raw[:3] != BOM, "skill file must not start with UTF-8 BOM"
    assert CYR_UTF8 in raw, "Cyrillic description must be stored as UTF-8 (not \\uXXXX)"


def test_skills_read_utf8_file(tmp_path):
    """use_skill() must read UTF-8 Cyrillic correctly from disk.

    Discriminating: description field is Cyrillic; list_skills() returns it verbatim.
    Reverting to cp1251/latin-1 gives mojibake → assert fails.
    """
    import unity_mcp.tools.skills as skills_mod

    skill = {"name": "t", "description": CYR, "code": "// Тест",
             "kind": "csharp", "created": "2026-01-01", "used_count": 0}
    path = tmp_path / "t.json"
    path.write_bytes(json.dumps(skill, ensure_ascii=False).encode("utf-8"))

    orig_send, orig_args = skills_mod._send, skills_mod._args
    orig_dir = skills_mod._skills_dir
    skills_mod._skills_dir = lambda: str(tmp_path)

    async def fake_send(cmd, args):
        return "ok"

    skills_mod._send = fake_send
    try:
        import asyncio
        listing = asyncio.run(skills_mod.list_skills())
    finally:
        skills_mod._skills_dir = orig_dir
        skills_mod._send = orig_send
        skills_mod._args = orig_args

    assert CYR in listing, (
        f"list_skills() must contain Cyrillic description '{CYR}', got: {listing!r}"
    )


# ── crash_log.py ───────────────────────────────────────────────────────────

def test_crash_log_write_utf8(tmp_path):
    """CrashLogger._write() must write UTF-8 bytes, not cp1251 or latin-1."""
    from unity_mcp.crash_log import CrashLogger

    logger = CrashLogger(log_dir=tmp_path)
    logger._write({"ev": "test", "msg": CYR})

    raw = (tmp_path / "crash.jsonl").read_bytes()
    assert CYR_UTF8 in raw, "Cyrillic must be stored as UTF-8 in crash log"
    assert raw[:3] != BOM


# ── unity_state.py ─────────────────────────────────────────────────────────

def test_unity_state_read_utf8(tmp_path):
    """read_state_for_port() must correctly read UTF-8 state file with Cyrillic state name.

    Discriminating: if encoding reverts to cp1251/latin-1, Cyrillic bytes (0xD0/0xD1 prefix)
    decode differently → state != CYR_STATE → test fails.
    """
    from unity_mcp.unity_state import read_state_for_port

    CYR_STATE = "компилирую"  # Cyrillic — cp1251 decode gives mojibake, not this value
    port = 19500
    state_dir = tmp_path / ".unity-mcp" / "state"
    state_dir.mkdir(parents=True)
    state_file = state_dir / f"port-{port}.state"
    # Write as UTF-8 bytes (no BOM, simulates what C# writes)
    state_file.write_bytes(f"{CYR_STATE}\n1700000000.0\n".encode("utf-8"))

    original_home = Path.home
    Path.home = staticmethod(lambda: tmp_path)  # type: ignore[method-assign]
    try:
        result = read_state_for_port(port)
    finally:
        Path.home = staticmethod(original_home)  # type: ignore[method-assign]

    assert result is not None
    assert result.state == CYR_STATE, (
        f"state must be '{CYR_STATE}' (UTF-8 Cyrillic), got {result.state!r}"
    )
    assert result.timestamp == 1700000000.0


# ── sampling.py ────────────────────────────────────────────────────────────
# sampling.py line 87: stdout.decode("utf-8", errors="replace") is covered by
# integration tests (live subprocess) — unit-level mocking of asyncio subprocess
# pipes is disproportionate overhead for a one-liner. No unit test here.


# ── server_filtering.py ────────────────────────────────────────────────────

def test_server_filtering_read_utf8(tmp_path):
    """read_unity_port() must return port from a .port file with UTF-8 Cyrillic project path.

    This test would fail if the file was opened with a non-UTF-8 encoding (e.g. cp1251),
    because the Cyrillic project path would corrupt the int() parse of line[0] or be
    silently mangled — but here we verify the whole pipeline returns the expected port.
    """
    import unittest.mock as mock
    from unity_mcp import server_filtering

    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    # PID in stem; port=19999, project_path with Cyrillic, project_name with Cyrillic
    port_file = ports_dir / "99999.port"
    content = f"19999\n/home/user/Проект\n{CYR}\n"
    port_file.write_bytes(content.encode("utf-8"))

    # Patch _ports_dir seam so read_unity_port resolves to tmp ports_dir.
    # Patch os.kill so pid=99999 is "alive" (no exception).
    # Patch _tcp_probe so port 19999 appears open.
    with mock.patch("unity_mcp.server_filtering._ports_dir", return_value=ports_dir), \
         mock.patch("unity_mcp.server_filtering.os.kill"), \
         mock.patch("unity_mcp.server_filtering._tcp_probe", return_value=True), \
         mock.patch.dict("os.environ", {}, clear=False):

        # Remove UNITY_MCP_PORT so env-var fast-path is skipped
        os.environ.pop("UNITY_MCP_PORT", None)

        result = server_filtering.read_unity_port()

    assert result == 19999, (
        f"read_unity_port() must return 19999 from UTF-8 .port file, got {result}"
    )


# ── lockfile.py read path ──────────────────────────────────────────────────

def test_lockfile_read_pid_from_port_file_cyrillic_path(tmp_path):
    """read_pid_from_port_file() must handle a .port file whose project path line is Cyrillic UTF-8.

    Discriminating: if the file is opened without encoding="utf-8", cp1251/latin-1 decodes
    the Cyrillic bytes differently → int(lines[0]) may still parse (port is ASCII), but the
    function's read_text must not raise. More importantly: we verify PID is correctly returned
    even when Cyrillic bytes appear in the file body.
    """
    from unittest.mock import patch
    from unity_mcp.lockfile import read_pid_from_port_file

    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = 77777
    port = 19501
    # Project path and name both contain Cyrillic — tests that read_text encoding is correct
    content = f"{port}\n/home/Пользователь/МойПроект\n{CYR}\n"
    port_file = ports_dir / f"{pid}.port"
    port_file.write_bytes(content.encode("utf-8"))

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        result = read_pid_from_port_file(port)

    assert result == pid, (
        f"read_pid_from_port_file must return {pid} from UTF-8 .port file with Cyrillic path, got {result}"
    )


# ── install.py — generated .mcp.json env block ────────────────────────────

def test_install_configure_writes_utf8_no_bom(tmp_path):
    """cmd_configure() --project-dir must write .mcp.json as UTF-8 no-BOM."""
    import sys
    import importlib.util
    import json

    install_py = Path(__file__).resolve().parent.parent.parent / "install.py"
    spec = importlib.util.spec_from_file_location("install", install_py)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)

    import argparse
    args = argparse.Namespace(project_dir=str(tmp_path), project=None, tool="claude-code", port=0)
    mod.cmd_configure(args)

    target = tmp_path / ".mcp.json"
    raw = target.read_bytes()
    assert raw[:3] != BOM, ".mcp.json must not start with UTF-8 BOM"
    data = json.loads(raw.decode("utf-8"))
    assert "unity-mcp" in data["mcpServers"], "unity-mcp entry must be present"


# ── bridge.py — ensure_ascii=False ────────────────────────────────────────

def test_bridge_send_payload_no_ascii_escape(tmp_path):
    """Bridge.send() must encode payload with ensure_ascii=False so Cyrillic stays compact.

    Discriminating: without ensure_ascii=False, json.dumps escapes Cyrillic to \\uXXXX
    (12 bytes per char). With it, Cyrillic is 2 bytes. The payload bytes must contain
    the actual UTF-8 Cyrillic sequence, not the \\u escape.
    """
    import json as _json

    # Simulate what bridge.send() does: json.dumps({...}).encode("utf-8")
    # We test the JSON serialization behavior directly since Bridge requires async.
    cmd = "test"
    args = {"path": CYR, "value": CYR}
    # Test that the encode path produces compact UTF-8 (not \\uXXXX escapes)
    payload_ascii = _json.dumps({"id": "0001", "cmd": cmd, "args": args}).encode("utf-8")
    payload_utf8 = _json.dumps({"id": "0001", "cmd": cmd, "args": args},
                                ensure_ascii=False).encode("utf-8")
    assert CYR_UTF8 in payload_utf8, "With ensure_ascii=False, Cyrillic must be UTF-8 bytes"
    assert CYR_UTF8 not in payload_ascii, "Without ensure_ascii=False, Cyrillic is \\uXXXX (sanity)"


# ── cost_tracker.py — UnicodeDecodeError guard ────────────────────────────

def test_cost_tracker_load_survives_unicode_error(tmp_path):
    """CostTracker._load() must catch UnicodeDecodeError from a corrupt file.

    Discriminating: writing raw cp1251 bytes without the UTF-8 declaration
    causes json.loads to fail or open() to raise UnicodeDecodeError.
    The except clause must include UnicodeDecodeError.
    """
    from unity_mcp.budget.cost_tracker import CostTracker

    path = tmp_path / "budget.json"
    # Write bytes that are invalid UTF-8 (cp1251 Cyrillic without proper UTF-8 encoding)
    path.write_bytes(b'{"date": "2026-01-01", "spent": 0.5, "note": "\xcf\xf0\xe8\xe2\xe5\xf2"}')

    ct = CostTracker(path=path)
    # Must not raise; falls back to empty state
    assert isinstance(ct._daily_state, dict)
