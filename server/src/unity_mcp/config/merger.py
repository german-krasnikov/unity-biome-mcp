"""Patch only <root_key>[SERVER_NAME] in an existing config file."""
import json
import os
import pathlib
import re
import shutil
from typing import Callable, Optional

# Our MCP server name (config key). "unity-biome-mcp" — one "mcp", and distinct from
# the foreign bare [mcp_servers.unity] (CoplayDev) that the TOML strip removes.
SERVER_NAME = "unity-biome-mcp"
# Previous names we shipped — migrated away from (removed) on every write so a
# rename never leaves an orphaned duplicate server behind.
_OLD_NAMES = ("unity-mcp",)

# Matches OUR section [mcp_servers.unity-biome-mcp] AND old [mcp_servers.unity-mcp],
# plus any dotted sub-sections (e.g. .env).
# Shared by merge_toml_mcp (replace → migrates old to new) and remove_toml_mcp_entry
# (delete). Does NOT match the foreign bare [mcp_servers.unity] — that has its own strip.
_UNITY_MCP_SECTION_RE = re.compile(
    r'\[mcp_servers\.unity-(?:mcp|biome-mcp)\]\n(?:(?!\[)[^\n]*\n)*'
    r'(?:\[mcp_servers\.unity-(?:mcp|biome-mcp)\.[^\]]+\]\n(?:(?!\[)[^\n]*\n)*)*',
    re.MULTILINE,
)


def merge_mcp_config(
    config_path: pathlib.Path,
    server_entry: dict,
    root_key: str = "mcpServers",
    entry_transformer: Optional[Callable[[dict], dict]] = None,
) -> None:
    """Read → parse → patch unity-biome-mcp entry → write. Creates file if missing."""
    if config_path.exists():
        try:
            data = json.loads(config_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            raise ValueError(f"Corrupt JSON in {config_path}: {e}") from e
    else:
        config_path.parent.mkdir(parents=True, exist_ok=True)
        data = {}

    entry = entry_transformer(server_entry) if entry_transformer else server_entry
    data.setdefault(root_key, {})
    for old in _OLD_NAMES:                 # migrate: drop any prior-name entry
        data[root_key].pop(old, None)
    data[root_key][SERVER_NAME] = entry

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    os.replace(str(tmp), str(config_path))


def merge_toml_mcp(config_path: pathlib.Path, server_entry: dict) -> None:
    """Merge unity-biome-mcp into a TOML config (Codex). Text-based, no TOML lib needed."""
    bak = config_path.with_suffix(".bak")
    if config_path.exists():
        if not bak.exists():
            shutil.copy2(config_path, bak)
        text = config_path.read_text(encoding="utf-8").replace("\r\n", "\n")
    else:
        text = ""

    # Strip stale bare [mcp_servers.unity] (no suffix) — causes "invalid transport"
    # Also strips any dotted sub-sections like [mcp_servers.unity.env].
    # \n? at end handles EOF without trailing newline.
    stale_re = re.compile(
        r'\[mcp_servers\.unity\]\n?(?:(?!\[)[^\n]*\n?)*'
        r'(?:\[mcp_servers\.unity\.[^\]]+\]\n?(?:(?!\[)[^\n]*\n?)*)*',
        re.MULTILINE,
    )
    text = stale_re.sub(lambda _: "", text)

    cmd = server_entry["command"]
    args_list = server_entry.get("args", [])
    args_toml = "[" + ", ".join(f"'{a}'" for a in args_list) + "]"
    block = f"[mcp_servers.{SERVER_NAME}]\ncommand = '{cmd}'\nargs = {args_toml}\n"
    env = server_entry.get("env", {})
    if env:
        env_lines = "\n".join(f"{k} = '{v}'" for k, v in env.items())
        block += f'\n[mcp_servers.{SERVER_NAME}.env]\n{env_lines}\n'

    if _UNITY_MCP_SECTION_RE.search(text):
        text = _UNITY_MCP_SECTION_RE.sub(lambda _: block, text)
    else:
        text = text.rstrip() + "\n\n" + block
    config_path.parent.mkdir(parents=True, exist_ok=True)
    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))


def remove_mcp_entry(config_path: pathlib.Path, root_key: str = "mcpServers") -> bool:
    """Delete data[root_key]['unity-biome-mcp'] if present.

    Returns True if it was removed, False if the file/entry didn't exist.
    Raises ValueError on corrupt JSON (same contract as merge_mcp_config).
    """
    if not config_path.exists():
        return False
    try:
        data = json.loads(config_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        raise ValueError(f"Corrupt JSON in {config_path}: {e}") from e

    servers = data.get(root_key, {})
    removed = [n for n in (SERVER_NAME, *_OLD_NAMES) if servers.pop(n, None) is not None]
    if not removed:
        return False

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True


def remove_toml_mcp_entry(config_path: pathlib.Path) -> bool:
    """Strip [mcp_servers.unity-biome-mcp] (+ any dotted sub-sections) from a TOML config.

    Returns True if a section was found and removed, False otherwise.
    """
    if not config_path.exists():
        return False
    text = config_path.read_text(encoding="utf-8").replace("\r\n", "\n")
    new_text, n = _UNITY_MCP_SECTION_RE.subn("", text)
    if n == 0:
        return False

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True
