"""Project-aware live tests for the sync protocol."""

import asyncio
import json
import os
import time
from pathlib import Path

import pytest
import pytest_asyncio

from tests.live.conftest import _connect_with_retry, make_live_bridge

# Domain reload can legitimately take up to DOMAIN_RELOAD_EXPIRY_S (90s);
# this file alone needs headroom past the pyproject.toml global 30s
# --timeout, without widening it for the rest of the suite.
pytestmark = [pytest.mark.live, pytest.mark.timeout(120)]


async def _wait_compile_idle(bridge) -> None:
    """Poll compile_status until Unity finishes compiling (up to 60 s)."""
    for _ in range(60):
        try:
            status = _data(await bridge.send("compile_status", {}))
            if "compiling" not in status.lower():
                return
        except (ConnectionError, TimeoutError):
            pass
        await asyncio.sleep(1.0)


@pytest_asyncio.fixture(scope="module", autouse=True)
async def _ensure_compile_idle():
    """Wait for Unity to finish any pending compilation before sync tests."""
    bridge = make_live_bridge()
    try:
        await _connect_with_retry(bridge)
        await _wait_compile_idle(bridge)
    finally:
        await bridge.close()

_EDITOR_PACKAGE_ID = "com.unity-biome-mcp.editor"


def _data(response) -> str:
    if isinstance(response, dict):
        return response.get("data", "") or response.get("err", "")
    return str(response)


def _installed_editor_package(project: Path) -> Path:
    """Resolve the local editor package exactly as the project manifest does."""
    manifest_path = project / "Packages/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies", {})
    reference = dependencies.get(_EDITOR_PACKAGE_ID)
    if not isinstance(reference, str) or not reference.startswith("file:"):
        raise AssertionError(
            f"{_EDITOR_PACKAGE_ID} must be a local file dependency: {reference!r}"
        )

    package_root = Path(reference.removeprefix("file:"))
    if not package_root.is_absolute():
        package_root = manifest_path.parent / package_root
    package_json = package_root.resolve() / "package.json"
    if not package_json.is_file():
        raise AssertionError(f"Installed editor package is missing: {package_json}")
    return package_json


async def test_live_noop_sync_fast(bridge):
    """With no source changes, sync returns an acknowledgement quickly."""
    started = time.monotonic()
    response = await bridge.send("sync", {"resolve": "false"})
    elapsed = time.monotonic() - started
    acknowledgement = _data(response)

    assert "sync_ack" in acknowledgement, f"Expected sync_ack: {response}"
    if "will_compile=false" in acknowledgement:
        assert elapsed < 5.0, f"Fast path took {elapsed:.1f}s"


async def test_live_sync_full_cycle(bridge):
    """Trigger sync and require its exact epoch to converge."""
    response = await bridge.send("sync", {"resolve": "false"})
    acknowledgement = _data(response)
    assert "sync_ack" in acknowledgement, f"Expected sync_ack: {response}"

    if "will_compile=true" in acknowledgement:
        await _wait_compile_idle(bridge)

    parts = {
        part.split("=", 1)[0]: part.split("=", 1)[1]
        for part in acknowledgement.split("|")
        if "=" in part
    }
    epoch = int(parts.get("epoch", "0"))
    deadline = time.monotonic() + 60.0

    while time.monotonic() < deadline:
        status = _data(await bridge.send("sync_status", {}))
        status_parts = {
            part.split("=", 1)[0]: part.split("=", 1)[1]
            for part in status.split("|")
            if "=" in part
        }
        state = status_parts.get("state", "unknown")
        if int(status_parts.get("epoch", "-1")) == epoch and state in {
            "ready",
            "idle",
        }:
            # Settle to compile-idle before returning control to teardown —
            # sync_status can report "ready" a beat before compile_status
            # clears, which otherwise races unity_state_owner's teardown.
            await _wait_compile_idle(bridge)
            return
        if state == "failed":
            pytest.fail(f"Compile failed: {status}")
        await asyncio.sleep(1.0)

    pytest.fail(f"sync_status never converged for epoch={epoch}")


async def test_live_dll_freshness(bridge):
    """After sync, compile diagnostics remain readable."""
    response = await bridge.send("sync", {"resolve": "false"})
    if "will_compile=true" in _data(response):
        await _wait_compile_idle(bridge)
    errors = _data(await bridge.send("get_compile_errors", {}))
    assert isinstance(errors, str)


async def test_live_reconnect_transparent(bridge):
    """sync_status is accessible and well-formed on the pinned worker."""
    status = _data(await bridge.send("sync_status", {}))
    assert "epoch=" in status, f"Expected epoch in status: {status}"
    assert "state=" in status, f"Expected state in status: {status}"


async def test_live_sync_compile_status_after_noop(bridge):
    """compile_status and sync_status remain coherent after a no-op sync."""
    response = await bridge.send("sync", {"resolve": "false"})
    if "will_compile=true" in _data(response):
        await _wait_compile_idle(bridge)
    compile_status = _data(await bridge.send("compile_status", {}))
    sync_status = _data(await bridge.send("sync_status", {}))

    assert compile_status.startswith(("idle", "compiling", "idle-failed"))
    assert "state=" in sync_status


async def test_live_plugin_bump_re_resolve(bridge, tmp_path):
    """Version bump logic is owned by a temp file; live sync stays non-mutating."""
    from unity_mcp.scripts.bump_version import bump_patch

    project = Path(os.environ["UNITY_MCP_PROJECT_PATH"]).resolve()
    installed_package = _installed_editor_package(project)
    source_package = Path(__file__).resolve().parents[3] / "unity-plugin/package.json"
    installed_before = installed_package.read_bytes()
    source_before = source_package.read_bytes()

    package = json.loads(installed_before.decode("utf-8"))
    owned_package = tmp_path / "package.json"
    owned_package.write_text(
        json.dumps(package, indent=2, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    old_version = package["version"]
    new_version = bump_patch(owned_package)
    old_parts = tuple(int(value) for value in old_version.split("."))
    new_parts = tuple(int(value) for value in new_version.split("."))
    assert new_parts == (old_parts[0], old_parts[1], old_parts[2] + 1)

    response = await bridge.send("sync", {"resolve": "false"})
    assert "sync_ack" in _data(response), f"Expected sync_ack: {response}"
    assert installed_package.read_bytes() == installed_before
    assert source_package.read_bytes() == source_before
    if "will_compile=true" in _data(response):
        await _wait_compile_idle(bridge)
