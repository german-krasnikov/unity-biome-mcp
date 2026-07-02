"""Validate MCP config files for known clients."""
import json
import socket

from unity_mcp.config.clients import CLIENT_REGISTRY
from unity_mcp.constants import DEFAULT_PORT
from unity_mcp.server_filtering import read_unity_port


def _port_reachable(port: int) -> bool:
    """Quick TCP probe — returns True if something is listening."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=1):
            return True
    except OSError:
        return False


def validate_config(client_key: str) -> str:
    """Check config for client. Return plain text report."""
    info = CLIENT_REGISTRY.get(client_key)
    if info is None:
        valid = ", ".join(sorted(CLIENT_REGISTRY))
        return f"Unknown client: {client_key!r}. Valid: {valid}"
    path = info.config_path
    lines = [f"Client: {info.name}", f"Config: {path}"]

    if info.is_toml:
        if path.exists():
            has_entry = "unity-mcp" in path.read_text(encoding="utf-8")
            lines.append(f"Status: {'configured' if has_entry else 'unity-mcp not found in TOML'}")
        else:
            lines.append("Status: file not found")
        return "\n".join(lines)

    if not path.exists():
        lines.append("Status: not found")
        return "\n".join(lines)

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        lines.append(f"Status: invalid JSON ({e})")
        return "\n".join(lines)

    servers = data.get(info.root_key, {})
    if "unity-mcp" not in servers:
        lines.append(f"Status: not configured (unity-mcp missing from {info.root_key!r})")
        return "\n".join(lines)

    entry = servers["unity-mcp"]
    lines.append(f"unity-mcp entry: {entry}")

    port = read_unity_port(skip_probe=True) or DEFAULT_PORT
    reachable = _port_reachable(port)
    lines.append(f"Port {port}: {'reachable' if reachable else 'not reachable (Unity not running?)'}")
    return "\n".join(lines)
