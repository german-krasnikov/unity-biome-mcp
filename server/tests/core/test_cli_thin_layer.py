"""Tests for thin BiomeCLI layer in unity_mcp.cli."""
import sys
import json
import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch, call


# ─── _build_cli ───────────────────────────────────────────────────────────────

def test_build_cli_returns_biome_cli_instance():
    from biome_mcp.cli import BiomeCLI
    from unity_mcp.cli import _build_cli
    cli = _build_cli()
    assert isinstance(cli, BiomeCLI)


def test_build_cli_has_legacy_subcommands_registered():
    from unity_mcp.cli import _build_cli
    cli = _build_cli()
    # configure, doctor, version, uninstall, port all added via cli.add()
    assert "configure" in cli._registry
    assert "doctor" in cli._registry
    assert "version" in cli._registry
    assert "uninstall" in cli._registry
    assert "port" in cli._registry


def test_build_cli_has_builtin_list_registered():
    from unity_mcp.cli import _build_cli
    cli = _build_cli()
    assert "list" in cli._registry


# ─── _cmd_port ────────────────────────────────────────────────────────────────

def test_cmd_port_prints_resolved_port(capsys, monkeypatch):
    from unity_mcp.cli import _cmd_port
    from biome_mcp.cli import ConnectionSpec

    fake_spec = ConnectionSpec(
        host="127.0.0.1", port=9501,
        protocol="tcp", app_name="unity-biome-mcp", source="default",
    )
    with patch("biome_mcp.cli.resolver.ConnectionResolver") as mock_resolver_cls:
        mock_resolver_cls.return_value.resolve.return_value = fake_spec
        rc = _cmd_port([])

    assert rc == 0
    out = capsys.readouterr().out.strip()
    assert out == "9501"


# ─── _build_hello ─────────────────────────────────────────────────────────────

def test_build_hello_returns_valid_json_bytes():
    from unity_mcp.cli import _build_hello
    raw = _build_hello()
    assert isinstance(raw, bytes)
    data = json.loads(raw.decode("utf-8"))
    assert data["cmd"] == "client_hello"
    assert data["role"] == "cli"
    assert "sessionId" in data
    assert "bridgePid" in data


# ─── _on_connect ──────────────────────────────────────────────────────────────

def test_on_connect_sends_framed_hello_and_reads_response():
    """_on_connect writes a framed hello then reads one frame."""
    from unity_mcp.cli import _on_connect

    async def fake_frame_read(reader, timeout=5.0):
        return b"{}"

    async def mock_drain():
        pass

    mock_writer = MagicMock()
    mock_writer.drain = mock_drain
    written_args = []

    def capture_frame_write(writer, payload):
        written_args.append((writer, payload))

    with patch("biome_mcp.transport.framing.frame_write", capture_frame_write), \
         patch("biome_mcp.transport.framing.frame_read_with_timeout", fake_frame_read):
        asyncio.run(_on_connect(MagicMock(), mock_writer))

    assert len(written_args) == 1
    _, payload = written_args[0]
    assert isinstance(payload, bytes)
    hello = json.loads(payload.decode("utf-8"))
    assert hello["cmd"] == "client_hello"


# ─── main() with BiomeCLI ─────────────────────────────────────────────────────

def test_main_zero_argv_starts_server(monkeypatch):
    import unity_mcp.server as srv
    from unity_mcp import cli

    calls = []
    monkeypatch.setattr(srv, "main", lambda: calls.append(True))
    monkeypatch.setattr("sys.argv", ["unity-biome-mcp"])

    cli.main()

    assert calls == [True]


def test_main_version_exits_zero(monkeypatch):
    from unity_mcp import cli
    import unity_mcp
    monkeypatch.setattr(unity_mcp, "__version__", "1.2.3")
    monkeypatch.setattr("sys.argv", ["unity-biome-mcp", "version"])

    with pytest.raises(SystemExit) as exc_info:
        cli.main()
    assert exc_info.value.code == 0


def test_main_list_outputs_tools(monkeypatch, capsys):
    from unity_mcp import cli

    monkeypatch.setattr("sys.argv", ["unity-biome-mcp", "list"])

    with pytest.raises(SystemExit) as exc_info:
        cli.main()
    assert exc_info.value.code == 0
    out = capsys.readouterr().out
    # list subcommand should print tool names
    assert len(out) > 0


def test_main_configure_routes_to_cmd_configure(monkeypatch, capsys):
    from unity_mcp import cli
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger as m

    monkeypatch.setattr(c, "detect_installed", lambda: [])
    monkeypatch.setattr("sys.argv", ["unity-biome-mcp", "configure"])

    with pytest.raises(SystemExit) as exc_info:
        cli.main()
    # No tools detected → exit 1
    assert exc_info.value.code == 1
    err = capsys.readouterr().err
    assert "--tool" in err
