"""unity-biome-mcp CLI — thin layer over biome-mcp.

Ships inside the uvx package (unlike install/, which is dev-repo-only), so
subcommands work even for users who never cloned the repo. Zero-argv invocation
starts the MCP server.
"""
import sys

from unity_mcp.constants import DEFAULT_PORT
from unity_mcp.paths import unity_mcp_dir

_SUBCOMMANDS = ("configure", "doctor", "version", "uninstall")

# Module-level cache so _build_cli() is cheap after first call
_cli_instance = None


# ─── Profile factory ─────────────────────────────────────────────────────────

def _make_profile():
    from biome_mcp.cli import AppProfile
    return AppProfile(
        app_name="unity-biome-mcp",
        protocol="tcp",
        default_port=DEFAULT_PORT,
        default_host="127.0.0.1",
        data_dir=unity_mcp_dir(),
        env_aliases=(("UNITY_MCP_PORT", "port"), ("UNITY_MCP_HOST", "host")),
    )


# ─── Handshake helpers ───────────────────────────────────────────────────────

def _build_hello() -> bytes:
    """Build minimal client_hello payload for Unity write access."""
    import json
    import os
    import uuid
    return json.dumps({
        "id": "cli-hello",
        "cmd": "client_hello",
        "role": "cli",
        "sessionId": str(uuid.uuid4()),
        "lockToken": "",
        "bridgePid": os.getpid(),
        "bridgeParentPid": os.getppid() if hasattr(os, "getppid") else 0,
        "agentId": "cli",
        "displayName": "unity-biome-mcp-cli",
        "bridgeVersion": "",
        "cwd": os.getcwd(),
        "startedAtUtc": "",
    }, ensure_ascii=False).encode("utf-8")


async def _on_connect(reader, writer):
    """Send client_hello handshake after TCP connect."""
    from biome_mcp.transport.framing import frame_read_with_timeout, frame_write
    frame_write(writer, _build_hello())
    await writer.drain()
    await frame_read_with_timeout(reader, timeout=5.0)


# ─── FollowPolicy helpers ────────────────────────────────────────────────────

def _is_done(result) -> bool:
    """True when result signals a terminal test-run state.

    Handles both pipe-delimited text (state=terminal) and JSON ({"state": "terminal"}).
    """
    text = result.text or ""
    if "state=terminal" in text:
        return True
    try:
        import json
        return json.loads(text).get("state") == "terminal"
    except (json.JSONDecodeError, AttributeError):
        return False


# ─── Port extraction ─────────────────────────────────────────────────────────

def _extract_port(argv: list[str]) -> tuple[int | None, list[str]]:
    """Extract --port N or --port=N from argv.

    Returns (port_or_None, filtered_argv_without_port_args).
    """
    out: list[str] = []
    port: int | None = None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--port" and i + 1 < len(argv):
            try:
                port = int(argv[i + 1])
                i += 2
                continue
            except ValueError:
                pass
        elif a.startswith("--port="):
            try:
                port = int(a[7:])
                i += 1
                continue
            except ValueError:
                pass
        out.append(a)
        i += 1
    return port, out


# ─── BiomeCLI factory ────────────────────────────────────────────────────────

def _build_cli(cli_port: int | None = None):
    """Build a configured BiomeCLI instance (cached when cli_port is None).

    Reuses the server's already-registered MCP instance so that calling
    register_all again with noop functions does not corrupt module-level
    _send/_args/_default globals shared by all tool modules.
    """
    global _cli_instance
    if _cli_instance is not None and cli_port is None:
        return _cli_instance

    import biome_mcp.transport.persistent as _persistent
    from biome_mcp.cli import BiomeCLI
    from biome_mcp.cli.follow_policy import FollowPolicy
    from biome_mcp.cli.provider import MCPToolProvider
    from biome_mcp.cli.resolver import ConnectionResolver

    from unity_mcp import __version__
    from unity_mcp.server import mcp as server_mcp

    profile = _make_profile()
    spec = ConnectionResolver(profile).resolve(cli_port=cli_port)

    # Wire _on_connect into TcpSession for persistent --follow connections.
    # Idempotent: skip if already patched (prevents double-wrap in test suites).
    if not getattr(_persistent.TcpSession, "_unity_patched", False):
        _orig = _persistent.TcpSession

        class _TcpSessionWithHello(_orig):
            def __init__(self, host, port, **kw):
                kw.setdefault("on_connect", _on_connect)
                super().__init__(host, port, **kw)

        _TcpSessionWithHello._unity_patched = True
        _persistent.TcpSession = _TcpSessionWithHello

    class _UnityProvider(MCPToolProvider):
        """MCPToolProvider that routes call/batch via direct TCP to Unity.

        list_tools/get_tool use the MCP catalog (metadata only).
        call_tool_sync/async bypass the MCP pipeline and send commands
        directly over TCP with client_hello handshake (P4: transport = native).
        """
        def __init__(self, mcp, spec):
            super().__init__(mcp)
            self._spec = spec

        async def _call_async(self, name, args):
            import asyncio
            import json
            import uuid

            from biome_mcp.transport.framing import frame_read_with_timeout, frame_write
            r, w = await asyncio.wait_for(
                asyncio.open_connection(self._spec.host, self._spec.port), timeout=5.0
            )
            try:
                frame_write(w, _build_hello())
                await w.drain()
                await frame_read_with_timeout(r, timeout=5.0)
                msg_id = f"cli-{uuid.uuid4().hex[:8]}"
                payload = json.dumps({"id": msg_id, "cmd": name, "args": args},
                                     ensure_ascii=False).encode("utf-8")
                frame_write(w, payload)
                await w.drain()
                raw = await frame_read_with_timeout(r, timeout=30.0)
                resp = json.loads(raw)
                if not resp.get("ok"):
                    raise RuntimeError(resp.get("err", "unknown error"))
                return resp.get("data", "")
            finally:
                w.close()
                import contextlib
                with contextlib.suppress(Exception):
                    await w.wait_closed()

    provider = _UnityProvider(server_mcp, spec)

    def _serve():
        from unity_mcp.server import main as _server_main
        _server_main()

    cli = BiomeCLI(
        provider,
        name="unity-biome-mcp",
        version=__version__,
        serve=_serve,
        spec=spec,
        follow=FollowPolicy(
            is_done=_is_done,
            interval_bounds=(3.0, 15.0),
            max_retries=200,
            poll_timeout=30.0,
        ),
    )

    cli.add("configure", _cmd_configure)
    cli.add("doctor", _cmd_doctor)
    cli.add("version", _cmd_version)
    cli.add("uninstall", _cmd_uninstall)
    cli.add("port", _cmd_port)

    if cli_port is None:
        _cli_instance = cli
    return cli


# ─── Legacy subcommand handlers ──────────────────────────────────────────────

def _cmd_port(argv):
    from biome_mcp.cli.resolver import ConnectionResolver
    spec = ConnectionResolver(_make_profile()).resolve()
    print(spec.port)
    return 0


def _cmd_configure(argv: list[str]) -> int:
    import argparse

    from .config.backup import backup
    from .config.clients import CLIENT_REGISTRY, detect_installed
    from .config.merger import merge_mcp_config, merge_toml_mcp
    from .config.resolver import build_server_entry

    p = argparse.ArgumentParser(prog="unity-biome-mcp configure", add_help=False)
    p.add_argument("--tool", choices=list(CLIENT_REGISTRY))
    p.add_argument("--port", type=int, default=0)
    args = p.parse_args(argv)

    entry = build_server_entry(port=args.port)
    tools = [args.tool] if args.tool else detect_installed()
    if not tools:
        print("No AI tool configs detected. Pass --tool <name>.", file=sys.stderr)
        return 1
    for key in tools:
        client = CLIENT_REGISTRY[key]
        if client.stdout_only:
            continue
        try:
            backup(client.config_path)
            if client.is_toml:
                merge_toml_mcp(client.config_path, entry)
            else:
                merge_mcp_config(client.config_path, entry, root_key=client.root_key,
                                  entry_transformer=client.entry_transformer)
        except ValueError as e:
            print(f"unity-biome-mcp: {client.name}: {e} (backup written, skipping)", file=sys.stderr)
            continue
        print(f"{client.name} configured at {client.config_path}")
    return 0


def _cmd_doctor(argv: list[str]) -> int:
    import asyncio

    from .doctor import format_report, run_doctor
    fix = "--fix" in argv
    results = asyncio.run(run_doctor(fix=fix))
    print(format_report(results))
    return 0


def _cmd_version(argv: list[str]) -> int:
    from . import __version__
    print(f"unity-biome-mcp {__version__}")
    return 0


def _cmd_uninstall(argv: list[str]) -> int:
    import argparse

    from .config.backup import backup
    from .config.clients import CLIENT_REGISTRY, detect_installed
    from .config.merger import remove_mcp_entry, remove_toml_mcp_entry

    p = argparse.ArgumentParser(prog="unity-biome-mcp uninstall", add_help=False)
    p.add_argument("--tool", choices=list(CLIENT_REGISTRY))
    args = p.parse_args(argv)

    tools = [args.tool] if args.tool else detect_installed()
    removed_any = False
    for key in tools:
        client = CLIENT_REGISTRY[key]
        if client.stdout_only:
            continue
        try:
            backup(client.config_path)
            removed = (remove_toml_mcp_entry(client.config_path) if client.is_toml
                       else remove_mcp_entry(client.config_path, root_key=client.root_key))
        except ValueError as e:
            print(f"unity-biome-mcp: {client.name}: {e} (backup written, skipping)", file=sys.stderr)
            continue
        removed_any = removed_any or removed
        print(f"Removed unity-biome-mcp from {client.config_path}" if removed
              else f"unity-biome-mcp not found in {client.config_path} — skipped")
    if not removed_any:
        print("Nothing to uninstall.")
    return 0


# ─── Backward-compat dispatch (used by existing tests) ───────────────────────

def dispatch(argv: list[str]) -> int | None:
    """None → zero-arg / not our concern → caller starts the MCP server.
    int → exit code, a known subcommand ran (or argv[0] was unrecognized → 1)."""
    if not argv:
        return None
    sub, rest = argv[0], argv[1:]
    if sub in ("-h", "--help"):
        print(f"unity-biome-mcp [{'|'.join(_SUBCOMMANDS)}]")
        return 0
    if sub not in _SUBCOMMANDS:
        print(f"unity-biome-mcp: unknown subcommand {sub!r}. Known: {', '.join(_SUBCOMMANDS)}",
              file=sys.stderr)
        return 1
    handlers = {
        "configure": _cmd_configure, "doctor": _cmd_doctor,
        "version": _cmd_version, "uninstall": _cmd_uninstall,
    }
    return handlers[sub](rest)


# ─── Entry point ─────────────────────────────────────────────────────────────

def main() -> None:
    argv = sys.argv[1:]
    if not argv:
        from unity_mcp.server import main as _server_main
        _server_main()
        return
    cli_port, argv = _extract_port(argv)
    try:
        cli = _build_cli(cli_port=cli_port)
        cli.dispatch(argv)  # NoReturn — calls sys.exit
    except SystemExit:
        raise
    except ImportError:
        # biome_mcp.cli not available — fallback to legacy dispatch
        code = dispatch(argv)
        sys.exit(code if code is not None else 0)
