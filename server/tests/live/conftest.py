"""Owned, project-pinned fixtures for the deterministic live acceptance lane."""
import asyncio
import ipaddress
import json
import os
from pathlib import Path
import re
import socket
import time
import uuid

import pytest
import pytest_asyncio

from unity_mcp.bridge import UnityBridge
from tests.live.unity_state_owner import (
    ObjectState,
    OwnershipPolicy,
    UnityStateSnapshot,
    build_ownership_plan,
    _needs_owned_scene_reset,
)
from tests.live._markers import strip_markers  # noqa: F401 — re-exported for test imports

LIVE_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
LIVE_PORT = int(os.environ.get("UNITY_MCP_PORT", "9500"))
LIVE_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")
REAL_PORTS_DIR = Path.home() / ".unity-biome-mcp" / "ports"
RUN_LIVE_CLI = os.environ.get("UNITY_MCP_RUN_LIVE_CLI") == "1"
GRIDTEST_SCENE = "Assets/Scenes/GridTest.unity"
LIVE_RUN_ID = f"{os.getpid()}_{uuid.uuid4().hex}"
RUN_OWNED_ROOT = f"Assets/TestsTemp/PythonLive/{LIVE_RUN_ID}"
OWNED_LIVE_SCENE = f"{RUN_OWNED_ROOT}/GridTest.unity"
LIVE_LEASE_TOKEN = f"python-live:{LIVE_RUN_ID}"
_LEASE_OWNER_KEY = "UnityMCP_python_live_lease_owner"
_LEASE_HEARTBEAT_KEY = "UnityMCP_python_live_lease_heartbeat"
_LEASE_LOCAL_KEY = "UnityMCP_python_live_lease_local"
_LEASE_PID_KEY = "UnityMCP_python_live_lease_pid"
_LEASE_PROCESS_START_KEY = "UnityMCP_python_live_lease_process_start"
_LEASE_LAST_RELEASED_KEY = "UnityMCP_python_live_lease_last_released"


def _required_live_project() -> Path:
    if not LIVE_PROJECT:
        raise RuntimeError("UNITY_MCP_PROJECT_PATH is required for live tests")
    project = Path(LIVE_PROJECT).resolve()
    if not (project / "Assets").is_dir() or not (project / "Packages").is_dir():
        raise RuntimeError(f"UNITY_MCP_PROJECT_PATH is not a Unity project: {project}")
    return project


def _worker_port_candidates() -> list[int]:
    project = _required_live_project()
    records: list[tuple[float, int, Path]] = []
    for port_file in REAL_PORTS_DIR.glob("*.port"):
        try:
            lines = port_file.read_text(encoding="utf-8").splitlines()
            port = int(lines[0])
            if 1 <= port <= 65535:
                records.append((port_file.stat().st_mtime, port, Path(lines[1]).resolve()))
        except (OSError, ValueError, IndexError):
            continue
    conflicted = {
        port
        for _, port, advertised_project in records
        if advertised_project != project
        and any(
            other_port == port and other_project == project
            for _, other_port, other_project in records
        )
    }
    matches = [
        (mtime, port)
        for mtime, port, advertised_project in records
        if advertised_project == project and port not in conflicted
    ]
    matches.sort(reverse=True)
    ports = list(dict.fromkeys(port for _, port in matches))
    if not ports:
        raise RuntimeError(f"no port file advertises live worker {project}")
    return ports


def current_worker_port() -> int:
    ports = _worker_port_candidates()
    return LIVE_PORT if LIVE_PORT in ports else ports[0]


def make_live_bridge() -> UnityBridge:
    project = _required_live_project()
    return UnityBridge(
        LIVE_HOST,
        port=current_worker_port(),
        port_discoverer=current_worker_port,
        expected_project_path=project,
    )


def _bridge_up(host: str = LIVE_HOST, port: int | None = None, timeout: float = 0.2) -> bool:
    del port
    try:
        candidate = current_worker_port()
    except RuntimeError:
        return False
    try:
        with socket.create_connection((host, candidate), timeout=timeout):
            return True
    except (OSError, socket.timeout):
        return False


def pytest_collection_modifyitems(items):
    """Order: edit-mode → play-mode → destructive reconnect (last).

    Tests with ensure_edit_mode fixture go to edit bucket regardless of module.
    """
    edit_mode, play_mode, destructive = [], [], []
    for item in items:
        fixtures = set(getattr(item, "fixturenames", []))
        play_fixtures = {"play_session", "fresh_scene"} & fixtures
        if "ensure_edit_mode" in fixtures and play_fixtures:
            raise pytest.UsageError(
                f"{item.nodeid} mixes ensure_edit_mode with PlayMode fixtures: "
                + ", ".join(sorted(play_fixtures))
            )
        if item.get_closest_marker("live_cli") and not RUN_LIVE_CLI:
            item.add_marker(pytest.mark.skip(
                reason=(
                    "external paid CLI lane; set UNITY_MCP_RUN_LIVE_CLI=1 "
                    "to run it"
                )
            ))
        if "test_reconnect" in item.nodeid:
            destructive.append(item)
        elif "ensure_edit_mode" in fixtures:
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
    """Require the configured worker; an unavailable live gate is a failure."""
    for _ in range(10):
        if _bridge_up():
            return
        time.sleep(1)
    pytest.fail(
        f"verified Unity worker unavailable: host={LIVE_HOST} "
        f"project={LIVE_PROJECT!r}"
    )


def _transient_id_capture_expression(object_expression: str) -> str:
    """Return the canonical Unity 6000.0 process-local object ID expression."""
    raw = f"unchecked((ulong)(long)({object_expression}).GetInstanceID())"
    return f"({raw}).ToString(System.Globalization.CultureInfo.InvariantCulture)"


def _build_state_snapshot_code() -> str:
    transient_id = _transient_id_capture_expression("go")
    return (
        'System.Func<string,string> enc = value => System.Convert.ToBase64String('
        'System.Text.Encoding.UTF8.GetBytes(value ?? ""));'
        'var lines = new System.Collections.Generic.List<string>();'
        'lines.Add("P\\t" + (UnityEditor.EditorApplication.isPlaying ? "1" : "0"));'
        'lines.Add("T\\t" + UnityEngine.Time.timeScale.ToString('
        ' "R", System.Globalization.CultureInfo.InvariantCulture));'
        'var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();'
        'for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {'
        ' var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);'
        ' if (!scene.IsValid() || !scene.isLoaded) continue;'
        ' lines.Add("S\\t" + enc(scene.path) + "\\t" + enc(scene.name) + "\\t" +'
        ' scene.handle + "\\t" + (scene.isDirty ? "1" : "0") + "\\t" +'
        ' (scene.handle == active.handle ? "1" : "0") + "\\t0");'
        ' foreach (var root in scene.GetRootGameObjects()) {'
        '  foreach (var transform in root.GetComponentsInChildren<UnityEngine.Transform>(true)) {'
        '   var go = transform.gameObject;'
        '   var gid = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(go);'
        '   var stable = (int)gid.identifierType == 0 ? "" : gid.ToString();'
        '   var indices = new System.Collections.Generic.List<int>();'
        '   var cursor = transform;'
        '   while (cursor != null) { indices.Insert(0, cursor.GetSiblingIndex()); cursor = cursor.parent; }'
        f'   lines.Add("O\\t" + enc(stable) + "\\t" + {transient_id} + "\\t" +'
        '    enc(scene.path) + "\\t" + enc(string.Join("/", indices)) + "\\t" +'
        '    enc(go.name) + "\\t0");'
        '  }'
        ' }'
        '}'
        'var assetPaths = new System.Collections.Generic.HashSet<string>('
        ' System.StringComparer.Ordinal);'
        f'if (UnityEditor.AssetDatabase.IsValidFolder({_cs(RUN_OWNED_ROOT)})) {{'
        ' foreach (var guid in UnityEditor.AssetDatabase.FindAssets('
        f'  "", new string[] {{ {_cs(RUN_OWNED_ROOT)} }})) {{'
        '  var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);'
        '  if (!UnityEditor.AssetDatabase.IsValidFolder(path))'
        '   assetPaths.Add(path);'
        ' }'
        '}'
        'var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine('
        ' UnityEngine.Application.dataPath, "..")).Replace("\\\\", "/");'
        f'var runRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine('
        f' projectRoot, {_cs(RUN_OWNED_ROOT)})).Replace("\\\\", "/");'
        'if (System.IO.Directory.Exists(runRoot)) {'
        ' foreach (var file in System.IO.Directory.EnumerateFiles('
        '  runRoot, "*", System.IO.SearchOption.AllDirectories)) {'
        '  var normalized = System.IO.Path.GetFullPath(file).Replace("\\\\", "/");'
        '  if (normalized.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))'
        '   continue;'
        '  if (normalized.StartsWith(projectRoot + "/", System.StringComparison.Ordinal))'
        '   assetPaths.Add(normalized.Substring(projectRoot.Length + 1));'
        ' }'
        '}'
        'foreach (var path in assetPaths) lines.Add("A\\t" + enc(path));'
        'return string.Join("\\n", lines);'
    )


def _response_data(result, operation: str) -> str:
    if not isinstance(result, dict):
        raise AssertionError(f"{operation} returned a non-object response: {result!r}")
    if not result.get("ok", True):
        raise AssertionError(
            f"{operation} failed: {result.get('err') or result.get('data') or result!r}"
        )
    return strip_markers(str(result.get("data", "")))


async def _execute_checked(bridge: UnityBridge, code: str, operation: str) -> str:
    result = await bridge.send("execute_code", {"code": code})
    return _response_data(result, operation)


async def _transient_id_expression(
    _bridge: UnityBridge,
    object_expression: str,
) -> str:
    return _transient_id_capture_expression(object_expression)


_RELOAD_FAILURE_MARKERS = ("domain reload", "outcome is uncertain")


def _is_reload_related(exc: Exception) -> bool:
    text = str(exc).lower()
    return any(marker in text for marker in _RELOAD_FAILURE_MARKERS)


async def _wait_compile_idle(
    bridge: UnityBridge, budget_s: float = 90.0, interval_s: float = 2.0
) -> bool:
    """Poll compile_status (read-only) until Unity is idle, bounded by budget_s.

    Never closes or reconnects the bridge: DomainReloadTracker documents a
    reload can run up to 90s (DOMAIN_RELOAD_EXPIRY_S) on slow machines, and
    repeated bridge.close()/reconnect cycling as the retry strategy was
    measured to add TCP churn that lengthened downstream test timing during
    an active Unity compile (see commit d41bc6e0). Returns True once idle is
    observed (or immediately if already idle), False if the budget expires —
    the caller decides what to do next.
    """
    deadline = time.monotonic() + budget_s
    while True:
        try:
            status = await bridge.send("compile_status", {})
            data = status.get("data", "") if isinstance(status, dict) else str(status)
            if "compiling" not in str(data).lower():
                return True
        except (ConnectionError, OSError):
            pass
        if time.monotonic() >= deadline:
            return False
        await asyncio.sleep(interval_s)


async def _capture_unity_state(bridge: UnityBridge) -> UnityStateSnapshot:
    last_error = None
    for attempt in range(3):
        try:
            payload = await _execute_checked(
                bridge, _build_state_snapshot_code(), "capture Unity test state"
            )
            return UnityStateSnapshot.parse(payload)
        except Exception as exc:
            last_error = exc
            if attempt == 2:
                break
            if _is_reload_related(exc):
                # Bounded read-only re-probe: wait out the reload in place,
                # then retry with a fresh op_id (the next _execute_checked
                # call) — no bridge.close()/reconnect churn.
                await _wait_compile_idle(bridge)
                continue
            try:
                await bridge.close()
            finally:
                await _connect_with_retry(bridge, retries=10, delay=0.5)
    raise AssertionError(f"could not capture Unity state: {last_error}")


def _cs(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def _is_loopback_host(host: str) -> bool:
    if host.lower() == "localhost":
        return True
    try:
        return ipaddress.ip_address(host).is_loopback
    except ValueError:
        return False


def _build_live_lease_acquire_code() -> str:
    token = _cs(LIVE_LEASE_TOKEN)
    local_client = "true" if _is_loopback_host(LIVE_HOST) else "false"
    return (
        f'var token = {token}; var clientPid = {os.getpid()};'
        f'var localClient = {local_client};'
        f'var ownerKey = {_cs(_LEASE_OWNER_KEY)};'
        f'var heartbeatKey = {_cs(_LEASE_HEARTBEAT_KEY)};'
        f'var localKey = {_cs(_LEASE_LOCAL_KEY)};'
        f'var pidKey = {_cs(_LEASE_PID_KEY)};'
        f'var processStartKey = {_cs(_LEASE_PROCESS_START_KEY)};'
        f'var lastReleasedKey = {_cs(_LEASE_LAST_RELEASED_KEY)};'
        'var owner = UnityEditor.SessionState.GetString(ownerKey, "");'
        'var now = System.DateTime.UtcNow.Ticks.ToString('
        ' System.Globalization.CultureInfo.InvariantCulture);'
        'if (owner == token) {'
        ' UnityEditor.SessionState.SetString(heartbeatKey, now);'
        ' return "renewed";'
        '}'
        'var reclaimingDeadOwner = false;'
        'if (!string.IsNullOrEmpty(owner)) {'
        ' if (!UnityEditor.SessionState.GetBool(localKey, false))'
        '  return "held-remote-owner:" + owner;'
        'var ownerPid = UnityEditor.SessionState.GetInt(pidKey, 0);'
        'var recordedStart = UnityEditor.SessionState.GetString(processStartKey, "");'
        'if (ownerPid <= 0 || string.IsNullOrEmpty(recordedStart))'
        ' return "held-unverifiable-owner:" + owner;'
        'try {'
        ' using (var process = System.Diagnostics.Process.GetProcessById(ownerPid)) {'
        '  var actualStart = process.StartTime.ToUniversalTime().Ticks.ToString('
        '   System.Globalization.CultureInfo.InvariantCulture);'
        '  if (actualStart == recordedStart) return "held-live-owner:" + owner;'
        ' }'
        '} catch (System.ArgumentException) {'
        '} catch (System.Exception) {'
        ' return "held-unverifiable-owner:" + owner;'
        '}'
        'reclaimingDeadOwner = true;'
        '}'
        'var clientStart = "";'
        'if (localClient) {'
        ' try {'
        '  using (var process = System.Diagnostics.Process.GetProcessById(clientPid))'
        '   clientStart = process.StartTime.ToUniversalTime().Ticks.ToString('
        '    System.Globalization.CultureInfo.InvariantCulture);'
        ' } catch (System.Exception) { return "invalid-local-client"; }'
        '}'
        'UnityEditor.SessionState.SetString(heartbeatKey, now);'
        'UnityEditor.SessionState.SetBool(localKey, localClient);'
        'UnityEditor.SessionState.SetInt(pidKey, clientPid);'
        'UnityEditor.SessionState.SetString(processStartKey, clientStart);'
        'UnityEditor.SessionState.SetString(lastReleasedKey, "");'
        'UnityEditor.SessionState.SetString(ownerKey, token);'
        'return reclaimingDeadOwner ? "reclaimed-dead-owner" : "acquired";'
    )


def _build_live_lease_renew_code() -> str:
    return (
        f'var token = {_cs(LIVE_LEASE_TOKEN)};'
        f'var ownerKey = {_cs(_LEASE_OWNER_KEY)};'
        'var owner = UnityEditor.SessionState.GetString(ownerKey, "");'
        'if (owner != token) return "not-owner:" + owner;'
        'UnityEditor.SessionState.SetString('
        f' {_cs(_LEASE_HEARTBEAT_KEY)}, System.DateTime.UtcNow.Ticks.ToString('
        ' System.Globalization.CultureInfo.InvariantCulture));'
        'return "renewed";'
    )


def _build_live_lease_release_code() -> str:
    return (
        f'var token = {_cs(LIVE_LEASE_TOKEN)};'
        f'var ownerKey = {_cs(_LEASE_OWNER_KEY)};'
        f'var lastReleasedKey = {_cs(_LEASE_LAST_RELEASED_KEY)};'
        'var owner = UnityEditor.SessionState.GetString(ownerKey, "");'
        'if (string.IsNullOrEmpty(owner) &&'
        ' UnityEditor.SessionState.GetString(lastReleasedKey, "") == token)'
        ' return "already-released";'
        'if (owner != token) return "not-owner:" + owner;'
        f'UnityEditor.SessionState.SetString({_cs(_LEASE_HEARTBEAT_KEY)}, "");'
        f'UnityEditor.SessionState.SetBool({_cs(_LEASE_LOCAL_KEY)}, false);'
        f'UnityEditor.SessionState.SetInt({_cs(_LEASE_PID_KEY)}, 0);'
        f'UnityEditor.SessionState.SetString({_cs(_LEASE_PROCESS_START_KEY)}, "");'
        'UnityEditor.SessionState.SetString(lastReleasedKey, token);'
        'UnityEditor.SessionState.SetString(ownerKey, "");'
        'return "released";'
    )


async def _execute_lease_checked(
    bridge: UnityBridge,
    code: str,
    operation: str,
) -> str:
    last_error = None
    for attempt in range(3):
        try:
            return await _execute_checked(bridge, code, operation)
        except Exception as exc:
            last_error = exc
            if attempt == 2:
                break
            try:
                await bridge.close()
            finally:
                await _connect_with_retry(bridge, retries=10, delay=0.5)
    raise AssertionError(f"{operation} failed after reconnect retries: {last_error}")


async def _acquire_live_suite_lease(bridge: UnityBridge) -> str:
    return await _execute_lease_checked(
        bridge,
        _build_live_lease_acquire_code(),
        "acquire live-suite lease",
    )


async def _renew_live_suite_lease(bridge: UnityBridge) -> str:
    return await _execute_lease_checked(
        bridge,
        _build_live_lease_renew_code(),
        "renew live-suite lease",
    )


async def _release_live_suite_lease(bridge: UnityBridge) -> str:
    return await _execute_lease_checked(
        bridge,
        _build_live_lease_release_code(),
        "release live-suite lease",
    )


def _assert_lease_owned(outcome: str, operation: str) -> None:
    expected = {
        "acquire": {"acquired", "renewed", "reclaimed-dead-owner"},
        "renew": {"renewed"},
        "release": {"released", "already-released"},
    }[operation]
    if outcome not in expected:
        raise AssertionError(
            f"live-suite lease {operation} refused: {outcome}. "
            "Another live suite may own this Unity Editor."
        )


async def _destroy_owned_object(bridge: UnityBridge, obj: ObjectState) -> None:
    code = (
        f'var stable = {_cs(obj.global_id)};'
        f'var transientId = System.UInt64.Parse({_cs(obj.transient_id)}, '
        ' System.Globalization.CultureInfo.InvariantCulture);'
        'UnityEngine.GameObject go = null;'
        'if (!string.IsNullOrEmpty(stable)) {'
        ' UnityEditor.GlobalObjectId gid;'
        ' if (!UnityEditor.GlobalObjectId.TryParse(stable, out gid)) return "invalid-stable-id";'
        ' go = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as UnityEngine.GameObject;'
        '} else {'
        ' go = UnityEditor.EditorUtility.InstanceIDToObject('
        '  unchecked((int)transientId)) as UnityEngine.GameObject;'
        ' if (go != null && unchecked((ulong)(long)go.GetInstanceID()) != transientId)'
        '  return "identity-mismatch";'
        '}'
        'if (go == null) return "already-absent";'
        f'if (go.scene.path != {_cs(obj.scene_path)}) return "scene-mismatch";'
        'if (string.IsNullOrEmpty(stable)) {'
        f' if (go.name != {_cs(obj.name)}) return "name-mismatch";'
        ' var indices = new System.Collections.Generic.List<int>();'
        ' var cursor = go.transform;'
        ' while (cursor != null) {'
        '  indices.Insert(0, cursor.GetSiblingIndex()); cursor = cursor.parent;'
        ' }'
        f' if (string.Join("/", indices) != {_cs(obj.hierarchy_path)})'
        '  return "hierarchy-mismatch";'
        '}'
        'UnityEngine.Object.DestroyImmediate(go); return "destroyed";'
    )
    outcome = await _execute_checked(bridge, code, f"destroy {obj.identity}")
    if outcome not in {"destroyed", "already-absent"}:
        raise AssertionError(f"refused to destroy {obj.identity}: {outcome}")


async def _close_owned_scene(bridge: UnityBridge, path: str) -> None:
    if not _is_run_owned_path(path):
        raise AssertionError(f"refused to close scene outside live run root: {path}")
    code = (
        f'var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath({_cs(path)});'
        'if (!scene.IsValid() || !scene.isLoaded) return "already-closed";'
        'if (!UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true))'
        ' return "close-failed"; return "closed";'
    )
    outcome = await _execute_checked(bridge, code, f"close owned scene {path}")
    if outcome not in {"closed", "already-closed"}:
        raise AssertionError(f"could not close owned scene {path}: {outcome}")


async def _delete_owned_asset(bridge: UnityBridge, path: str) -> None:
    if not _is_run_owned_path(path):
        raise AssertionError(f"refused to delete asset outside live run root: {path}")
    code = (
        f'var path = {_cs(path)};'
        'var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine('
        ' UnityEngine.Application.dataPath, "..", path));'
        'var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);'
        'if (string.IsNullOrEmpty(guid) && !System.IO.File.Exists(fullPath) &&'
        ' !System.IO.Directory.Exists(fullPath)) return "already-absent";'
        'if (UnityEditor.AssetDatabase.DeleteAsset(path)) return "deleted";'
        'if (System.IO.File.Exists(fullPath)) {'
        ' System.IO.File.Delete(fullPath);'
        ' if (System.IO.File.Exists(fullPath + ".meta")) System.IO.File.Delete(fullPath + ".meta");'
        ' return "deleted-raw";'
        '}'
        'if (System.IO.Directory.Exists(fullPath)) {'
        ' System.IO.Directory.Delete(fullPath, true);'
        ' if (System.IO.File.Exists(fullPath + ".meta")) System.IO.File.Delete(fullPath + ".meta");'
        ' return "deleted-raw";'
        '}'
        'return "delete-failed";'
    )
    outcome = await _execute_checked(bridge, code, f"delete owned asset {path}")
    if outcome not in {"deleted", "deleted-raw", "already-absent"}:
        raise AssertionError(f"could not delete owned asset {path}: {outcome}")


async def _reset_owned_scene(bridge: UnityBridge, path: str, playing: bool) -> None:
    if path != OWNED_LIVE_SCENE:
        raise AssertionError(f"refused to reset non-primary live scene: {path}")
    restore_asset_preflight = (
        f'var source = {_cs(GRIDTEST_SCENE)}; var target = {_cs(path)};'
        'var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine('
        ' UnityEngine.Application.dataPath, ".."));'
        'var sourceFull = System.IO.Path.Combine(projectRoot, source);'
        'var targetFull = System.IO.Path.Combine(projectRoot, target);'
        'if (!System.IO.File.Exists(sourceFull)) return "source-missing";'
    )
    restore_asset = (
        'var targetAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<'
        ' UnityEditor.SceneAsset>(target);'
        'var contentMatches = System.IO.File.Exists(targetFull) &&'
        ' System.Linq.Enumerable.SequenceEqual('
        '  System.IO.File.ReadAllBytes(sourceFull),'
        '  System.IO.File.ReadAllBytes(targetFull));'
        'if (!contentMatches || targetAsset == null) {'
        ' if (!contentMatches) System.IO.File.Copy(sourceFull, targetFull, true);'
        ' UnityEditor.AssetDatabase.ImportAsset(target,'
        '  UnityEditor.ImportAssetOptions.ForceSynchronousImport |'
        '  UnityEditor.ImportAssetOptions.ForceUpdate);'
        '}'
        'if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(target) == null)'
        ' return "target-import-failed";'
    )
    if not playing:
        code = "".join((
            'if (UnityEditor.EditorApplication.isPlaying) return "unexpected-play-mode";',
            restore_asset_preflight,
            f'var runRoot = {_cs(RUN_OWNED_ROOT)};'
            'var loaded = new System.Collections.Generic.List<'
            ' UnityEngine.SceneManagement.Scene>();'
            'for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {'
            ' var current = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);'
            ' if (!current.IsValid() || !current.isLoaded) continue;'
            ' if (string.IsNullOrEmpty(current.path) ||'
            '  !(current.path == runRoot || current.path.StartsWith('
            '   runRoot + "/", System.StringComparison.Ordinal)))'
            '  return "unsafe-loaded-scene:" + current.path;'
            ' loaded.Add(current);'
            '}'
            'var guard = UnityEditor.SceneManagement.EditorSceneManager.NewScene('
            ' UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,'
            ' UnityEditor.SceneManagement.NewSceneMode.Additive);'
            'if (!guard.IsValid()) return "guard-scene-failed";'
            'var activeGuard = UnityEngine.SceneManagement.SceneManager.GetActiveScene();'
            'if (activeGuard.handle != guard.handle &&'
            ' !UnityEngine.SceneManagement.SceneManager.SetActiveScene(guard)) {'
            ' UnityEditor.SceneManagement.EditorSceneManager.CloseScene(guard, true);'
            ' return "guard-scene-failed";'
            '}'
            'for (int i = loaded.Count - 1; i >= 0; i--) {'
            ' if (!UnityEditor.SceneManagement.EditorSceneManager.CloseScene(loaded[i], true)) {'
            '  UnityEditor.SceneManagement.EditorSceneManager.CloseScene(guard, true);'
            '  return "close-failed:" + loaded[i].path;'
            ' }'
            '}',
            restore_asset,
            f'var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene({_cs(path)}, '
            'UnityEditor.SceneManagement.OpenSceneMode.Single);'
            'return scene.IsValid() && !scene.isDirty ? scene.path : "open-failed";',
        ))
        outcome = await _execute_checked(bridge, code, f"reset owned scene {path}")
        if outcome != path:
            raise AssertionError(f"could not reset owned scene {path}: {outcome}")
        return

    code = "".join((
        'if (!UnityEditor.EditorApplication.isPlaying) return "unexpected-edit-mode";',
        restore_asset_preflight,
        f'var runRoot = {_cs(RUN_OWNED_ROOT)};'
        'for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {'
        ' var current = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);'
        ' if (!current.IsValid() || !current.isLoaded) continue;'
        ' if (string.IsNullOrEmpty(current.path) ||'
        '  !(current.path == runRoot || current.path.StartsWith('
        '   runRoot + "/", System.StringComparison.Ordinal)))'
        '  return "unsafe-loaded-scene:" + current.path;'
        '}',
        restore_asset,
        'var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();'
        'var previousHandle = previous.IsValid() ? previous.handle : 0;'
        'var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode('
        f'{_cs(path)}, new UnityEngine.SceneManagement.LoadSceneParameters('
        'UnityEngine.SceneManagement.LoadSceneMode.Single));'
        'return operation == null ? "reload-failed" : "reload-started:" + previousHandle;',
    ))
    outcome = await _execute_checked(bridge, code, f"reload owned play scene {path}")
    if not outcome.startswith("reload-started:"):
        raise AssertionError(f"could not reload owned play scene {path}: {outcome}")
    previous_handle = int(outcome.removeprefix("reload-started:"))
    for _ in range(30):
        await asyncio.sleep(0.1)
        state = await _capture_unity_state(bridge)
        if any(
            scene.path == path
            and scene.is_active
            and not scene.is_dirty
            and (previous_handle == 0 or scene.handle != previous_handle)
            for scene in state.scenes
        ):
            return
    raise AssertionError(f"owned play scene did not reload: {path}")


def _global_transition_blockers(
    state: UnityStateSnapshot,
    policy: OwnershipPolicy,
) -> list[str]:
    blockers = []
    for scene in state.scenes:
        if not scene.path:
            blockers.append(f"pathless scene loaded: {scene.identity}")
        elif not policy.owns_scene(scene):
            blockers.append(f"unowned scene loaded: {scene.identity}")
        if scene.is_dirty:
            blockers.append(f"dirty scene loaded: {scene.identity}")
    return blockers


def _is_run_owned_path(path: str) -> bool:
    if not path or "\\" in path:
        return False
    if any(part in {"", ".", ".."} for part in path.split("/")):
        return False
    return path == RUN_OWNED_ROOT or path.startswith(RUN_OWNED_ROOT + "/")


def _stable_scene_structure(
    state: UnityStateSnapshot,
    scene_path: str,
) -> tuple[tuple[str, str, str], ...]:
    return tuple(sorted(
        (obj.global_id, obj.hierarchy_path, obj.name)
        for obj in state.objects
        if obj.scene_path == scene_path and obj.global_id
    ))


async def _restore_play_mode(
    bridge: UnityBridge,
    expected_playing: bool,
) -> None:
    if expected_playing:
        await _enter_play(bridge)
    else:
        await _stop_play(bridge)


async def _restore_time_scale(bridge: UnityBridge, expected: float) -> None:
    expected_text = format(expected, ".17g")
    code = (
        'var expected = System.Single.Parse('
        f'{_cs(expected_text)}, System.Globalization.CultureInfo.InvariantCulture);'
        'UnityEngine.Time.timeScale = expected;'
        'return UnityEngine.Time.timeScale.ToString('
        ' "R", System.Globalization.CultureInfo.InvariantCulture);'
    )
    outcome = await _execute_checked(bridge, code, "restore Time.timeScale")
    if float(outcome) != expected:
        raise AssertionError(
            f"could not restore Time.timeScale to {expected}: {outcome}"
        )


async def _restore_owned_state(
    bridge: UnityBridge,
    before: UnityStateSnapshot,
    policy: OwnershipPolicy,
) -> None:
    after = await _capture_unity_state(bridge)
    plan = build_ownership_plan(before, after, policy)
    # Decided from this first, raw post-test capture and its already-built
    # plan — before any play-mode or time-scale restoration below can
    # reassign `after`/`plan` out from under it.
    must_reset_owned_scene = _needs_owned_scene_reset(policy, plan, after.is_playing)
    errors = list(plan.violations)
    block_global_reset = plan.has_unowned_state

    if plan.play_mode_changed:
        blockers = _global_transition_blockers(after, policy)
        if blockers:
            block_global_reset = True
            errors.append(
                "Play/Edit mode was not restored because global transition is unsafe: "
                + "; ".join(blockers)
            )
        else:
            try:
                await _restore_play_mode(bridge, before.is_playing)
                after = await _capture_unity_state(bridge)
                plan = build_ownership_plan(before, after, policy)
                block_global_reset = block_global_reset or plan.has_unowned_state
            except Exception as exc:
                block_global_reset = True
                errors.append(f"could not restore Play/Edit mode: {exc}")

    if after.time_scale != before.time_scale:
        try:
            await _restore_time_scale(bridge, before.time_scale)
            after = await _capture_unity_state(bridge)
            plan = build_ownership_plan(before, after, policy)
            block_global_reset = block_global_reset or plan.has_unowned_state
        except Exception as exc:
            errors.append(f"could not restore Time.timeScale: {exc}")

    added_scene_paths = {scene.path for scene in plan.owned_added_scenes}

    for obj in plan.owned_added_objects:
        if obj.scene_path in added_scene_paths:
            continue
        try:
            await _destroy_owned_object(bridge, obj)
        except Exception as exc:
            errors.append(str(exc))
    for scene in plan.owned_added_scenes:
        try:
            await _close_owned_scene(bridge, scene.path)
        except Exception as exc:
            errors.append(str(exc))
    for path in plan.owned_added_assets:
        try:
            await _delete_owned_asset(bridge, path)
        except Exception as exc:
            errors.append(str(exc))
    reset_completed = False
    if must_reset_owned_scene and not block_global_reset:
        try:
            await _reset_owned_scene(bridge, policy.reset_scene_path, after.is_playing)
            reset_completed = True
        except Exception as exc:
            errors.append(str(exc))
    elif must_reset_owned_scene:
        errors.append(
            "owned scene reset was blocked because unowned or unsafe scene state exists"
        )

    try:
        final = await _capture_unity_state(bridge)
        remaining = build_ownership_plan(before, final, policy)
        errors.extend(remaining.violations)
        remaining_owned_objects = tuple(
            obj for obj in remaining.owned_added_objects
            if not (
                reset_completed
                and obj.scene_path == policy.reset_scene_path
            )
        )
        if remaining_owned_objects:
            errors.append(
                "owned objects remain after cleanup: "
                + ", ".join(obj.identity for obj in remaining_owned_objects)
            )
        if remaining.owned_added_scenes:
            errors.append(
                "owned scenes remain after cleanup: "
                + ", ".join(scene.identity for scene in remaining.owned_added_scenes)
            )
        if remaining.owned_added_assets:
            errors.append(
                "owned assets remain after cleanup: "
                + ", ".join(remaining.owned_added_assets)
            )
        if remaining.reset_owned_scene and not reset_completed:
            errors.append(f"owned scene was not restored: {policy.reset_scene_path}")
        if reset_completed and _stable_scene_structure(
            before,
            policy.reset_scene_path,
        ) != _stable_scene_structure(final, policy.reset_scene_path):
            errors.append(
                "owned scene stable hierarchy differs after reload: "
                f"{policy.reset_scene_path}"
            )
        if before.is_playing:
            active = next((scene for scene in final.scenes if scene.is_active), None)
            if (
                active is None
                or active.path != policy.reset_scene_path
                or active.is_dirty
            ):
                errors.append(
                    "PlayMode primary scene was not reloaded cleanly: "
                    f"active={active}"
                )
    except Exception as exc:
        errors.append(f"post-cleanup verification failed: {exc}")

    if errors:
        raise AssertionError("Unity state ownership failure:\n- " + "\n- ".join(errors))


def _restore_scene_setup_code(snapshot: UnityStateSnapshot) -> str:
    paths = [scene.path for scene in snapshot.scenes]
    active = next((scene.path for scene in snapshot.scenes if scene.is_active), paths[0])
    chunks = [
        f'var first = UnityEditor.SceneManagement.EditorSceneManager.OpenScene({_cs(paths[0])}, '
        'UnityEditor.SceneManagement.OpenSceneMode.Single);'
    ]
    chunks.extend(
        'UnityEditor.SceneManagement.EditorSceneManager.OpenScene('
        f'{_cs(path)}, UnityEditor.SceneManagement.OpenSceneMode.Additive);'
        for path in paths[1:]
    )
    chunks.append(
        f'var active = UnityEngine.SceneManagement.SceneManager.GetSceneByPath({_cs(active)});'
        'if (!active.IsValid()) return "active-restore-missing";'
        'var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();'
        'if (current.handle != active.handle &&'
        ' !UnityEngine.SceneManagement.SceneManager.SetActiveScene(active))'
        ' return "active-restore-failed"; return "restored";'
    )
    return "".join(chunks)


def _session_restore_blockers(state: UnityStateSnapshot) -> list[str]:
    policy = OwnershipPolicy(
        scene_paths={OWNED_LIVE_SCENE},
        asset_paths={OWNED_LIVE_SCENE},
        allowed_path_root=RUN_OWNED_ROOT,
    )
    blockers = _global_transition_blockers(state, policy)
    active = next((scene for scene in state.scenes if scene.is_active), None)
    if active is None or active.path != OWNED_LIVE_SCENE:
        blockers.append(f"owned live scene is not active: {active}")
    unexpected_assets = sorted(
        path for path in state.assets if not policy.owns_asset_path(path)
    )
    if unexpected_assets:
        blockers.append("unowned run assets exist: " + ", ".join(unexpected_assets))
    return blockers


@pytest_asyncio.fixture(scope="session", autouse=True)
async def _live_suite_lease(_require_unity):
    """Fence one autonomous live suite to one Editor for its full lifetime.

    Loopback owners are reclaimed only after PID/start-time proves the prior
    process is dead. Remote or unverifiable owners remain fenced until their
    release or an Editor restart clears SessionState.
    """
    bridge = make_live_bridge()
    acquired = False
    release_failure = None
    try:
        await _connect_with_retry(bridge)
        outcome = await _acquire_live_suite_lease(bridge)
        _assert_lease_owned(outcome, "acquire")
        acquired = True
        yield LIVE_LEASE_TOKEN
    finally:
        if acquired:
            try:
                outcome = await _release_live_suite_lease(bridge)
                _assert_lease_owned(outcome, "release")
            except Exception as exc:
                release_failure = exc
        await bridge.close()
        if release_failure is not None:
            pytest.fail(f"live-suite lease release failed: {release_failure}")


@pytest_asyncio.fixture(scope="session", autouse=True)
async def _ensure_gridtest_scene(_live_suite_lease):
    """Run the live suite in an owned copy; never save a user's open scene."""
    bridge = make_live_bridge()
    await _connect_with_retry(bridge)
    _assert_lease_owned(await _renew_live_suite_lease(bridge), "renew")
    original = await _capture_unity_state(bridge)
    unstable = [scene.identity for scene in original.scenes if not scene.path]
    dirty = [scene.identity for scene in original.scenes if scene.is_dirty]
    if original.is_playing or unstable or dirty:
        await bridge.close()
        pytest.fail(
            "live tests require clean, path-backed EditMode scenes; "
            f"playing={original.is_playing}, pathless={unstable}, dirty={dirty}"
        )

    prepare = (
        'if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TestsTemp"))'
        ' UnityEditor.AssetDatabase.CreateFolder("Assets", "TestsTemp");'
        'if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TestsTemp/PythonLive"))'
        ' UnityEditor.AssetDatabase.CreateFolder("Assets/TestsTemp", "PythonLive");'
        f'var runRoot = {_cs(RUN_OWNED_ROOT)};'
        'if (!UnityEditor.AssetDatabase.IsValidFolder(runRoot))'
        f' UnityEditor.AssetDatabase.CreateFolder("Assets/TestsTemp/PythonLive", {_cs(LIVE_RUN_ID)});'
        'if (!UnityEditor.AssetDatabase.IsValidFolder(runRoot)) return "run-root-failed";'
        f'var source = {_cs(GRIDTEST_SCENE)}; var target = {_cs(OWNED_LIVE_SCENE)};'
        'if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(source) == null)'
        ' return "source-missing";'
        'if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(target)))'
        ' return "target-exists";'
        'if (!UnityEditor.AssetDatabase.CopyAsset(source, target)) return "copy-failed";'
        'var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene('
        ' target, UnityEditor.SceneManagement.OpenSceneMode.Single);'
        'return scene.IsValid() ? scene.path : "open-failed";'
    )
    prepared = False
    try:
        outcome = await _execute_checked(bridge, prepare, "prepare owned live scene")
        if outcome != OWNED_LIVE_SCENE:
            pytest.fail(f"could not prepare owned live scene: {outcome}")
        prepared = True
        yield OWNED_LIVE_SCENE
    finally:
        failures = []
        restored = False
        lease_owned = False
        try:
            outcome = await _renew_live_suite_lease(bridge)
            _assert_lease_owned(outcome, "renew")
            lease_owned = True
        except Exception as exc:
            failures.append(f"live-suite lease lost before session cleanup: {exc}")
        if prepared and lease_owned:
            try:
                state = await _capture_unity_state(bridge)
                blockers = _session_restore_blockers(state)
                if state.is_playing and not blockers:
                    await _stop_play(bridge)
                    state = await _capture_unity_state(bridge)
                    blockers = _session_restore_blockers(state)
                if blockers:
                    failures.append(
                        "original scene setup was not restored: " + "; ".join(blockers)
                    )
                else:
                    outcome = await _execute_checked(
                        bridge,
                        _restore_scene_setup_code(original),
                        "restore original scene setup",
                    )
                    if outcome != "restored":
                        failures.append(f"scene setup restore returned {outcome}")
                    else:
                        restored = True
            except Exception as exc:
                failures.append(f"could not restore original scene setup: {exc}")
        if restored:
            for owned_path in (OWNED_LIVE_SCENE, RUN_OWNED_ROOT):
                try:
                    await _delete_owned_asset(bridge, owned_path)
                except Exception as exc:
                    failures.append(str(exc))
        await bridge.close()
        if failures:
            pytest.fail("live session cleanup failed: " + "; ".join(failures))


@pytest_asyncio.fixture(scope="session")
async def _save_scene_before_play(_ensure_gridtest_scene):
    """Prove the owned scene is clean; saving scenes in test setup is forbidden."""
    bridge = make_live_bridge()
    try:
        await _connect_with_retry(bridge)
        state = await _capture_unity_state(bridge)
        active = next((scene for scene in state.scenes if scene.is_active), None)
        if active is None or active.path != OWNED_LIVE_SCENE or active.is_dirty:
            pytest.fail(
                "owned live scene must be active and clean before Play Mode; "
                f"active={active}"
            )
        yield
    finally:
        await bridge.close()


@pytest_asyncio.fixture
async def bridge():
    b = make_live_bridge()
    await _connect_with_retry(b)
    yield b
    await b.close()


@pytest_asyncio.fixture(autouse=True)
async def unity_state_owner(
    bridge,
    _ensure_gridtest_scene,
    _live_suite_lease,
    request,
):
    """Own and verify every live test's Unity mutations.

    The active test-scene copy and reserved additive scene paths are explicit
    ownership boundaries. Teardown uses GlobalObjectId/transient ID and exact
    asset paths, then restores the primary scene from its canonical source and
    reloads it after every test. This rolls back component/property mutations
    without relying on Unity's dirty flag. It never saves or clears the dirty
    flag on a user scene.
    """
    policy = OwnershipPolicy(
        scene_paths={OWNED_LIVE_SCENE},
        asset_paths={OWNED_LIVE_SCENE},
        reset_scene_path=OWNED_LIVE_SCENE,
        allowed_path_root=RUN_OWNED_ROOT,
        allowed_play_mode_target=(
            True
            if {"play_session", "fresh_scene"} & set(request.fixturenames)
            else None
        ),
    )
    _assert_lease_owned(await _renew_live_suite_lease(bridge), "renew")
    initial = await _capture_unity_state(bridge)
    if "ensure_edit_mode" in request.fixturenames and initial.is_playing:
        blockers = _session_restore_blockers(initial)
        if blockers:
            pytest.fail(
                "cannot establish explicit EditMode safely: " + "; ".join(blockers)
            )
        await _stop_play(bridge)
        initial = await _capture_unity_state(bridge)
    blockers = _session_restore_blockers(initial)
    if blockers:
        pytest.fail("unsafe Unity baseline before live test: " + "; ".join(blockers))
    before = initial
    yield policy
    # A test can leave Unity mid-domain-reload (e.g. a sync/recompile it
    # triggered). Settle before the lease renew and state capture below —
    # both are read-only probes that otherwise race a genuine reload.
    await _wait_compile_idle(bridge)
    _assert_lease_owned(await _renew_live_suite_lease(bridge), "renew")
    await _restore_owned_state(bridge, before, policy)


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
        try:
            r = await b.send("editor", {"action": "state"})
            if "playing:False" in r.get("data", ""):
                return
        except Exception:
            pass
    raise RuntimeError("Failed to leave Play Mode within 10s")


def _data(result) -> str:
    if isinstance(result, dict):
        return result.get("data") or result.get("err", "")
    return str(result)


def _ok(result) -> str:
    """Assert result is ok and return data string, stripped of middleware markers."""
    d = result.get("data", "") if isinstance(result, dict) else str(result)
    err = result.get("err", "") if isinstance(result, dict) else ""
    ok = result.get("ok", True) if isinstance(result, dict) else True
    assert ok, f"cmd failed: {err or d}"
    return strip_markers(d)


def _transient_ref(text: str) -> str:
    """Extract a version-independent '#transientObjectId' reference."""
    import re
    m = re.search(r'#(-?\d+)', text)
    assert m, f"No transient object ID in: {text}"
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
        f'"{OWNED_LIVE_SCENE}", '
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
    b = make_live_bridge()
    connected = False
    try:
        await _connect_with_retry(b)
        connected = True
        await _enter_play(b)
        await _clear_console(b)
    except Exception as e:
        cleanup_error = None
        if connected:
            try:
                await _stop_play(b)
            except Exception as exc:
                cleanup_error = exc
        await b.close()
        if cleanup_error is not None:
            pytest.fail(
                "PlayMode setup failed and EditMode could not be restored: "
                f"setup={e}; cleanup={cleanup_error}"
            )
        pytest.fail(f"Could not enter Play Mode: {e}")
    yield
    try:
        await _stop_play(b)
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
    """Local fast-path cleanup; the ownership fixture still verifies identity."""
    code = (
        f'var go = GameObject.Find("{name}"); '
        f'if(go) {{ UnityEngine.Object.DestroyImmediate(go); return "ok"; }} '
        f'return "not found";'
    )
    outcome = await _execute_checked(bridge, code, f"destroy local object {name}")
    if outcome not in {"ok", "not found"}:
        raise AssertionError(f"could not destroy local object {name}: {outcome}")


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
