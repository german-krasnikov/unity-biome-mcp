"""Write per-backend MCP config files and resolve the Python server command.

Used by BackendDef.build_args() in backend_def.py.
All paths are injectable (config_dir params) for testability.
"""
import json
import os
import shutil
import sys
from pathlib import Path

from .config.merger import SERVER_NAME, _OLD_NAMES


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
        return uvx, ["unity-mcp"]

    python = "python3" if sys.platform != "win32" else "python"
    return python, ["-m", "unity_mcp.server"]


def _atomic_write(path: str, content: str) -> None:
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    tmp = path + ".tmp"
    Path(tmp).write_text(content, encoding="utf-8")
    os.replace(tmp, path)


def write_claude_config(config_dir: str, mcp_port: int) -> str:
    """Writes unity-mcp-config-{port}.json for --mcp-config. Returns absolute path."""
    cmd, args = resolve_server_cmd()
    config = {
        "mcpServers": {
            SERVER_NAME: {
                "command": cmd,
                "args": args,
                "env": {"UNITY_MCP_PORT": str(mcp_port), "UNITY_MCP_CHAT": "1"},
            }
        }
    }
    path = os.path.join(config_dir, f"unity-mcp-config-{mcp_port}.json")
    _atomic_write(path, json.dumps(config))
    return path


def write_kimi_mcp_config(config_dir: str, mcp_port: int) -> None:
    """Writes mcp.json in config_dir. Merge-safe: preserves non-unity entries."""
    os.makedirs(config_dir, exist_ok=True)
    path = os.path.join(config_dir, "mcp.json")
    cmd, args = resolve_server_cmd()

    existing: dict = {}
    if os.path.exists(path):
        try:
            existing = json.loads(Path(path).read_text(encoding="utf-8"))
        except Exception:
            existing = {}

    servers = existing.get("mcpServers", {})
    for old in _OLD_NAMES:
        servers.pop(old, None)                # migrate away from prior name(s)
    servers[SERVER_NAME] = {"command": cmd, "args": args,
                             "env": {"UNITY_MCP_PORT": str(mcp_port)}}
    existing["mcpServers"] = servers
    _atomic_write(path, json.dumps(existing, indent=2))


def write_agy_settings(settings_dir: str, mcp_port: int) -> None:
    """Writes settings.json in settings_dir. Merge-safe: preserves non-unity entries."""
    os.makedirs(settings_dir, exist_ok=True)
    path = os.path.join(settings_dir, "settings.json")
    cmd, args = resolve_server_cmd()

    existing: dict = {}
    if os.path.exists(path):
        try:
            existing = json.loads(Path(path).read_text(encoding="utf-8"))
        except Exception:
            existing = {}

    servers = existing.get("mcpServers", {})
    for old in _OLD_NAMES:
        servers.pop(old, None)                # migrate away from prior name(s)
    servers[SERVER_NAME] = {"command": cmd, "args": args,
                             "env": {"UNITY_MCP_PORT": str(mcp_port)},
                             "trust": True}
    existing["mcpServers"] = servers
    _atomic_write(path, json.dumps(existing, indent=2))


def write_opencode_config(config_dir: str, mcp_port: int) -> str:
    """Writes opencode-unity-mcp-{port}.json. Returns absolute path."""
    os.makedirs(config_dir, exist_ok=True)
    cmd, args = resolve_server_cmd()
    config = {
        "mcp": {
            SERVER_NAME: {
                "type": "local",
                "command": [cmd] + args,
                "environment": {"UNITY_MCP_PORT": str(mcp_port)},
                "enabled": True,
            }
        }
    }
    path = os.path.join(config_dir, f"opencode-unity-mcp-{mcp_port}.json")
    _atomic_write(path, json.dumps(config, indent=2))
    return path
