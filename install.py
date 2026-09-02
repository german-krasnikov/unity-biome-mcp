#!/usr/bin/env python3
"""Unity Biome MCP installer. stdlib only, Python 3.14+."""
import sys
if sys.version_info < (3, 14):
    sys.exit(f"Python 3.14+ required, got {sys.version.split()[0]}\n  Download: https://python.org/downloads/")
import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).parent.resolve()
SERVER_DIR = REPO_ROOT / "server"
CODEX_DIR = REPO_ROOT / ".codex"
CODEX_CONFIG = CODEX_DIR / "config.toml"
_UNITY_MCP_DATA_DIR = Path.home() / ".unity-biome-mcp"

# ── package imports ───────────────────────────────────────────────────────────
sys.path.insert(0, str(REPO_ROOT))
from install import ui  # noqa: E402
from install.ui import prompt_yn  # noqa: E402
import install.commands as _cmds  # noqa: E402

# ── lazy server-package imports (patchable at module level) ───────────────────

def _add_server_to_path() -> None:
    src = str(SERVER_DIR / "src")
    if src not in sys.path:
        sys.path.insert(0, src)


try:
    _add_server_to_path()
    from unity_mcp.config.clients import CLIENT_REGISTRY, detect_installed
    from unity_mcp.config.merger import (
        merge_mcp_config, merge_toml_mcp, SERVER_NAME, PROJECT_CONFIG_FILENAME,
        is_entry_pinned, is_toml_pinned, unpin_entry, pin_toml_entry, unpin_toml_entry,
    )
    from unity_mcp.config.backup import backup
    from unity_mcp.config.resolver import build_server_entry, GIT_INSTALL_URL
    from unity_mcp.config.validator import validate_config
except ImportError:
    CLIENT_REGISTRY = {}  # type: ignore[assignment]
    detect_installed = lambda: []  # type: ignore[assignment]
    merge_mcp_config = None  # type: ignore[assignment]
    merge_toml_mcp = None  # type: ignore[assignment]
    SERVER_NAME = "unity-biome-mcp"  # type: ignore[assignment]
    PROJECT_CONFIG_FILENAME = ".mcp.json"  # type: ignore[assignment]
    is_entry_pinned = lambda *a, **kw: False  # type: ignore[assignment]
    is_toml_pinned = lambda *a, **kw: False  # type: ignore[assignment]
    unpin_entry = lambda *a, **kw: False  # type: ignore[assignment]
    pin_toml_entry = lambda *a, **kw: None  # type: ignore[assignment]
    unpin_toml_entry = lambda *a, **kw: False  # type: ignore[assignment]
    backup = None  # type: ignore[assignment]
    build_server_entry = lambda port=0: {}  # type: ignore[assignment]
    GIT_INSTALL_URL = "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server"
    validate_config = lambda key: "not configured"  # type: ignore[assignment]

# MCP_JSON depends on PROJECT_CONFIG_FILENAME above (single literal source,
# C1 #2 follow-up) so its assignment must come after the import/fallback block.
MCP_JSON = REPO_ROOT / PROJECT_CONFIG_FILENAME


# ── thin wrappers (read module globals at call time so tests can patch) ───────

def _venv_python() -> Path:
    return _cmds.venv_python(SERVER_DIR)


def _venv_stale() -> bool:
    return _cmds.venv_stale(SERVER_DIR)


def _setup_env(force_recreate: bool = False) -> None:
    _cmds.setup_env(SERVER_DIR, CODEX_DIR, CODEX_CONFIG, ui, force_recreate=force_recreate)


def _has_uvx() -> bool:
    return shutil.which("uvx") is not None


def _reinstall_uvx() -> None:
    subprocess.run(["uvx", "--reinstall", "--from", GIT_INSTALL_URL, "unity-biome-mcp"], check=True)


# C1-FIX-01 (config-writers MAJOR): one client's unreadable config must never
# abort reconfiguration of the rest -- caught at both the pinned-check call
# (install.py:97-98, previously unprotected) and the merge call below it.
# UnicodeDecodeError/json.JSONDecodeError are both ValueError subclasses;
# listed explicitly to document exactly which real-world corruption this
# guards (Windows stray BOM/binary, corrupt JSON, unwritable/locked file).
_CLIENT_CONFIG_ERRORS = (json.JSONDecodeError, UnicodeDecodeError, ValueError, OSError)


def _reconfigure_detected_clients() -> None:
    """Re-assert MCP entry for AI tools already configured. Never adds a new tool,
    never prompts — matches cmd_update's existing non-interactive contract.
    Skips any entry pinned via `install.py version --set` (ARC-0b). A single
    client with an undecodable/corrupt config is skipped, not fatal -- every
    other detected client still gets reconfigured."""
    if merge_mcp_config is None:
        return
    entry = build_server_entry()
    updated = 0
    skipped = 0
    for key in detect_installed():
        client = CLIENT_REGISTRY[key]
        if client.stdout_only:
            continue
        report = validate_config(key)
        if "not configured" in report or "not found" in report:
            continue
        try:
            pinned = (is_toml_pinned(client.config_path) if client.is_toml
                      else is_entry_pinned(client.config_path, root_key=client.root_key))
        except _CLIENT_CONFIG_ERRORS as exc:
            ui.fail(f"Skipped {client.name}: {exc}")
            skipped += 1
            continue
        if pinned:
            ui.info(f"Skipped {client.name} (pinned)")
            skipped += 1
            continue
        try:
            backup(client.config_path)
            if client.is_toml:
                merge_toml_mcp(client.config_path, entry)
            else:
                merge_mcp_config(client.config_path, entry, root_key=client.root_key,
                                  entry_transformer=client.entry_transformer)
        except _CLIENT_CONFIG_ERRORS as e:
            ui.fail(f"Skipped {client.name}: {e}")
            skipped += 1
            continue
        ui.ok(f"Reconfigured {client.name}")
        updated += 1
    if updated or skipped:
        ui.info(f"Reconfigure summary: {updated} updated, {skipped} skipped")


# ── subcommands ───────────────────────────────────────────────────────────────

def cmd_setup(_args: argparse.Namespace) -> None:
    ui.box(["Unity Biome MCP setup"])
    _setup_env()
    _write_mcp_json()
    ui.ok("Done. Run 'python install.py doctor' to verify.")


def _write_mcp_json() -> None:
    """Auto-generate .mcp.json pointing at the local venv."""
    from unity_mcp.mcp_config_writer import resolve_server_cmd
    from unity_mcp.config.merger import merge_mcp_config
    cmd, args = resolve_server_cmd()
    merge_mcp_config(MCP_JSON, {"command": cmd, "args": args})
    ui.ok(f".mcp.json written → {MCP_JSON}")


def cmd_update(_args: argparse.Namespace, _stop_fn=None) -> None:
    ui.box(["Unity Biome MCP update"])

    # Stop the running server BEFORE swapping files (avoids file-lock / port-hold).
    # Explicit --port required for safety: we NEVER wildcard-kill all servers.
    port = getattr(_args, "port", 0) or 0
    if port:
        ui.info(f"Stopping server on port {port} before update ...")
        stop = _stop_fn or _load_stop_server()
        if stop is not None:
            try:
                stopped = stop(port=port, timeout=15.0)
                if stopped:
                    ui.ok("Server stopped.")
                else:
                    ui.info("No running server found on that port — proceeding.")
            except Exception as e:
                ui.info(f"Could not stop server: {e} — proceeding with update anyway.")
        else:
            ui.info("server_control not available — skipping stop (server not installed yet).")
    else:
        ui.info("No --port given; skipping server stop. Pass --port PORT to stop before update.")

    if _has_uvx():
        ui.info("uvx found — reinstalling from GitHub (invalidates cache) ...")
        try:
            _reinstall_uvx()
            ui.ok("uvx cache refreshed.")
        except subprocess.CalledProcessError as e:
            ui.fail(f"uvx --reinstall failed: {e}")
            sys.exit(1)
    else:
        stale = _venv_stale()
        if stale:
            ui.info("Stale venv detected (folder moved).")
        _setup_env(force_recreate=stale)

    _reconfigure_detected_clients()
    _write_mcp_json()
    ui.ok("Done. To reconnect: run /mcp in your Claude session.")


def cmd_doctor(_args: argparse.Namespace) -> None:
    _cmds.cmd_doctor(SERVER_DIR, CODEX_CONFIG, MCP_JSON, ui, _args)


def cmd_configure(args: argparse.Namespace) -> None:
    """Configure AI tools (--tool) or write project-scoped config (--project-dir)."""
    if merge_mcp_config is None:
        ui.fail("unity_mcp not installed. Run: python install.py setup")
        sys.exit(1)

    entry = build_server_entry(port=getattr(args, "port", 0))
    tool_key = getattr(args, "tool", None)

    project_dir = getattr(args, "project_dir", None) or getattr(args, "project", None)
    if project_dir:
        project = Path(project_dir).resolve()
        if not project.is_dir():
            sys.exit(f"Directory not found: {project}")
        target = _project_config_path(project, tool_key or "claude-code")
        target.parent.mkdir(parents=True, exist_ok=True)
        backup(target)
        client = CLIENT_REGISTRY.get(tool_key or "claude-code", CLIENT_REGISTRY["claude-code"])
        if client.is_toml:
            merge_toml_mcp(target, entry)
        else:
            merge_mcp_config(target, entry, root_key=client.root_key, entry_transformer=client.entry_transformer)
        ui.ok(f"{client.name} configured at {target}")
        return

    if getattr(args, "all", False):
        tools = detect_installed()
        ui.info(f"Detected: {', '.join(tools) or 'none'}")
    elif tool_key:
        tools = [tool_key]
    else:
        installed = detect_installed()
        ui.info(f"Detected: {', '.join(installed) or 'none'}")
        tools = [t for t in installed if prompt_yn(f"Configure {t}?")]

    for tool in tools:
        client = CLIENT_REGISTRY[tool]
        if client.stdout_only:
            print(json.dumps({"mcpServers": {SERVER_NAME: entry}}, indent=2, ensure_ascii=False))
            continue
        backup(client.config_path)
        if client.is_toml:
            merge_toml_mcp(client.config_path, entry)
        else:
            merge_mcp_config(
                client.config_path, entry,
                root_key=client.root_key,
                entry_transformer=client.entry_transformer,
            )
        ui.ok(f"{client.name} configured at {client.config_path}")


def _load_stop_server():
    """Import stop_server from unity_mcp. Returns None if not installed yet."""
    try:
        _add_server_to_path()
        from unity_mcp.server_control import stop_server
        return stop_server
    except ImportError:
        return None


def cmd_stop(_args: argparse.Namespace) -> None:
    stop = _load_stop_server()
    if stop is None:
        ui.fail("unity_mcp not installed. Run: python install.py setup")
        sys.exit(1)
    ui.info(f"Stopping server on port {_args.port} ...")
    stopped = stop(port=_args.port, timeout=getattr(_args, "timeout", 10.0))
    if stopped:
        ui.ok(f"Server on port {_args.port} stopped.")
    else:
        ui.info(f"No running server found on port {_args.port} (or already stopped).")


def _project_config_path(project: Path, tool_key: str) -> Path:
    """Return per-tool project-scoped config path."""
    paths = {
        "claude-code": project / PROJECT_CONFIG_FILENAME,
        "cursor": project / ".cursor" / "mcp.json",
        "vscode": project / ".vscode" / "mcp.json",
        "junie": project / ".junie" / "mcp" / "mcp.json",
    }
    return paths.get(tool_key, project / PROJECT_CONFIG_FILENAME)


_CHANGELOG_HEADING = re.compile(
    r'^## \[v?([\d.]+)(?:[^\]]+)?\]\s*(?:[—-]\s*([\d-]+))?', re.MULTILINE
)
_SEMVER_RE_INSTALL = re.compile(r"^\d+\.\d+\.\d+$")
_CHANGELOG_PATH = REPO_ROOT / "CHANGELOG.md"


def _version_list_offline(changelog_path: Path) -> list[tuple[str, str]]:
    """Return [(version, date), ...] newest-first, excluding Unreleased."""
    text = changelog_path.read_text(encoding="utf-8")
    result = []
    for m in _CHANGELOG_HEADING.finditer(text):
        ver = m.group(1).lstrip("v")
        if "unreleased" in ver.lower():
            continue
        date = m.group(2) or ""
        result.append((ver, date))
    return result


def build_server_entry_for_ref(version: str) -> dict:
    """Build MCP server entry with pinned @vX.Y.Z URL."""
    from unity_mcp.config.resolver import server_git_url
    url = server_git_url(ref=version)
    return {"command": "uvx", "args": ["--from", url, "unity-biome-mcp"]}


def _plugin_upm_url(version: str) -> str:
    return f"https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin#v{version}"


def _target_clients(tool_key: str | None) -> list[str]:
    """Client keys to operate on for `version --set`/`--unpin` without --tool.

    C1 round1 #2 (MAJOR config-writers): must be the already-detected/configured
    clients (matches _reconfigure_detected_clients), never every registered
    client -- otherwise a single-tool user gets newly-pinned configs written
    for AI tools they never installed."""
    return [tool_key] if tool_key else detect_installed()


def _unpin_configs(tool_key: str | None) -> None:
    """Remove the pin marker from configs (ARC-0b: `install.py version --unpin`)."""
    tools = _target_clients(tool_key)
    for key in tools:
        client = CLIENT_REGISTRY.get(key)
        if client is None or client.stdout_only:
            continue
        try:
            removed = (
                unpin_toml_entry(client.config_path)
                if client.is_toml
                else unpin_entry(client.config_path, root_key=client.root_key)
            )
            if removed:
                ui.ok(f"{key} unpinned")
            else:
                ui.info(f"{key}: no pin to remove")
        except Exception as e:
            ui.info(f"Could not unpin {key}: {e}")


def cmd_version(args: argparse.Namespace) -> None:
    """Handle 'version --list', 'version --set X.Y.Z', and 'version --unpin'."""
    if getattr(args, "list", False):
        versions = _version_list_offline(_CHANGELOG_PATH)
        ui.info("Available versions (from CHANGELOG.md):")
        for ver, date in versions:
            suffix = f"  {date}" if date else ""
            print(f"  v{ver}{suffix}")
        return

    if getattr(args, "unpin", False):
        _unpin_configs(getattr(args, "tool", None))
        return

    set_version = getattr(args, "set_version", None)
    if not set_version:
        ui.fail("Specify --list, --set X.Y.Z, or --unpin")
        sys.exit(1)

    if not _SEMVER_RE_INSTALL.match(set_version):
        ui.fail(f"Invalid semver: {set_version!r} — expected X.Y.Z")
        sys.exit(1)

    if getattr(args, "force_print_plugin_url", False):
        print(_plugin_upm_url(set_version))
        return

    # Stop server first
    _add_server_to_path()
    from unity_mcp.config.resolver import find_port
    port = getattr(args, "port", 0) or find_port()
    stop = _load_stop_server()
    if stop is not None:
        try:
            stopped = stop(port=port, timeout=10.0)
            if stopped:
                ui.ok(f"Server on port {port} stopped.")
        except Exception as e:
            ui.info(f"Could not stop server: {e} — proceeding.")
    else:
        ui.info("server_control not available — skipping stop.")

    # Build pinned entry
    entry = build_server_entry_for_ref(set_version)
    entry["_pin"] = True

    # Re-pin all (or one) detected configs
    tool_key = getattr(args, "tool", None)
    tools = _target_clients(tool_key)

    for key in tools:
        client = CLIENT_REGISTRY.get(key)
        if client is None:
            continue
        if client.stdout_only:
            continue
        try:
            if backup is not None:
                backup(client.config_path)
            if client.is_toml:
                merge_toml_mcp(client.config_path, entry)
                pin_toml_entry(client.config_path, set_version)
            else:
                merge_mcp_config(
                    client.config_path, entry,
                    root_key=client.root_key,
                    entry_transformer=client.entry_transformer,
                )
            ui.ok(f"{key} repinned to v{set_version}")
        except Exception as e:
            ui.info(f"Could not update {key}: {e}")

    ui.ok(f"Server side pinned to v{set_version}.")
    print(f"\nPlugin re-pin: open Unity → MCP → Updates → Version Picker → select v{set_version} → Roll Back")
    print(f"  OR: python install.py version --set {set_version} --force-print-plugin-url")
    print(f"Plugin UPM URL: {_plugin_upm_url(set_version)}")
    print(f"\nIf the server doesn't start correctly, run: uvx cache clean unity-biome-mcp")


def cmd_pull(_args: argparse.Namespace) -> None:
    ui.box(["Unity Biome MCP — pull latest"])
    code = _cmds.cmd_pull(REPO_ROOT, ui)
    if code != 0:
        sys.exit(code)


def cmd_uninstall(_args: argparse.Namespace) -> None:
    _cmds.cmd_uninstall(SERVER_DIR, _UNITY_MCP_DATA_DIR, ui, prompt_yn, _args)


def cmd_connect(args: argparse.Namespace) -> None:
    sys.exit(_cmds.cmd_connect(args, ui))


def cmd_disconnect(args: argparse.Namespace) -> None:
    sys.exit(_cmds.cmd_disconnect(args, ui))


# ── argparse ──────────────────────────────────────────────────────────────────

def main() -> None:
    p = argparse.ArgumentParser(description="Unity Biome MCP installer")
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("setup", help="First-time setup")

    p_update = sub.add_parser("update", help="Re-sync after folder move")
    p_update.add_argument("--port", type=int, default=0,
                          help="Port of running server to stop before update")

    sub.add_parser("doctor", help="Diagnose installation")

    p_stop = sub.add_parser("stop", help="Stop a running Unity Biome MCP server")
    p_stop.add_argument("--port", type=int, required=True,
                        help="Port of the server to stop (e.g. 9515)")
    p_stop.add_argument("--timeout", type=float, default=10.0,
                        help="Seconds to wait before SIGKILL fallback")

    cfg = sub.add_parser("configure", help="Configure AI tools or write .mcp.json")
    cfg.add_argument("--project", help="Path to Unity project root (legacy alias for --project-dir)")
    cfg.add_argument("--project-dir", help="Unity project root — writes project-scope config")
    cfg.add_argument("--tool", choices=list(CLIENT_REGISTRY) or
                     ["claude-desktop", "claude-code", "cursor", "windsurf", "generic"])
    cfg.add_argument("--all", action="store_true", help="Configure every detected AI tool, no prompts")
    cfg.add_argument("--port", type=int, default=0)

    p_ver = sub.add_parser("version", help="List or pin a specific release version")
    p_ver.add_argument("--list", action="store_true", help="Show available versions from CHANGELOG")
    p_ver.add_argument("--online", action="store_true", help="Fetch tag list from GitHub (requires network)")
    p_ver.add_argument("--set", metavar="X.Y.Z", dest="set_version", help="Pin server + print plugin URL")
    p_ver.add_argument("--tool", choices=list(CLIENT_REGISTRY) or
                       ["claude-desktop", "claude-code", "cursor", "windsurf"],
                       default=None, help="Only re-pin config for this AI tool")
    p_ver.add_argument("--port", type=int, default=0, help="Port of running server to stop first")
    p_ver.add_argument("--force-print-plugin-url", action="store_true",
                       help="Print plugin UPM git URL for the pinned version and exit")
    p_ver.add_argument("--unpin", action="store_true",
                       help="Remove the pin so the next update re-syncs to the live version")

    sub.add_parser("pull", help="Pull latest code (git clone installs only)")
    sub.add_parser("uninstall", help="Remove Unity Biome MCP")

    p_connect = sub.add_parser("connect", help="Connect Unity Biome MCP to a Unity project")
    p_connect.add_argument("unity_project", help="Path to Unity project root")

    p_disconnect = sub.add_parser("disconnect", help="Disconnect Unity Biome MCP from a Unity project")
    p_disconnect.add_argument("unity_project", help="Path to Unity project root")

    args = p.parse_args()
    dispatch = {
        "setup": cmd_setup, "update": cmd_update, "doctor": cmd_doctor,
        "configure": cmd_configure, "pull": cmd_pull, "uninstall": cmd_uninstall,
        "connect": cmd_connect, "disconnect": cmd_disconnect, "stop": cmd_stop,
        "version": cmd_version,
    }
    dispatch[args.cmd](args)


if __name__ == "__main__":
    main()
