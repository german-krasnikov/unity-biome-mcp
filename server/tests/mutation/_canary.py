"""Owned-canary install/remove + project baseline snapshot for the mutation lane.

Split out of conftest.py to keep fixture wiring short. See
Plans/MUTATION-REGRESSION-MODULE.md section 9 for the harness contract.
Reuses gauntlet.readiness_canary (target_body/FIXTURES) and
gauntlet.fsr_qualification_fixture (mono_meta) verbatim -- no duplication.
"""
import contextlib
import hashlib
import types
import uuid
from pathlib import Path  # noqa: TC003 -- used in runtime-evaluated annotations, not type-checking only

from gauntlet.fsr_qualification_fixture import mono_meta
from gauntlet.readiness_canary import FIXTURES, target_body
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.bridge_result import unwrap_bridge_result
from unity_mcp.timeout_categories import get_timeout

MUTATION_ROOT = "Assets/TestsTemp/MutationLive"
_BASELINE_DIRS = ("Assets", "Packages", "ProjectSettings")

# run_playtest's Edit-mode pre-flight (PlaytestIsolationScope.RefuseIfDirty) refuses to run
# against ANY dirty loaded scene. create_object() dirties the active scene as a normal side
# effect; the canary is throwaway scaffolding that must never be saved or discarded (that
# would destroy it), so the harness clears the transient in-memory dirty flag instead via the
# non-destructive EditorSceneManager.ClearSceneDirtiness (harness-only, execute_code).
# Verified live (2026-09-06): it exists but is internal to UnityEditor.CoreModule
# (IsPublic=False, IsAssembly=True) -- a normal compiled call is CS0117, so this
# invokes it through reflection instead.
CLEAR_SCENE_DIRTINESS_CODE = (
    "var method = typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod("
    "\"ClearSceneDirtiness\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);\n"
    "for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)\n"
    "    method.Invoke(null, new object[] { UnityEngine.SceneManagement.SceneManager.GetSceneAt(i) });\n"
    "return \"cleared\";"
)


def snapshot_project(project: Path) -> dict[str, tuple[int, str]]:
    """sha256+size for every file under Assets/Packages/ProjectSettings."""
    result: dict[str, tuple[int, str]] = {}
    for sub in _BASELINE_DIRS:
        root = project / sub
        if not root.is_dir():
            continue
        for path in root.rglob("*"):
            if path.is_file():
                data = path.read_bytes()
                result[str(path.relative_to(project))] = (len(data), hashlib.sha256(data).hexdigest())
    return result


def diff_snapshot(before: dict, after: dict) -> str:
    """Return "" if identical, else a description of the drift."""
    added = sorted(set(after) - set(before))
    missing = sorted(set(before) - set(after))
    changed = sorted(k for k in (set(before) & set(after)) if before[k] != after[k])
    if not (added or missing or changed):
        return ""
    return f"added={added} missing={missing} changed={changed}"


def install_canary(project: Path) -> dict:
    """Write Target(101)+Probe sources+fresh metas into an owned uid8 dir.

    Returns the exact set of paths remove_canary must later undo, plus the
    owned scene-object name/path (must match render_dsl's ownership regex).
    """
    uid8 = uuid.uuid4().hex[:8]
    rel_dir = f"{MUTATION_ROOT}/{uid8}"
    canary_dir = project / rel_dir
    canary_dir.mkdir(parents=True, exist_ok=False)  # fail closed on collision

    target_rel = f"{rel_dir}/BuildReadinessCanaryTarget.cs"
    probe_rel = f"{rel_dir}/BuildReadinessCanaryProbe.cs"
    (project / target_rel).write_bytes(target_body(101))
    (project / f"{target_rel}.meta").write_text(mono_meta(uuid.uuid4().hex), encoding="utf-8")
    (project / probe_rel).write_bytes((FIXTURES / "BuildReadinessCanaryProbe.cs").read_bytes())
    (project / f"{probe_rel}.meta").write_text(mono_meta(uuid.uuid4().hex), encoding="utf-8")

    # render_dsl() requires this exact literal shape before any instance_id substitution.
    object_name = f"__BiomeReadiness_{uuid.uuid4().hex}"
    return {
        "dir": canary_dir, "rel_dir": rel_dir,
        "target_path": project / target_rel, "target_rel": target_rel,
        "probe_rel": probe_rel,
        "object_name": object_name, "object_path": f"/{object_name}",
    }


def remove_canary(project: Path, info: dict) -> None:
    """Undo exactly what install_canary created: 4 owned files + generated folder metas."""
    for rel in (info["target_rel"], f"{info['target_rel']}.meta",
                info["probe_rel"], f"{info['probe_rel']}.meta"):
        path = project / rel
        if path.exists():
            path.unlink()

    dir_meta = project / f"{info['rel_dir']}.meta"  # Unity-generated for the uid8 folder
    if dir_meta.exists():
        dir_meta.unlink()
    # not empty (unexpected leftover) -- leave for the baseline guard to catch
    with contextlib.suppress(OSError):
        info["dir"].rmdir()

    # Only remove the shared MutationLive parent (+ its generated .meta) once it's empty --
    # never touch it if another owned uid8 dir is still present under it.
    root = project / MUTATION_ROOT
    root_meta = project / f"{MUTATION_ROOT}.meta"
    with contextlib.suppress(OSError):
        root.rmdir()
        if root_meta.exists():
            root_meta.unlink()


def make_bridge(host: str, port: int, project):
    """UnityBridge matching server.py:561's production retry-safety wiring: without
    is_retry_safe, EVERY command is "unsafe to resend" after a SENT-but-uncertain
    delivery (e.g. a reload-adjacent connection drop) -- even harmless reads like
    sync_status, raising UncertainDeliveryError instead of transparently reconnecting."""
    from unity_mcp.bridge import UnityBridge
    from unity_mcp.tools._annotations import _INTERNAL_RETRY_SAFE_CMDS
    return UnityBridge(host, port=port, expected_project_path=project,
                       is_retry_safe=lambda cmd: cmd in _INTERNAL_RETRY_SAFE_CMDS)


def make_raw_send(bridge):
    """Production-style send: unwrap the wire result, raise ToolError on ok:false.
    Mirrors server/tests/live/conftest.py::sdk_runtime's _send_raw_like."""
    async def _send_raw_like(cmd, args, timeout=0):
        result = await bridge.send(cmd, args, timeout=timeout or get_timeout(cmd))
        text, ok = unwrap_bridge_result(result)
        if not ok:
            raise ToolError(text)
        return text
    return _send_raw_like


def sdk_args(**kwargs) -> dict:
    return {k: v for k, v in kwargs.items() if v is not None}


def build_mutation_sdk(bridge, middleware, *, sync, diagnose, runtime, objects, codegen):
    """Bundle the bound tool functions behind one plain namespace (no class-body
    self-name shadowing risk -- SimpleNamespace attributes aren't descriptor-bound)."""
    return types.SimpleNamespace(
        sync_unity=sync.sync_unity, diagnose=diagnose.diagnose,
        run_playtest=runtime.run_playtest, create_object=objects.create_object,
        manage_component=objects.manage_component, set_property=objects.set_property,
        delete_object=objects.delete_object, execute_code=codegen.execute_code,
        bridge=bridge, middleware=middleware,
    )
