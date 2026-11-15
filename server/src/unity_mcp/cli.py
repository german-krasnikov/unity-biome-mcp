"""unity-biome-mcp CLI — thin layer over biome-mcp.

Ships inside the uvx package (unlike install/, which is dev-repo-only), so these
subcommands work even for users who never cloned the repo. Zero-argv invocation
starts the MCP server.
"""
import sys

from unity_mcp.constants import DEFAULT_PORT
from unity_mcp.paths import unity_mcp_dir

_SUBCOMMANDS = ("configure", "doctor", "version", "uninstall")
_LEGACY_SUBCOMMANDS = ("configure", "doctor", "version", "uninstall", "port")

# Module-level cache so _build_cli() is cheap after first call
_cli_instance = None


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
    from biome_mcp.transport.framing import frame_write, frame_read_with_timeout
    frame_write(writer, _build_hello())
    await writer.drain()
    await frame_read_with_timeout(reader, timeout=5.0)


# ─── BiomeCLI factory ────────────────────────────────────────────────────────

def _build_cli():
    """Build a configured BiomeCLI instance (cached).

    Reuses the server's already-registered MCP instance so that calling
    register_all again with noop functions does not corrupt module-level
    _send/_args/_default globals shared by all tool modules.
    """
    global _cli_instance
    if _cli_instance is not None:
        return _cli_instance

    from biome_mcp.cli import AppProfile, BiomeCLI
    from biome_mcp.cli.provider import MCPToolProvider
    from biome_mcp.cli.follow_policy import FollowPolicy
    from unity_mcp import __version__
    # Use the server's already-registered MCP — avoids a second register_all
    # that would overwrite module-level _send/_args/_default in every tool module.
    from unity_mcp.server import mcp as server_mcp

    profile = AppProfile(
        app_name="unity-biome-mcp",
        protocol="tcp",
        default_port=DEFAULT_PORT,
        default_host="127.0.0.1",
        data_dir=unity_mcp_dir(),
        env_aliases=(("UNITY_MCP_PORT", "port"), ("UNITY_MCP_HOST", "host")),
    )

    provider = MCPToolProvider(server_mcp)

    def _serve():
        from unity_mcp.server import main as _server_main
        _server_main()

    def _is_done(result):
        return "state=terminal" in (result.text or "")

    cli = BiomeCLI(
        provider,
        name="unity-biome-mcp",
        version=__version__,
        serve=_serve,
        follow=FollowPolicy(
            is_done=_is_done,
            interval_bounds=(3.0, 15.0),
            max_retries=200,
            poll_timeout=30.0,
        ),
    )

    cli.add("configure", lambda rest: _cmd_configure(rest))
    cli.add("doctor", lambda rest: _cmd_doctor(rest))
    cli.add("version", lambda rest: _cmd_version(rest))
    cli.add("uninstall", lambda rest: _cmd_uninstall(rest))
    cli.add("port", lambda rest: _cmd_port(rest))

    _cli_instance = cli
    return cli


# ─── Legacy subcommand handlers ──────────────────────────────────────────────

def _cmd_port(argv):
    from biome_mcp.cli import AppProfile
    from biome_mcp.cli.resolver import ConnectionResolver
    profile = AppProfile(
        app_name="unity-biome-mcp",
        default_port=DEFAULT_PORT,
        default_host="127.0.0.1",
        data_dir=unity_mcp_dir(),
        env_aliases=(("UNITY_MCP_PORT", "port"),),
    )
    spec = ConnectionResolver(profile).resolve()
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
    try:
        cli = _build_cli()
        cli.dispatch(argv)  # NoReturn — calls sys.exit
    except SystemExit:
        raise
    except ImportError:
        # biome_mcp.cli not available — fallback to legacy dispatch
        code = dispatch(argv)
        sys.exit(code if code is not None else 0)
