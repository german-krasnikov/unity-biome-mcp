"""Live test fixtures. All live tests skip automatically when Unity bridge is down."""
import asyncio
import logging
import os
import re
import socket
import subprocess
import time
import uuid

import pytest
import pytest_asyncio

from unity_mcp.bridge import UnityBridge


def strip_markers(text: str) -> str:
    """Strip middleware suffixes appended to responses."""
    text = re.sub(r'\n?\[confidence: [\d.]+\].*$', '', text, flags=re.DOTALL).strip()
    text = re.sub(r'\n?⚠ CONSOLE ERRORS:\n.*$', '', text, flags=re.DOTALL).strip()
    text = re.sub(r'\n?\[next: [^\]]+\]', '', text).strip()
    return text

LIVE_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
LIVE_PORT = int(os.environ.get("UNITY_MCP_PORT", "9500"))
GRIDTEST_SCENE = "Assets/Scenes/GridTest.unity"


def _bridge_up(host: str = LIVE_HOST, port: int = LIVE_PORT, timeout: float = 0.2) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except (OSError, socket.timeout):
        return False


def pytest_collection_modifyitems(items):
    """Order: edit-mode → play-mode → destructive reconnect (last).

    Tests with ensure_edit_mode fixture go to edit bucket regardless of module.
    """
    edit_mode, play_mode, destructive = [], [], []
    for item in items:
        if "test_reconnect" in item.nodeid:
            destructive.append(item)
        elif "ensure_edit_mode" in getattr(item, "fixturenames", []):
            edit_mode.append(item)
        elif any(k in item.nodeid for k in ("gridtest_playmode", "gridtest_movement", "batch_runtime")):
            play_mode.append(item)
        else:
            edit_mode.append(item)
    items[:] = edit_mode + play_mode + destructive


async def _connect_with_retry(b: UnityBridge, retries: int = 15, delay: float = 1.0):
    """Connect with backoff — handles post-domain-reload window."""
    last_err = None
    for _ in range(retries):
        try:
            await b.connect()
            return
        except (OSError, asyncio.TimeoutError) as e:
            last_err = e
            await asyncio.sleep(delay)
    raise ConnectionError(f"Bridge connect failed after {retries}s: {last_err}")


@pytest.fixture(scope="session", autouse=True)
def _require_unity():
    """Skip all live tests if Unity is not available. Kill competing MCP server if port is free."""
    # Only pkill if port is not already answering — avoid killing a valid process
    if not _bridge_up():
        subprocess.run(["pkill", "-f", "unity_mcp.server"], capture_output=True)
        time.sleep(2)  # let old process fully die + Unity drop old client
    # Wait for Unity to accept connections
    for _ in range(10):
        if _bridge_up():
            return
        time.sleep(1)
    pytest.skip(
        f"Unity bridge not on {LIVE_HOST}:{LIVE_PORT} — skipping live suite",
        allow_module_level=False,
    )


@pytest_asyncio.fixture(scope="session", autouse=True)
async def _ensure_gridtest_scene(_require_unity):
    """Open GridTest scene if not already active."""
    b = UnityBridge()
    try:
        await _connect_with_retry(b)
        r = await b.send("get_hierarchy", {})
        if "GridPlayer" not in r.get("data", ""):
            await b.send("scene", {"action": "open", "path": GRIDTEST_SCENE})
            await asyncio.sleep(2)
    except Exception:
        pass
    finally:
        await b.close()


@pytest_asyncio.fixture(scope="session")
async def _save_scene_before_play(_ensure_gridtest_scene):
    """Save scene so Unity won't show 'Save Modified Scenes?' dialog on Play Mode entry."""
    b = UnityBridge()
    try:
        await _connect_with_retry(b)
        code = (
            'var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
            'bool ok = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s); '
            'return ok ? "saved" : "save_failed";'
        )
        await b.send("execute_code", {"code": code})
    except Exception:
        pass
    finally:
        await b.close()


_CLOSE_SCENES_CODE = (
    'int closed = 0; '
    'var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
    'for (int i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--) { '
    '  var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); '
    '  if (!s.IsValid() || !s.isLoaded) continue; '
    '  if (s.handle == active.handle) continue; '
    '  if (s.name.StartsWith("LiveMS_") || s.name.StartsWith("LiveSS_")) { '
    '    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true); closed++; '
    '  } '
    '} '
    'return closed + " orphan scenes closed";'
)
# Covers: Live* (test objects), Del* (DelForceContainer/DelLeaf left by interrupted sessions)
_DESTROY_ORPHANS_CODE = (
    'var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(); '
    'int count = 0; '
    'foreach(var go in roots) { '
    '  if(go.name.StartsWith("Live") || go.name.StartsWith("Del")) { '
    '    UnityEngine.Object.DestroyImmediate(go); count++; '
    '  } '
    '} '
    'return count + " orphans cleaned";'
)
_SAVE_CODE = (
    'var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); '
    'UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s); '
    'return "saved";'
)


async def _run_orphan_cleanup() -> None:
    """Close orphan additive scenes and destroy orphan root objects.

    Called at session START (clears interrupted-previous-session residue)
    and session END (clears current-session residue).
    """
    if not _bridge_up():
        return
    b = UnityBridge()
    try:
        for attempt in range(2):
            try:
                await b.connect()
                await b.send("execute_code", {"code": _CLOSE_SCENES_CODE})
                await b.send("execute_code", {"code": _DESTROY_ORPHANS_CODE})
                await b.send("execute_code", {"code": _SAVE_CODE})
                break
            except Exception as e:
                logging.warning(f"cleanup failed (attempt {attempt + 1}): {e}")
                if attempt == 0:
                    await asyncio.sleep(2)
    finally:
        await b.close()


@pytest_asyncio.fixture(scope="session", autouse=True)
async def _cleanup_orphans():
    """Sweep orphans at session START and END.

    START: clears objects left by an interrupted previous session.
    END: clears objects left by this session.

    Closes LiveMS_*/LiveSS_* additive scenes and destroys Live*/Del* root objects.
    Del* covers DelForceContainer/DelLeaf residue from deletion-feature tests.
    """
    await _run_orphan_cleanup()  # pre-session: clear prior-session residue
    yield
    await _run_orphan_cleanup()  # post-session: clear this-session residue


@pytest_asyncio.fixture
async def bridge():
    b = UnityBridge()
    await _connect_with_retry(b)
    yield b
    await b.close()


_SNAP_CODE = (
    'return string.Join(",", UnityEngine.SceneManagement.SceneManager'
    '.GetActiveScene().GetRootGameObjects().Select(go => go.name));'
)


@pytest_asyncio.fixture(autouse=True)
async def _orphan_guard(bridge):
    """Snapshot root object names before each test; destroy + fail if any leaked."""
    try:
        r = await bridge.send("execute_code", {"code": _SNAP_CODE})
        raw = r.get("data", "").strip()
        before = set(raw.split(",")) if raw else set()
    except Exception:
        yield
        return
    yield
    try:
        r = await bridge.send("execute_code", {"code": _SNAP_CODE})
        raw = r.get("data", "").strip()
        after_names = raw.split(",") if raw else []
        leaked = [n for n in after_names if n not in before]
        if not leaked:
            return
        for name in leaked:
            escaped = name.replace('"', '\\"')
            try:
                await bridge.send("execute_code", {"code": (
                    f'var go = GameObject.Find("{escaped}"); '
                    f'if(go) UnityEngine.Object.DestroyImmediate(go); return "ok";'
                )})
            except Exception:
                pass
        pytest.fail(f"Test leaked {len(leaked)} root objects (cleaned up): {', '.join(leaked)}")
    except Exception:
        pass


# ---------------------------------------------------------------------------
# Play Mode helpers (shared across test modules)
# ---------------------------------------------------------------------------

PLAYER = "/GridPlayer"
COMP = "GridPlayer"


async def _enter_play(b: UnityBridge) -> None:
    """Enter Play Mode and poll until playing:True (max 20s).

    Domain reload kills TCP. We reconnect manually each iteration.
    No heartbeat — we ARE the only client, no race condition.
    """
    try:
        await b.send("editor", {"action": "play"})
    except Exception:
        pass
    for _ in range(20):
        await asyncio.sleep(1)
        if not b.connected:
            try:
                await b.connect()
            except Exception:
                continue
        try:
            r = await b.send("editor", {"action": "state"})
            if "playing:True" in r.get("data", ""):
                break
        except Exception:
            pass
    else:
        raise RuntimeError("Failed to enter Play Mode within 20s")

    # Wait for scene objects to initialise after entering Play Mode (max 5s)
    player_name = PLAYER.lstrip("/")
    for _ in range(10):
        try:
            h = await b.send("get_hierarchy", {})
            if player_name in h.get("data", ""):
                return
        except Exception:
            pass
        await asyncio.sleep(0.5)
    raise RuntimeError(f"{player_name} not found in hierarchy after entering Play Mode")


async def _stop_play(b: UnityBridge) -> None:
    """Stop Play Mode and poll until playing:False (max 10s).

    Exiting Play Mode also triggers domain reload on some Unity versions.
    """
    try:
        await b.send("editor", {"action": "stop"})
    except Exception:
        pass
    for _ in range(20):
        await asyncio.sleep(0.5)
        if not b.connected:
            try:
                await b.connect()
            except Exception:
                continue
        try:
            r = await b.send("editor", {"action": "state"})
            if "playing:False" in r.get("data", ""):
                return
        except Exception:
            pass


def _data(result) -> str:
    if isinstance(result, dict):
        return result.get("data") or result.get("err", "")
    return str(result)


def _ok(result) -> str:
    """Assert result is ok and return data string."""
    d = result.get("data", "") if isinstance(result, dict) else str(result)
    err = result.get("err", "") if isinstance(result, dict) else ""
    ok = result.get("ok", True) if isinstance(result, dict) else True
    assert ok, f"cmd failed: {err or d}"
    return d


def _iid(text: str) -> str:
    """Extract '#instanceId' from text, assert present."""
    import re
    m = re.search(r'#(-?\d+)', text)
    assert m, f"No instance ID in: {text}"
    return m.group(0)


async def _reset(b: UnityBridge) -> None:
    await b.send("invoke_method", {
        "path": PLAYER, "component": COMP, "method": "ResetState", "args": ""
    })
    await asyncio.sleep(0.1)


async def _reload_scene(b: UnityBridge) -> None:
    """Reload GridTest scene in PlayMode for full state isolation (~0.5s).

    Uses EditorSceneManager.LoadSceneAsyncInPlayMode — works without build settings.
    """
    code = (
        'var op = UnityEditor.SceneManagement.EditorSceneManager'
        '.LoadSceneAsyncInPlayMode('
        f'"{GRIDTEST_SCENE}", '
        'new UnityEngine.SceneManagement.LoadSceneParameters('
        'UnityEngine.SceneManagement.LoadSceneMode.Single));'
        'return "reload_started";'
    )
    await b.send("execute_code", {"code": code})
    await asyncio.sleep(0.5)
    for _ in range(10):
        try:
            h = await b.send("get_hierarchy", {})
            if "GridPlayer" in h.get("data", ""):
                return
        except Exception:
            pass
        await asyncio.sleep(0.2)
    raise RuntimeError("Scene reload failed: GridPlayer not found")


async def _clear_console(b: UnityBridge) -> None:
    """Clear ConsoleCapture buffer (removes [MCP] reconnect noise before ASSERT_CONSOLE_CLEAN)."""
    try:
        await b.send("clear_console", {})
    except Exception:
        pass


# ---------------------------------------------------------------------------
# Session-scoped Play Mode (enter once, exit once)
# ---------------------------------------------------------------------------

@pytest_asyncio.fixture(scope="session")
async def _play_mode_session(_save_scene_before_play):
    """Enter Play Mode ONCE for all play-mode tests in the session."""
    b = UnityBridge()
    try:
        await _connect_with_retry(b)
        await _enter_play(b)
        await _clear_console(b)
    except Exception as e:
        await b.close()
        pytest.skip(f"Could not enter Play Mode: {e}")
        return
    yield
    try:
        await _stop_play(b)
    except Exception:
        pass
    finally:
        await b.close()


@pytest_asyncio.fixture
async def play_session(bridge, _play_mode_session):
    """Per-test bridge. PlayMode already active from session fixture."""
    r = await bridge.send("editor", {"action": "state"})
    if "playing:True" not in r.get("data", ""):
        await _enter_play(bridge)
    yield bridge


@pytest_asyncio.fixture
async def fresh_scene(bridge, _play_mode_session):
    """Per-test bridge with full scene reload for guaranteed isolation."""
    r = await bridge.send("editor", {"action": "state"})
    if "playing:True" not in r.get("data", ""):
        await _enter_play(bridge)
    await _reload_scene(bridge)
    yield bridge


@pytest_asyncio.fixture
async def ensure_edit_mode(bridge):
    """Ensure Edit Mode before test (for tests that may follow Play Mode modules)."""
    r = await bridge.send("editor", {"action": "state"})
    if "playing:True" in r.get("data", ""):
        await _stop_play(bridge)
    yield bridge


async def _destroy(bridge: UnityBridge, name: str) -> None:
    """Destroy a root-level GameObject by name using DestroyImmediate."""
    code = (
        f'var go = GameObject.Find("{name}"); '
        f'if(go) {{ UnityEngine.Object.DestroyImmediate(go); return "ok"; }} '
        f'return "not found";'
    )
    try:
        await bridge.send("execute_code", {"code": code})
    except Exception:
        pass


@pytest_asyncio.fixture
async def sandbox(bridge):
    """UUID-named GameObject, cleaned up in finally via DestroyImmediate."""
    name = f"Live_{uuid.uuid4().hex[:8]}"
    await bridge.send("create_object", {"name": name})
    path = f"/{name}"
    try:
        yield path
    finally:
        await _destroy(bridge, name)


@pytest.fixture
def sampling_mock(monkeypatch):
    monkeypatch.setattr(
        "unity_mcp.sampling.SamplingService.enabled",
        property(lambda self: False),
        raising=False,
    )


@pytest.fixture
def hinter_enabled(monkeypatch):
    """Override the global UNITY_MCP_HINTS=0 default set by the unit-test conftest."""
    monkeypatch.setenv("UNITY_MCP_HINTS", "1")


@pytest.fixture
def visual_verify_enabled(monkeypatch):
    """Override for visual diff tests that need SamplingService.enabled=True."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")


@pytest.fixture
def cost_cap():
    from unity_mcp.metrics import METRICS
    counters = METRICS.snapshot().get("counters", {})
    before = counters.get("sampling.calls", 0)
    yield
    counters = METRICS.snapshot().get("counters", {})
    delta = counters.get("sampling.calls", 0) - before
    assert delta <= 2, f"Test made {delta} Haiku calls (limit 2). Possible loop or retry."


@pytest_asyncio.fixture
async def wrapped_bridge(bridge):
    """Production-style bridge with middleware pipeline."""
    from unity_mcp.middleware import Middleware, wrap_send
    from unity_mcp.timeout_categories import get_timeout

    async def send_with_timeout(cmd, args, timeout=0):
        if timeout <= 0:
            timeout = get_timeout(cmd)
        return await bridge.send(cmd, args, timeout=timeout)

    mw = Middleware()
    wrapped_send = wrap_send(send_with_timeout, mw)

    class WrappedBridge:
        def __init__(self):
            self.send = wrapped_send
            self.connected = bridge.connected
            self._raw = bridge
            self._raw_send = send_with_timeout  # timeout-aware shim for custom wrap_send

    return WrappedBridge()
