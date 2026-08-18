"""Public console-entry MCP contracts against an independent scripted peer."""


import asyncio
import json
import os
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

from unity_mcp import __version__

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from gauntlet.fake_unity_peer import ScriptedUnityPeer

if TYPE_CHECKING:
    from collections.abc import AsyncIterator

pytestmark = [pytest.mark.asyncio, pytest.mark.timeout(30)]


@asynccontextmanager
async def _public_session(
    tmp_path: Path,
    peer: ScriptedUnityPeer,
) -> AsyncIterator[tuple[ClientSession, types.InitializeResult]]:
    synthetic_root = tmp_path / "client-root"
    synthetic_root.mkdir()
    isolated_home = tmp_path / "home"
    cache_dir = isolated_home / ".unity-biome-mcp"
    cache_dir.mkdir(parents=True)
    (cache_dir / "update_cache.json").write_text(
        json.dumps({"ts": time.time(), "latest": __version__}),
        encoding="utf-8",
    )
    stderr_path = tmp_path / "server.stderr"
    entrypoint = Path(sys.executable).with_name("unity-biome-mcp")
    if os.name == "nt":
        entrypoint = Path(sys.executable).with_name("Scripts") / "unity-biome-mcp.exe"
    assert entrypoint.is_file(), "test must run from the installed server environment"

    environment = {
        "HOME": str(isolated_home),
        "USERPROFILE": str(isolated_home),
        "APPDATA": str(isolated_home / "appdata"),
        "LOCALAPPDATA": str(isolated_home / "localappdata"),
        "TMPDIR": str(tmp_path),
        "TEMP": str(tmp_path),
        "TMP": str(tmp_path),
        "PATH": os.environ.get("PATH", ""),
        "PYTHONNOUSERSITE": "1",
        "PYTHONUTF8": "1",
        "UNITY_MCP_PORT": str(peer.port),
        "UNITY_MCP_PROJECT_PATH": str(peer.project_path.resolve()),
        "UNITY_MCP_PROJECT_DIR": str(peer.project_path.resolve()),
        "UNITY_MCP_TRANSPORT": "stdio",
        "UNITY_MCP_IDLE_TIMEOUT": "0",
        "UNITY_MCP_HINTS": "0",
        "UNITY_MCP_BUDGET": "0",
        "UNITY_MCP_PREFETCH_CACHE": "0",
        "UNITY_MCP_DISTILL": "0",
        "UNITY_MCP_PLUGIN_DIRS": "",
    }
    system_root = os.environ.get("SYSTEMROOT")
    if system_root is not None:
        environment["SYSTEMROOT"] = system_root

    parameters = StdioServerParameters(
        command=str(entrypoint),
        args=[],
        env=environment,
        cwd=synthetic_root,
    )
    try:
        with stderr_path.open("w+", encoding="utf-8") as error_stream:
            async with stdio_client(parameters, errlog=error_stream) as (read, write):
                async with ClientSession(
                    read,
                    write,
                    read_timeout_seconds=timedelta(seconds=10),
                    client_info=types.Implementation(name="contract-gauntlet", version="1"),
                ) as session:
                    initialized = await session.initialize()
                    yield session, initialized
    finally:
        await _wait_for_peer_disconnect(peer)
        locks = list((isolated_home / ".unity-biome-mcp").glob("server-*.lock"))
        assert locks == []
        stderr = stderr_path.read_text(encoding="utf-8").lower()
        assert "traceback" not in stderr
        assert "fatal" not in stderr


async def _wait_for_peer_disconnect(peer: ScriptedUnityPeer) -> None:
    for _ in range(100):
        if peer.active_connections == 0:
            return
        await asyncio.sleep(0.01)
    raise AssertionError("stdio server left a scripted TCP connection open")


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


async def test_public_stdio_identity_schema_and_read_round_trip(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    async with _public_session(tmp_path, scripted_peer) as (session, initialized):
        tools = await session.list_tools()
        names = [tool.name for tool in tools.tools]
        hierarchy = next(tool for tool in tools.tools if tool.name == "get_hierarchy")
        result = await session.call_tool("get_hierarchy", {"depth": 1})

        assert initialized.serverInfo.version == __version__
        assert len(names) == len(set(names))
        assert hierarchy.inputSchema.get("additionalProperties") is False
        assert result.isError is False
        assert "Synthetic" in result.content[0].text
    assert scripted_peer.unexpected_commands == []


async def test_public_stdio_unknown_argument_is_rejected_before_tcp_dispatch(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    async with _public_session(tmp_path, scripted_peer) as (session, _):
        before = scripted_peer.count("get_hierarchy")
        result = await session.call_tool("get_hierarchy", {"bogus": 1})

        assert result.isError is True
        assert scripted_peer.count("get_hierarchy") == before


async def test_public_stdio_missing_required_argument_is_rejected_before_tcp(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    async with _public_session(tmp_path, scripted_peer) as (session, _):
        before = scripted_peer.count("scene")
        result = await session.call_tool("scene", {})

        assert result.isError is True
        assert scripted_peer.count("scene") == before


async def test_public_stdio_preserves_unity_error_envelope(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    scripted_peer.set_response(
        "get_hierarchy",
        ok=False,
        error="synthetic hierarchy failure",
    )
    async with _public_session(tmp_path, scripted_peer) as (session, _):
        result = await session.call_tool("get_hierarchy", {"depth": 1})

        assert result.isError is True
        assert "synthetic hierarchy failure" in result.content[0].text


async def test_public_stdio_rejects_foreign_project_identity(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    foreign_project = tmp_path / "ForeignUnityProject"
    foreign_project.mkdir()
    scripted_peer.reported_project_path = foreign_project

    async with _public_session(tmp_path, scripted_peer) as (session, _):
        result = await session.call_tool("get_hierarchy", {"depth": 1})

        assert result.isError is True
        assert "unity_unavailable" in result.content[0].text.lower()
    assert scripted_peer.count("get_hierarchy") == 0


async def test_public_stdio_propagates_client_label_after_tool_discovery(
    tmp_path: Path,
    scripted_peer: ScriptedUnityPeer,
) -> None:
    async with _public_session(tmp_path, scripted_peer) as (session, _):
        await session.list_tools()
        for _ in range(100):
            if scripted_peer.count("set_client_label") == 1:
                break
            await asyncio.sleep(0.01)

        labels = [
            request
            for request in scripted_peer.transcript
            if request.get("cmd") == "set_client_label"
        ]
        assert len(labels) == 1
        assert labels[0].get("args") == {"label": "contract-gauntlet"}
