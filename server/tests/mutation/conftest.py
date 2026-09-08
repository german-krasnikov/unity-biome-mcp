"""Mutation regression lane: opt-in gate + fail-closed worker requirement.

No `unity_state_owner` here -- mutation tests manage their own canary
directory (outside `Assets/TestsTemp/PythonLive/`), use `[BiomeWorkerOnly]`
disposable-worker boundaries, and perform explicit cleanup with 516-baseline
verification. See Plans/MUTATION-REGRESSION-MODULE.md section 9.
"""
import os
import socket
import time
from pathlib import Path

import pytest
import pytest_asyncio

from tests.mutation._canary import (
    CLEAR_SCENE_DIRTINESS_CODE,
    build_mutation_sdk,
    diff_snapshot,
    install_canary,
    make_bridge,
    make_raw_send,
    remove_canary,
    sdk_args,
    snapshot_project,
)
from unity_mcp import editor_log
from unity_mcp.middleware import Middleware, wrap_send
from unity_mcp.tools import asset as asset_tool
from unity_mcp.tools import codegen, diagnose, editor_control, objects, runtime, sync

REAL_PORTS_DIR = Path.home() / ".unity-biome-mcp" / "ports"  # at import, before conftest patches Path.home(); reserved for port-file discovery (A5+)
MUTATION_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
MUTATION_PORT = int(os.environ.get("UNITY_MCP_PORT", "9600"))
MUTATION_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")
MUTATION_LIVE_GATE = "UNITY_MCP_RUN_MUTATION_LIVE"


def pytest_collection_modifyitems(items):
    if os.environ.get(MUTATION_LIVE_GATE) != "1":
        skip = pytest.mark.skip(reason=f"mutation lane; set {MUTATION_LIVE_GATE}=1")
        for item in items:
            if item.get_closest_marker("mutation_live"):
                item.add_marker(skip)


def _bridge_up(timeout: float = 0.2) -> bool:
    try:
        with socket.create_connection((MUTATION_HOST, MUTATION_PORT), timeout=timeout):
            return True
    except OSError:
        return False


@pytest.fixture(scope="session", autouse=True)
def _require_mutation_worker():
    """Require the configured worker; an unavailable mutation gate is a failure."""
    if os.environ.get(MUTATION_LIVE_GATE) != "1":
        return  # gate closed -- items are already skipped, never dial out
    for _ in range(10):
        if _bridge_up():
            return
        time.sleep(1)
    pytest.fail(
        f"verified mutation worker unavailable: host={MUTATION_HOST} "
        f"port={MUTATION_PORT} project={MUTATION_PROJECT!r}"
    )


@pytest.fixture
async def mutation_bridge():
    """Bare bridge factory for mutation regression tests -- no ownership wrapper."""
    bridge = make_bridge(MUTATION_HOST, MUTATION_PORT, MUTATION_PROJECT or None)
    await bridge.connect()
    try:
        yield bridge
    finally:
        await bridge.close()


@pytest.fixture(scope="session", autouse=True)
def project_baseline_guard(_require_mutation_worker):
    """Independent cleanup oracle: byte-identical Assets/Packages/ProjectSettings
    at session end. Mirrors the 516-file baseline check from the live qualification."""
    if os.environ.get(MUTATION_LIVE_GATE) != "1":
        yield
        return
    project = Path(MUTATION_PROJECT).resolve()
    before = snapshot_project(project)
    yield
    after = snapshot_project(project)
    drift = diff_snapshot(before, after)
    if drift:
        pytest.fail(f"mutation lane left project baseline drift ({len(before)} files before): {drift}")


@pytest_asyncio.fixture
async def mutation_sdk(monkeypatch):
    """Production-style send (unwrap + ToolError on ok:false, fresh Middleware()) bound
    to the public sync/diagnose/runtime/objects/codegen tool modules. Mirrors
    server/tests/live/conftest.py::sdk_runtime -- no MCP memory-transport layer."""
    # Middleware's periodic "AUTO STATE" hierarchy injection (every 10th write call,
    # middleware_async.py:35) appends text after a format="json" run_playtest receipt,
    # which _classify_outcome then fails to parse -- a real latent interaction bug,
    # out of scope here (see final report). Use the existing documented opt-out
    # (already covered by test_middleware_distill_integration.py) for deterministic JSON.
    monkeypatch.setenv("UNITY_MCP_AUTO_STATE", "0")
    project = Path(MUTATION_PROJECT).resolve() if MUTATION_PROJECT else None
    bridge = make_bridge(MUTATION_HOST, MUTATION_PORT, project)
    await bridge.connect()

    editor_log.init_corroboration()  # parity with sync.register()
    mw = Middleware()
    wrapped_send = wrap_send(make_raw_send(bridge), mw)
    for module in (sync, diagnose, runtime, objects, codegen, editor_control, asset_tool):
        monkeypatch.setattr(module, "_send", wrapped_send)
        # sync.py/diagnose.py don't pre-declare a module-level _args (they never call it);
        # bind() sets it anyway via globals(), so mirror that here with raising=False.
        monkeypatch.setattr(module, "_args", sdk_args, raising=False)

    try:
        yield build_mutation_sdk(bridge, mw, sync=sync, diagnose=diagnose,
                                  runtime=runtime, objects=objects, codegen=codegen,
                                  editor_control=editor_control, asset=asset_tool)
    finally:
        await bridge.close()


@pytest_asyncio.fixture
async def owned_canary(mutation_sdk):
    """Owned Target+Probe canary, installed/compiled/instantiated, cleaned up in finally.

    No unity_state_owner here -- ownership is the exact 4 files + 1 scene object this
    fixture creates, verified by project_baseline_guard at session end."""
    # Everything from here on must clean up in finally -- an orphaned uid8 dir left behind
    # by a mid-setup failure declares the same global-namespace type twice on the next run
    # (CS0101), wedging the whole worker's compile for every later test.
    project = Path(MUTATION_PROJECT).resolve()
    info = install_canary(project)
    object_created = False
    try:
        result = await mutation_sdk.sync_unity(timeout=120)
        if result != "sync clean":
            raise RuntimeError(f"owned_canary install did not compile clean: {result!r}")
        await mutation_sdk.create_object(name=info["object_name"], components="BuildReadinessCanaryProbe")
        object_created = True
        # Edit-mode run_playtest refuses any dirty loaded scene; never save/discard the
        # canary object away -- just clear the transient flag it set (see comment above).
        await mutation_sdk.execute_code(code=CLEAR_SCENE_DIRTINESS_CODE)
        yield {**info, "instance_id": None}
    finally:
        if object_created:
            await mutation_sdk.delete_object(path=info["object_path"])
            # delete_object dirties the scene again; never save/discard it away --
            # clear the transient flag the same way setup did, so GridTest is not
            # left dirty for other lanes' RefuseIfDirty/_ensure_gridtest_scene checks.
            await mutation_sdk.execute_code(code=CLEAR_SCENE_DIRTINESS_CODE)
        remove_canary(project, info)
        teardown = await mutation_sdk.sync_unity(timeout=120)
        if teardown not in ("sync clean", "sync clean (no compile needed)"):
            raise RuntimeError(f"owned_canary teardown did not compile clean: {teardown!r}")
