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
    assert "configure" in cli._registry
    assert "doctor" in cli._registry
    assert "version" in cli._registry
    assert "uninstall" in cli._registry
    assert "port" in cli._registry


def test_build_cli_has_builtin_list_registered():
    from unity_mcp.cli import _build_cli
    cli = _build_cli()
    assert "list" in cli._registry


def test_build_cli_wires_on_connect_into_tcp_session():
    """TcpSession in biome_mcp.transport.persistent must have on_connect wired."""
    import biome_mcp.transport.persistent as _p
    from unity_mcp.cli import _build_cli, _on_connect
    _build_cli()
    # After _build_cli(), TcpSession is subclassed and injects _on_connect by default
    assert getattr(_p.TcpSession, "_unity_patched", False)


def test_build_cli_passes_spec_to_biome_cli():
    """BiomeCLI must receive a ConnectionSpec so --follow uses TCP."""
    from unity_mcp.cli import _build_cli
    cli = _build_cli()
    assert cli._spec is not None
    assert cli._spec.protocol == "tcp"


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

    # Patch at definition site (local imports inside _on_connect)
    with patch("unity_mcp.bridge.socket_io.frame_write", capture_frame_write), \
         patch("unity_mcp.bridge.socket_io.frame_read_with_timeout", fake_frame_read):
        asyncio.run(_on_connect(MagicMock(), mock_writer))

    assert len(written_args) == 1
    _, payload = written_args[0]
    assert isinstance(payload, bytes)
    hello = json.loads(payload.decode("utf-8"))
    assert hello["cmd"] == "client_hello"


# ─── _is_done ────────────────────────────────────────────────────────────────

def test_is_done_detects_pipe_delimited_terminal():
    from unity_mcp.cli import _is_done
    result = MagicMock()
    result.text = "tests-started|request_id=abc|state=terminal|run_id=xyz"
    assert _is_done(result) is True


def test_is_done_detects_json_terminal():
    from unity_mcp.cli import _is_done
    result = MagicMock()
    result.text = json.dumps({"state": "terminal", "run_id": "xyz"})
    assert _is_done(result) is True


def test_is_done_false_for_non_terminal():
    from unity_mcp.cli import _is_done
    result = MagicMock()
    result.text = json.dumps({"state": "running"})
    assert _is_done(result) is False


def test_is_done_false_for_empty():
    from unity_mcp.cli import _is_done
    result = MagicMock()
    result.text = ""
    assert _is_done(result) is False


# ─── _extract_port ───────────────────────────────────────────────────────────

def test_extract_port_separate_flag():
    from unity_mcp.cli import _extract_port
    port, rest = _extract_port(["--port", "9502", "version"])
    assert port == 9502
    assert rest == ["version"]


def test_extract_port_equals_form():
    from unity_mcp.cli import _extract_port
    port, rest = _extract_port(["--port=9503", "list"])
    assert port == 9503
    assert rest == ["list"]


def test_extract_port_absent():
    from unity_mcp.cli import _extract_port
    port, rest = _extract_port(["version"])
    assert port is None
    assert rest == ["version"]


# ─── main() with BiomeCLI ─────────────────────────────────────────────────────

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
    # Both well-known tools must be present in the listing
    assert "batch" in out
    assert "get_hierarchy" in out


def test_main_port_flag_accepted(monkeypatch):
    """--port N must be extracted before dispatch without error."""
    from unity_mcp import cli
    import unity_mcp
    monkeypatch.setattr(unity_mcp, "__version__", "1.0.0")
    monkeypatch.setattr("sys.argv", ["unity-biome-mcp", "--port", "9999", "version"])

    with pytest.raises(SystemExit) as exc_info:
        cli.main()
    assert exc_info.value.code == 0


def test_main_configure_routes_to_cmd_configure(monkeypatch, capsys):
    from unity_mcp import cli
    from unity_mcp.config import clients as c

    monkeypatch.setattr(c, "detect_installed", lambda: [])
    monkeypatch.setattr("sys.argv", ["unity-biome-mcp", "configure"])

    with pytest.raises(SystemExit) as exc_info:
        cli.main()
    # No tools detected → exit 1
    assert exc_info.value.code == 1
    err = capsys.readouterr().err
    assert "--tool" in err
