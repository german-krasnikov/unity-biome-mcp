import os
import sys
import warnings

# PYTHONWARNDEFAULTENCODING must be set BEFORE interpreter starts — os.environ here is too late.
# Run with: PYTHONWARNDEFAULTENCODING=1 pytest tests/
# The filterwarnings = ["error::EncodingWarning"] in pyproject.toml turns warnings into errors
# when the env-var gate is active.
if sys.flags.warn_default_encoding == 0:
    warnings.warn(
        "Encoding gate inactive — run: PYTHONWARNDEFAULTENCODING=1 pytest",
        UserWarning,
        stacklevel=1,
    )

import asyncio
import json
import re as _re
import struct
import shutil
import warnings as _warnings
import pytest
from pathlib import Path
from unittest.mock import AsyncMock, Mock, patch


_OR_ASSERT_RE = _re.compile(
    r'assert\s+["\'][^"\']+["\']\s+in\s+\S+\s+or\s+["\'][^"\']+["\']\s+in\s+'
)


def pytest_collection_modifyitems(config, items):
    """Soft-warn on OR-asserts in test source. Cycle 3a swept existing — guard new ones.

    Opt-in only (UNITY_MCP_OR_GUARD=1) to avoid log noise in default runs.
    Enable with: UNITY_MCP_OR_GUARD=1 pytest tests/
    Prefer: `and`, regex, or separate branches over OR-asserts.
    """
    if os.environ.get("UNITY_MCP_OR_GUARD") != "1":
        return
    seen: set = set()
    for item in items:
        path = str(item.fspath)
        if path in seen or "/live/" in path:
            continue
        seen.add(path)
        try:
            with open(path, encoding="utf-8") as f:
                for lineno, line in enumerate(f, 1):
                    if _OR_ASSERT_RE.search(line):
                        _warnings.warn(
                            f"OR-assert at {path}:{lineno} — use `and`, regex, or branch.",
                            UserWarning,
                        )
        except OSError:
            pass


@pytest.fixture(scope="session", autouse=True)
def _isolate_home(tmp_path_factory):
    """Redirect Path.home() per session to prevent ~/.unity-mcp pollution."""
    fake_home = tmp_path_factory.mktemp("home")
    real_home = Path.home
    Path.home = staticmethod(lambda: fake_home)
    yield fake_home
    Path.home = real_home





@pytest.fixture(autouse=True)
def _reset_metrics():
    """Fresh METRICS counters per test (was local in test_metrics.py)."""
    from unity_mcp.metrics import METRICS
    METRICS.reset()
    yield
    METRICS.reset()


@pytest.fixture(autouse=True)
def _reset_sampling_semaphore():
    from unity_mcp.sampling import SamplingService
    SamplingService._semaphore = None
    yield
    SamplingService._semaphore = None


@pytest.fixture(autouse=True)
def _reset_login_path_cache():
    """Fresh login-shell PATH cache per test. Without this, the first test in the
    session to exercise real CliSession.start() populates the module-global cache
    (backend_def.py:108) and poisons every later test regardless of monkeypatching,
    since login_shell_path() short-circuits on a non-None cache."""
    import unity_mcp.backend_def as bd
    bd._LOGIN_PATH_CACHE = None
    yield
    bd._LOGIN_PATH_CACHE = None


@pytest.fixture(autouse=True)
def _reset_diagnose_send():
    """Prevent diagnose._send from leaking between tests (set by server.register())."""
    import unity_mcp.tools.diagnose as _diag
    original = _diag._send
    _diag._send = None
    yield
    _diag._send = original


@pytest.fixture(autouse=True)
def _clean_unity_env(monkeypatch):
    """Default-disable env-gated features. Tests opt in via their own monkeypatch.setenv."""
    for k in ("UNITY_MCP_HINTS", "UNITY_MCP_VALIDATE", "UNITY_MCP_VISUAL_VERIFY",
              "UNITY_MCP_LESSONS", "UNITY_MCP_WATCHDOG", "UNITY_MCP_INFERENCE",
              "UNITY_MCP_SPECULATION", "UNITY_MCP_BUDGET_DISABLED",
              "UNITY_MCP_MIDDLEWARE", "UNITY_MCP_SCENE_BRIEF", "UNITY_MCP_DISTILL"):
        monkeypatch.delenv(k, raising=False)
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")


@pytest.fixture
def mock_bridge():
    """Mock the active bridge via slot/manager for server tool tests."""
    mock_b = Mock()
    mock_b.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    mock_b.connected = True

    mock_slot = Mock()
    mock_slot.bridge = mock_b
    mock_slot.connected = True
    mock_slot.port = 9500
    mock_slot.connect = AsyncMock(return_value="Connected to Unity on port 9500")
    mock_slot.close = AsyncMock()

    with patch("unity_mcp.server.slot", mock_slot), \
         patch("unity_mcp.server.manager", mock_slot):
        yield mock_b


@pytest.fixture
def mock_reader():
    """Mock asyncio StreamReader."""
    reader = AsyncMock()
    return reader


@pytest.fixture
def mock_writer():
    """Mock asyncio StreamWriter. Mirrors helpers.make_writer() as a fixture."""
    from helpers import make_writer
    return make_writer()


@pytest.fixture
def mock_connection(mock_reader, mock_writer):
    """Returns (reader, writer) tuple for mocking open_connection."""
    return (mock_reader, mock_writer)



@pytest.fixture
async def mock_unity_server():
    """Real async TCP server that echoes commands."""
    responses = {}

    async def handler(reader, writer):
        while True:
            try:
                header = await reader.readexactly(4)
                length = struct.unpack("!I", header)[0]
                payload = await reader.readexactly(length)
                request = json.loads(payload.decode("utf-8"))

                cmd = request["cmd"]
                msg_id = request["id"]

                if cmd in responses:
                    response = {"id": msg_id, "ok": True, "data": responses[cmd]}
                else:
                    response = {"id": msg_id, "ok": True, "data": f"echo:{cmd}"}

                resp_payload = json.dumps(response).encode("utf-8")
                resp_header = struct.pack("!I", len(resp_payload))
                writer.write(resp_header + resp_payload)
                await writer.drain()
            except asyncio.IncompleteReadError:
                break
        writer.close()

    server = await asyncio.start_server(handler, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]

    class MockServer:
        def __init__(self):
            self.port = port
            self.responses = responses

        def set_response(self, cmd, data):
            responses[cmd] = data

    mock = MockServer()
    yield mock

    server.close()
    await server.wait_closed()


# bridge_response() is the preferred way to configure mock_bridge.send.
# ~270 older sites still use `mock_bridge.send = AsyncMock(...)` directly —
# migrate incrementally (see tests/test_server.py top 10 for canonical examples).
@pytest.fixture
def bridge_response(mock_bridge):
    """Factory: configure mock_bridge.send return shape."""
    def _set(data="ok", ok=True, err=None, file=None):
        if file is not None:
            ret = {"ok": True, "file": file}
        elif ok:
            ret = {"ok": True, "data": data}
        else:
            ret = {"ok": False, "err": err or "unknown"}
        mock_bridge.send = AsyncMock(return_value=ret)
        return mock_bridge
    return _set


@pytest.fixture
def mw():
    """Shared Middleware instance for tests that don't need custom init."""
    from unity_mcp.middleware import Middleware
    return Middleware()


@pytest.fixture
def send_fn():
    """AsyncMock send_fn for middleware/reflect/schema_guard tests."""
    return AsyncMock(return_value="ok")
