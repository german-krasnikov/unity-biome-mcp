"""Installed Python runtime contract for public MCP stdio."""

from __future__ import annotations

import asyncio
import hashlib
import json
import shutil
import subprocess
import sys
import time
from contextlib import asynccontextmanager
from datetime import timedelta
from pathlib import Path
from typing import TYPE_CHECKING

import pytest
import pytest_asyncio
from mcp import ClientSession, types
from mcp.client.stdio import StdioServerParameters, stdio_client

TESTS = Path(__file__).resolve().parent
REPO = TESTS.parent.parent
sys.path.insert(0, str(TESTS.parent))

from gauntlet import installed_runtime  # noqa: E402
from gauntlet.fake_unity_peer import ScriptedUnityPeer  # noqa: E402
from gauntlet.installed_runtime import (  # noqa: E402
    InstalledRuntimeReceipt,
    RuntimeInstallError,
    install_python_wheel_runtime,
    public_stdio_environment,
)

if TYPE_CHECKING:
    from collections.abc import AsyncIterator

PRODUCT_VERSION = "1.26.0"


@pytest.fixture
def built_wheel(tmp_path: Path) -> Path:
    output = tmp_path / "dist"
    uv = shutil.which("uv")
    if uv:
        command = (uv, "build", "--wheel", "--out-dir", str(output), "server")
    else:
        command = (sys.executable, "-m", "pip", "wheel", "--no-deps", "--wheel-dir", str(output), "server")
    subprocess.run(command, cwd=REPO, check=True, text=True, encoding="utf-8", capture_output=True, timeout=60)
    return next(output.glob("unity_biome_mcp-*.whl"))


def test_runtime_install_rejects_wrong_wheel_digest(
    tmp_path: Path,
    built_wheel: Path,
) -> None:
    with pytest.raises(RuntimeInstallError, match="digest"):
        install_python_wheel_runtime(
            built_wheel,
            tmp_path / "runtime",
            expected_sha256="0" * 64,
            product_version=PRODUCT_VERSION,
        )


def test_runtime_install_uses_pip_fallback_when_uv_is_unavailable(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    wheel = tmp_path / "unity_biome_mcp-1.26.0-py3-none-any.whl"
    wheel.write_bytes(b"wheel")
    expected = hashlib.sha256(wheel.read_bytes()).hexdigest()
    commands: list[tuple[str, ...]] = []

    def fake_run(command: tuple[str, ...], cwd: Path) -> None:
        commands.append(command)
        if command[1:3] == ("-m", "venv"):
            executable = Path(command[-1]) / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")
            executable.parent.mkdir(parents=True)
            executable.write_text("", encoding="utf-8")

    def fake_probe(
        python: Path,
        runtime_root: Path,
        *,
        expected_sha256: str,
        artifact_size: int,
        product_version: str,
    ) -> InstalledRuntimeReceipt:
        return InstalledRuntimeReceipt(
            artifact_sha256=expected_sha256,
            artifact_size_bytes=artifact_size,
            distribution_version=product_version,
            distribution_path=str(runtime_root / "venv"),
            module_path=str(runtime_root / "venv" / "unity_mcp" / "__init__.py"),
            entrypoint_path=str(runtime_root / "venv" / "bin" / "unity-biome-mcp"),
            python_executable=str(python),
            runtime_root=str(runtime_root),
        )

    monkeypatch.setattr(installed_runtime.shutil, "which", lambda name: None)
    monkeypatch.setattr(installed_runtime, "_run", fake_run)
    monkeypatch.setattr(installed_runtime, "_probe_runtime", fake_probe)

    install_python_wheel_runtime(
        wheel,
        tmp_path / "runtime",
        expected_sha256=expected,
        product_version=PRODUCT_VERSION,
        python_executable=sys.executable,
    )

    assert commands[1][1:4] == ("-m", "pip", "install")


def test_runtime_install_receipt_proves_installed_origin(
    tmp_path: Path,
    built_wheel: Path,
) -> None:
    expected = hashlib.sha256(built_wheel.read_bytes()).hexdigest()
    receipt = install_python_wheel_runtime(
        built_wheel,
        tmp_path / "runtime",
        expected_sha256=expected,
        product_version=PRODUCT_VERSION,
    )

    runtime_root = Path(receipt.runtime_root)
    assert receipt.artifact_sha256 == expected
    assert receipt.distribution_version == PRODUCT_VERSION
    assert Path(receipt.module_path).is_relative_to(runtime_root)
    assert Path(receipt.distribution_path).is_relative_to(runtime_root)
    assert Path(receipt.entrypoint_path).is_relative_to(runtime_root)
    assert "server/src" not in receipt.module_path


@pytest_asyncio.fixture
async def scripted_peer(tmp_path: Path) -> AsyncIterator[ScriptedUnityPeer]:
    project = tmp_path / "SyntheticUnityProject"
    (project / "Assets").mkdir(parents=True)
    (project / "Library").mkdir()
    peer = ScriptedUnityPeer(project)
    await peer.start()
    try:
        yield peer
    finally:
        await peer.close()


@asynccontextmanager
async def _installed_session(
    tmp_path: Path,
    built_wheel: Path,
    peer: ScriptedUnityPeer,
) -> AsyncIterator[tuple[ClientSession, types.InitializeResult]]:
    expected = hashlib.sha256(built_wheel.read_bytes()).hexdigest()
    receipt = install_python_wheel_runtime(
        built_wheel,
        tmp_path / "runtime",
        expected_sha256=expected,
        product_version=PRODUCT_VERSION,
    )
    home = tmp_path / "home"
    (home / ".unity-biome-mcp").mkdir(parents=True)
    (home / ".unity-biome-mcp" / "update_cache.json").write_text(
        json.dumps({"ts": time.time(), "latest": PRODUCT_VERSION}),
        encoding="utf-8",
    )
    stderr_path = tmp_path / "server.stderr"
    environment = public_stdio_environment(
        receipt,
        home=home,
        temp=tmp_path,
        port=peer.port,
        project_path=peer.project_path,
    )
    parameters = StdioServerParameters(
        command=receipt.entrypoint_path,
        args=[],
        env=environment,
        cwd=tmp_path,
    )
    try:
        with stderr_path.open("w+", encoding="utf-8") as error_stream:
            async with stdio_client(parameters, errlog=error_stream) as (read, write):
                async with ClientSession(
                    read,
                    write,
                    read_timeout_seconds=timedelta(seconds=10),
                    client_info=types.Implementation(name="installed-runtime", version="1"),
                ) as session:
                    yield session, await session.initialize()
    finally:
        for _ in range(100):
            if peer.active_connections == 0:
                break
            await asyncio.sleep(0.01)
        assert peer.active_connections == 0
        assert not list((home / ".unity-biome-mcp").glob("server-*.lock"))
        stderr = stderr_path.read_text(encoding="utf-8").lower()
        assert "traceback" not in stderr


@pytest.mark.asyncio
@pytest.mark.timeout(90)
async def test_installed_entrypoint_serves_public_stdio(
    tmp_path: Path,
    built_wheel: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    async with _installed_session(tmp_path, built_wheel, scripted_peer) as (session, initialized):
        tools = await session.list_tools()
        result = await session.call_tool("get_hierarchy", {"depth": 1})

    assert initialized.serverInfo.version == PRODUCT_VERSION
    assert any(tool.name == "get_hierarchy" for tool in tools.tools)
    assert result.isError is False
    assert "Synthetic" in result.content[0].text
    assert scripted_peer.unexpected_commands == []
