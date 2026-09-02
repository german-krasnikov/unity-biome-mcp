"""Write per-backend MCP config files and resolve the Python server command.

Used by BackendDef.build_args() in backend_def.py.
All paths are injectable (config_dir params) for testability.
"""
import json
import logging
import os
import shutil
import sys
from pathlib import Path

from .config.merger import _OLD_NAMES, SERVER_NAME, _deep_merge

log = logging.getLogger(__name__)


def resolve_server_cmd() -> tuple[str, list[str]]:
    """Returns (command, args) to launch unity_mcp.server.

    Order: adjacent .venv/bin/python → sys.executable (if in venv) → uvx → python3.
    """
    server_dir = Path(__file__).parent.parent.parent  # server/src/unity_mcp/../../../ = server/
    venv_py = server_dir / ".venv" / "bin" / "python"
    if venv_py.exists():
        return str(venv_py), ["-m", "unity_mcp.server"]

    if sys.base_prefix != sys.prefix:
        return sys.executable, ["-m", "unity_mcp.server"]

    uvx = shutil.which("uvx")
    if uvx:
        return uvx, ["--quiet", "unity-biome-mcp"]

    python = "python3" if sys.platform != "win32" else "python"
    return python, ["-m", "unity_mcp.server"]


def _atomic_write(path: str, content: str) -> None:
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    tmp = path + ".tmp"
    Path(tmp).write_text(content, encoding="utf-8")
    os.replace(tmp, path)


def _read_existing_or_none(path: str) -> dict | None:
    """Returns {} if missing, parsed dict if valid JSON, None if corrupt or undecodable.

    None means "do not touch this file" — a caller must never turn a parse
    failure into a wholesale overwrite (that would silently wipe every other
    entry a user already has in the file).
    """
    if not os.path.exists(path):
        return {}
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None


def write_claude_config(config_dir: str, mcp_port: int) -> str:
    """Writes unity-biome-mcp-config-{port}.json for --mcp-config. Returns absolute path.

    When mcp_port == 0, UNITY_MCP_PORT is omitted so Python uses discovery files instead
    of a baked port — prevents connection failures after Windows port drift.
    """
    cmd, args = resolve_server_cmd()
    entry: dict = {"command": cmd, "args": args}
    if mcp_port:
        entry["env"] = {"UNITY_MCP_PORT": str(mcp_port)}
    config = {"mcpServers": {SERVER_NAME: entry}}
    path = os.path.join(config_dir, f"unity-biome-mcp-config-{mcp_port}.json")
    _atomic_write(path, json.dumps(config))
    return path


def write_kimi_mcp_config(config_dir: str, mcp_port: int) -> bool:
    """Writes mcp.json in config_dir. Merge-safe: preserves non-unity entries.

    Returns False (no write) if existing mcp.json has corrupt JSON — never
    wipes it down to just our entry. Returns True on a successful write.
    """
    os.makedirs(config_dir, exist_ok=True)
    path = os.path.join(config_dir, "mcp.json")
    cmd, args = resolve_server_cmd()

    existing = _read_existing_or_none(path)
    if existing is None:
        log.warning("Corrupt JSON in %s — skipping write to avoid data loss", path)
        return False

    servers = existing.get("mcpServers", {})
    for old in _OLD_NAMES:
        servers.pop(old, None)                # migrate away from prior name(s)
    entry: dict = {"command": cmd, "args": args}
    if mcp_port:
        entry["env"] = {"UNITY_MCP_PORT": str(mcp_port)}
    current = servers.get(SERVER_NAME)
    base = current if isinstance(current, dict) else {}
    servers[SERVER_NAME] = _deep_merge(base, entry)
    existing["mcpServers"] = servers
    _atomic_write(path, json.dumps(existing, indent=2))
    return True


def write_agy_settings(settings_dir: str, mcp_port: int) -> bool:
    """Writes settings.json in settings_dir. Merge-safe: preserves non-unity entries.

    Returns False (no write) if existing settings.json has corrupt JSON — never
    wipes it down to just our entry. Returns True on a successful write.
    """
    os.makedirs(settings_dir, exist_ok=True)
    path = os.path.join(settings_dir, "settings.json")
    cmd, args = resolve_server_cmd()

    existing = _read_existing_or_none(path)
    if existing is None:
        log.warning("Corrupt JSON in %s — skipping write to avoid data loss", path)
        return False

    servers = existing.get("mcpServers", {})
    for old in _OLD_NAMES:
        servers.pop(old, None)                # migrate away from prior name(s)
    entry: dict = {"command": cmd, "args": args, "trust": True}
    if mcp_port:
        entry["env"] = {"UNITY_MCP_PORT": str(mcp_port)}
    current = servers.get(SERVER_NAME)
    base = current if isinstance(current, dict) else {}
    servers[SERVER_NAME] = _deep_merge(base, entry)
    existing["mcpServers"] = servers
    _atomic_write(path, json.dumps(existing, indent=2))
    return True


def write_opencode_config(config_dir: str, mcp_port: int) -> str:
    """Writes opencode-unity-biome-mcp-{port}.json. Returns absolute path."""
    os.makedirs(config_dir, exist_ok=True)
    cmd, args = resolve_server_cmd()
    entry: dict = {"type": "local", "command": [cmd] + args, "enabled": True}
    if mcp_port:
        entry["environment"] = {"UNITY_MCP_PORT": str(mcp_port)}
    config = {"mcp": {SERVER_NAME: entry}}
    path = os.path.join(config_dir, f"opencode-unity-biome-mcp-{mcp_port}.json")
    _atomic_write(path, json.dumps(config, indent=2))
    return path
