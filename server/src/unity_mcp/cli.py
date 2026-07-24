"""unity-biome-mcp CLI surface: configure/doctor/version/uninstall + MCP server fallthrough.

Ships inside the uvx package (unlike install/, which is dev-repo-only), so these
subcommands work even for users who never cloned the repo. Every real MCP-client
spawn invokes `unity-biome-mcp` with zero argv — argv is non-empty only when a human
typed a subcommand at a terminal.
"""
import sys
from typing import Optional

_SUBCOMMANDS = ("configure", "doctor", "version", "uninstall")


def dispatch(argv: list[str]) -> Optional[int]:
    """None -> zero-arg / not our concern -> caller starts the MCP server.
    int -> exit code, a known subcommand ran (or argv[0] was unrecognized -> 1)."""
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


def _cmd_configure(argv: list[str]) -> int:
    import argparse
    from .config.clients import CLIENT_REGISTRY, detect_installed
    from .config.merger import merge_mcp_config, merge_toml_mcp
    from .config.backup import backup
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
    from .doctor import run_doctor, format_report
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
    from .config.clients import CLIENT_REGISTRY, detect_installed
    from .config.merger import remove_mcp_entry, remove_toml_mcp_entry
    from .config.backup import backup

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


def main() -> None:
    code = dispatch(sys.argv[1:])
    if code is not None:
        sys.exit(code)
    from unity_mcp.server import main as _server_main
    _server_main()
