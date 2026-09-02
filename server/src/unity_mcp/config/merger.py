"""Patch only <root_key>[SERVER_NAME] in an existing config file."""
import json
import os
import pathlib  # noqa: TC003
import re
import shutil
from collections.abc import Callable  # noqa: TC003

# Our MCP server name (config key). "unity-biome-mcp" — one "mcp", and distinct from
# the foreign bare [mcp_servers.unity] (CoplayDev) that the TOML strip removes.
SERVER_NAME = "unity-biome-mcp"
# Previous names we shipped — migrated away from (removed) on every write so a
# rename never leaves an orphaned duplicate server behind.
_OLD_NAMES = ("unity-mcp",)

# Claude Code project-scoped config filename (root_key="mcpServers"). Canonical
# source for this literal — install.py's _project_config_path (claude-code
# branch) is the other consumer and is slated to import this constant instead
# of repeating the literal.
PROJECT_CONFIG_FILENAME = ".mcp.json"

# C1-FIX-01 (windows-platform CRITICAL): every config READ in this module uses
# utf-8-sig, which transparently strips a leading UTF-8 BOM (Windows Notepad /
# PowerShell 5.1 Out-File/Set-Content default) before json.loads/regex parsing.
# Byte-identical to plain utf-8 decoding when no BOM is present, so this is a
# strict superset — never used for WRITES, which stay plain "utf-8" (no BOM emitted).
_READ_ENCODING = "utf-8-sig"

# Matches OUR section [mcp_servers.unity-biome-mcp] AND old [mcp_servers.unity-mcp],
# plus any dotted sub-sections (e.g. .env).
# Shared by merge_toml_mcp (replace → migrates old to new) and remove_toml_mcp_entry
# (delete). Does NOT match the foreign bare [mcp_servers.unity] — that has its own strip.
_UNITY_MCP_SECTION_RE = re.compile(
    r'\[mcp_servers\.unity-(?:mcp|biome-mcp)\]\n(?:(?!\[)[^\n]*\n)*'
    r'(?:\[mcp_servers\.unity-(?:mcp|biome-mcp)\.[^\]]+\]\n(?:(?!\[)[^\n]*\n)*)*',
    re.MULTILINE,
)

# ARC-0b Task 3: pin markers. JSON uses a "_pin": true sibling of "_v" inside our
# entry (checked directly via dict lookup, no regex needed — see is_entry_pinned).
# TOML has no JSON parser to lean on, so a comment line directly above our section
# carries the marker, mirroring the C# writer's format exactly (ProjectConfigToml.cs):
#   # unity-biome-mcp generated v0.54.1 pinned
# The lookahead scopes the marker to OUR section only, same guarantee as the JSON
# side's FindOurEntry — a sibling server's comment can never leak into our pin state.
_TOML_MARKER_RE = re.compile(
    r"^# unity-(?:biome-mcp|mcp) generated v[\d.]+(?: pinned)?\n"
    r"(?=\[mcp_servers\.unity-(?:biome-mcp|mcp)\])",
    re.MULTILINE,
)
_TOML_PIN_RE = re.compile(
    r"^# unity-(?:biome-mcp|mcp) generated v[\d.]+ pinned\n"
    r"(?=\[mcp_servers\.unity-(?:biome-mcp|mcp)\])",
    re.MULTILINE,
)


def _deep_merge(base: dict, overlay: dict) -> dict:
    """Recursively merge overlay into base, mutating and returning base.

    For each key in overlay: if base[key] and overlay[key] are both dict,
    recurse; otherwise overlay's value wins outright (scalars/lists — command,
    args, type, enabled, trust — are always replaced wholesale, we own the
    whole value). Keys present only in base, at any depth, stay untouched.
    """
    for key, value in overlay.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            _deep_merge(base[key], value)
        else:
            base[key] = value
    return base


def merge_mcp_config(
    config_path: pathlib.Path,
    server_entry: dict,
    root_key: str = "mcpServers",
    entry_transformer: Callable[[dict], dict] | None = None,
) -> None:
    """Read → parse → patch unity-biome-mcp entry → write. Creates file if missing."""
    if config_path.exists():
        try:
            data = json.loads(config_path.read_text(encoding=_READ_ENCODING))
        except json.JSONDecodeError as e:
            raise ValueError(f"Corrupt JSON in {config_path}: {e}") from e
    else:
        config_path.parent.mkdir(parents=True, exist_ok=True)
        data = {}

    entry = entry_transformer(server_entry) if entry_transformer else server_entry
    data.setdefault(root_key, {})
    for old in _OLD_NAMES:                 # migrate: drop any prior-name entry
        data[root_key].pop(old, None)
    current = data[root_key].get(SERVER_NAME)
    base = current if isinstance(current, dict) else {}
    data[root_key][SERVER_NAME] = _deep_merge(base, entry)

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(str(tmp), str(config_path))


def is_entry_pinned(
    config_path: pathlib.Path,
    root_key: str = "mcpServers",
    server_name: str = SERVER_NAME,
) -> bool:
    """True if our entry carries "_pin": true. ARC-0b: a pinned entry is never
    overwritten by _reconfigure_detected_clients (install.py update).

    False on a missing file, corrupt JSON, undecodable bytes (e.g. a UTF-16
    BOM or binary corruption), or a missing/non-dict entry — "degrade, don't
    crash" (same contract as merge_mcp_config's corrupt-JSON path).
    """
    if not config_path.exists():
        return False
    try:
        data = json.loads(config_path.read_text(encoding=_READ_ENCODING))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return False
    root = data.get(root_key)
    if not isinstance(root, dict):
        return False
    entry = root.get(server_name)
    return isinstance(entry, dict) and entry.get("_pin", False) is True


def unpin_entry(
    config_path: pathlib.Path,
    root_key: str = "mcpServers",
    server_name: str = SERVER_NAME,
) -> bool:
    """Remove "_pin" from our entry if present. Returns True iff a pin was removed
    (no-op, no rewrite, on a missing file/entry/pin, or undecodable bytes —
    `install.py version --unpin`)."""
    if not config_path.exists():
        return False
    try:
        data = json.loads(config_path.read_text(encoding=_READ_ENCODING))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return False
    root = data.get(root_key)
    entry = root.get(server_name) if isinstance(root, dict) else None
    if not isinstance(entry, dict) or "_pin" not in entry:
        return False
    del entry["_pin"]

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True


def merge_toml_mcp(config_path: pathlib.Path, server_entry: dict) -> None:
    """Merge unity-biome-mcp into a TOML config (Codex). Text-based, no TOML lib needed."""
    bak = config_path.with_suffix(".bak")
    if config_path.exists():
        if not bak.exists():
            shutil.copy2(config_path, bak)
        text = config_path.read_text(encoding=_READ_ENCODING).replace("\r\n", "\n")
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


def is_toml_pinned(config_path: pathlib.Path) -> bool:
    """True if the marker comment directly above our TOML section ends " pinned".

    False on undecodable bytes too — "degrade, don't crash" (mirrors
    is_entry_pinned's contract)."""
    if not config_path.exists():
        return False
    try:
        text = config_path.read_text(encoding=_READ_ENCODING).replace("\r\n", "\n")
    except UnicodeDecodeError:
        return False
    return bool(_TOML_PIN_RE.search(text))


def pin_toml_entry(config_path: pathlib.Path, version: str) -> None:
    """Insert (or replace) the pin marker comment directly above our TOML section.
    No-op if the file or our section doesn't exist yet — `install.py version --set`
    calls merge_toml_mcp first, then this, so the section is always present by then."""
    if not config_path.exists():
        return
    text = config_path.read_text(encoding=_READ_ENCODING).replace("\r\n", "\n")
    text = _TOML_MARKER_RE.sub("", text)  # drop any stale marker (pinned or not) first
    marker = f"# {SERVER_NAME} generated v{version} pinned\n"
    new_text, n = _UNITY_MCP_SECTION_RE.subn(lambda m: marker + m.group(0), text, count=1)
    if n == 0:
        return

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))


def unpin_toml_entry(config_path: pathlib.Path) -> bool:
    """Remove the pin marker comment above our TOML section, if present.
    Returns True iff a marker was removed (no-op, no rewrite, on a missing
    file/marker, or undecodable bytes) -- mirrors unpin_entry's bool contract."""
    if not config_path.exists():
        return False
    try:
        text = config_path.read_text(encoding=_READ_ENCODING).replace("\r\n", "\n")
    except UnicodeDecodeError:
        return False
    new_text = _TOML_PIN_RE.sub("", text)
    if new_text == text:
        return False

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True


def remove_mcp_entry(config_path: pathlib.Path, root_key: str = "mcpServers") -> bool:
    """Delete data[root_key]['unity-biome-mcp'] if present.

    Returns True if it was removed, False if the file/entry didn't exist.
    Raises ValueError on corrupt JSON (same contract as merge_mcp_config).
    """
    if not config_path.exists():
        return False
    try:
        data = json.loads(config_path.read_text(encoding=_READ_ENCODING))
    except json.JSONDecodeError as e:
        raise ValueError(f"Corrupt JSON in {config_path}: {e}") from e

    servers = data.get(root_key, {})
    removed = [n for n in (SERVER_NAME, *_OLD_NAMES) if servers.pop(n, None) is not None]
    if not removed:
        return False

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True


def remove_toml_mcp_entry(config_path: pathlib.Path) -> bool:
    """Strip [mcp_servers.unity-biome-mcp] (+ any dotted sub-sections) from a TOML config.

    Returns True if a section was found and removed, False otherwise.
    """
    if not config_path.exists():
        return False
    text = config_path.read_text(encoding=_READ_ENCODING).replace("\r\n", "\n")
    new_text, n = _UNITY_MCP_SECTION_RE.subn("", text)
    if n == 0:
        return False

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))
    return True
