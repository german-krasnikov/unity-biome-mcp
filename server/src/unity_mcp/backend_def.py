"""CLI backend definitions: binary resolution + argv construction.

Each BackendDef subclass knows its binary name, resume capability, and how to
build (argv, env_set, env_strip) from high-level params.  All I/O (config file
writes) is injectable via config_dir so unit tests never touch real FS paths.
"""
from __future__ import annotations
import asyncio
import os
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass

from . import mcp_config_writer
from .config.merger import SERVER_NAME

if sys.platform == "win32":
    import winreg
else:
    winreg = None  # type: ignore[assignment]  # seam for monkeypatching in tests on non-Windows CI

# ── Permission constants (mirrors PermissionConfig.cs) ───────────────────────
# NOT SERVER_NAME.replace('-', '_') — hyphens are legal in MCP tool-name
# segments and are never touched by Claude's sanitizer. Must match
# PermissionConfig.MCP_BLANKET in the C# plugin exactly.
MCP_BLANKET        = f"mcp__{SERVER_NAME}"

# ── extra_args sanitizer ─────────────────────────────────────────────────────
# Flags that would override security-critical argv already set by build_args.
_BLOCKED_FLAGS = frozenset({
    "--output-format", "--input-format",
    "--permission-mode", "--permission-prompt-tool",
    "--mcp-config", "--config",
    "--format",
})


def _sanitize_extra_args(raw: str) -> list[str]:
    """Strip dangerous flags (and their values) from user-supplied extra_args."""
    if not raw:
        return []
    tokens = shlex.split(raw)
    result, skip = [], False
    for tok in tokens:
        if skip:
            skip = False
            continue
        flag = tok.split("=", 1)[0]
        if flag in _BLOCKED_FLAGS:
            if "=" not in tok:
                skip = True  # separate-value style: drop next token too
            continue
        result.append(tok)
    return result
MCP_PERMISSION_TOOL = MCP_BLANKET + "__permission_prompt"
MCP_TOOL_PREFIX    = MCP_BLANKET + "__"

# ── Output format discriminators ──────────────────────────────────────────────
OUTPUT_FORMAT_STREAM_JSON   = "stream-json"    # Claude
OUTPUT_FORMAT_PLAIN_TEXT    = "plain-text"     # Agy (plain stdout)
OUTPUT_FORMAT_CODEX_JSON    = "codex-json"     # Codex (OpenAI Responses API)
OUTPUT_FORMAT_OPENCODE_JSON = "opencode-json"  # OpenCode run --format json
OUTPUT_FORMAT_KIMI_JSON     = "kimi-json"      # Kimi -p --output-format stream-json


_WIN_VAR_RE = re.compile(r"%(\w+)%")


def _expand_win_vars(path: str) -> str:
    """Expand '%VAR%' refs via os.environ. Regex-based (not ntpath.expandvars)
    so this is testable on non-Windows hosts too — posixpath.expandvars ignores
    %VAR% syntax entirely."""
    return _WIN_VAR_RE.sub(lambda m: os.environ.get(m.group(1), m.group(0)), path)


def _which_windows_registry(binary: str) -> str | None:
    """Read HKCU/HKLM PATH from the registry, probe each dir + well-known
    fallbacks for '<binary>.exe'/'.cmd'. Unity's Editor process doesn't
    inherit a login-shell PATH on Windows (no equivalent of zsh/bash -lic),
    so npm/cargo/uv global installs are otherwise invisible."""
    raw_dirs: list[str] = []
    for root, subkey in (
        (winreg.HKEY_CURRENT_USER, "Environment"),
        (winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
    ):
        try:
            with winreg.OpenKey(root, subkey) as key:
                value, _ = winreg.QueryValueEx(key, "Path")
                raw_dirs += value.split(";")
        except OSError:
            continue

    fallbacks = ["%APPDATA%/npm", "%USERPROFILE%/.cargo/bin", "%LOCALAPPDATA%/uv/bin"]
    for d in raw_dirs + fallbacks:
        d = _expand_win_vars(d.strip())
        if not d:
            continue
        for ext in (".exe", ".cmd"):
            candidate = os.path.join(d, binary + ext)
            if os.path.isfile(candidate):
                return candidate
    return None


_LOGIN_PATH_CACHE: str | None = None
_LOGIN_PATH_CACHE_TS: float = 0.0
_LOGIN_PATH_RETRY_TTL = 30.0  # seconds — bounds re-spawn frequency while shell stays broken


async def _run_login_shell(command: str, timeout: float = 3.0) -> str:
    """Run `command` in the user's login shell (zsh/bash -lic), return raw stdout.
    Empty string on any failure or unsupported platform (win32 handled by callers)."""
    if sys.platform == "darwin":
        shell, flag = "/bin/zsh", "-lic"
    elif sys.platform.startswith("linux"):
        shell, flag = "/bin/bash", "-lic"
    else:
        return ""
    try:
        return (await asyncio.to_thread(
            subprocess.run, [shell, flag, command],
            capture_output=True, text=True, timeout=timeout,
        )).stdout
    except Exception:
        return ""


async def login_shell_path() -> str:
    """The user's full login-shell PATH (cached). Empty on Windows / failure.

    Unity launched from Finder gives child processes a minimal PATH, so node-based
    CLIs (codex is `#!/usr/bin/env node`) fail with exit 127 `env: node: not found`.
    Spawning backends with this PATH lets their interpreters/tools resolve.

    A successful result is cached for the process lifetime (PATH doesn't change at
    runtime). A failure (empty result) is only cached for `_LOGIN_PATH_RETRY_TTL`
    seconds, then retried — a transient failure must not permanently disable PATH
    prepending for the rest of the Unity session.
    """
    global _LOGIN_PATH_CACHE, _LOGIN_PATH_CACHE_TS
    if _LOGIN_PATH_CACHE:
        return _LOGIN_PATH_CACHE
    if _LOGIN_PATH_CACHE is not None and time.monotonic() - _LOGIN_PATH_CACHE_TS < _LOGIN_PATH_RETRY_TTL:
        return _LOGIN_PATH_CACHE
    out = await _run_login_shell('printf %s "$PATH"')
    # login shells may print noise (history msgs); take the last line containing ':' path-list
    cand = [ln for ln in out.splitlines() if "/" in ln and ":" in ln]
    _LOGIN_PATH_CACHE = cand[-1].strip() if cand else out.strip()
    _LOGIN_PATH_CACHE_TS = time.monotonic()
    return _LOGIN_PATH_CACHE


async def _which_via_login_shell(binary: str) -> str | None:
    """Login-shell resolution for macOS/Linux (Unity has minimal PATH)."""
    if sys.platform == "win32":
        return await asyncio.to_thread(_which_windows_registry, binary)
    out = await _run_login_shell(f'command -v "{binary}"')
    for line in reversed(out.splitlines()):
        if line.startswith("/"):
            return line.strip()
    return None


# ── Base class ────────────────────────────────────────────────────────────────

@dataclass
class BackendDef:
    name:          str
    binary:        str
    has_resume:    bool
    output_format: str  = OUTPUT_FORMAT_STREAM_JSON
    reads_stdin:   bool = False

    @property
    def uses_stream_json(self) -> bool:
        """Backward-compat: True iff output_format is stream-json."""
        return self.output_format == OUTPUT_FORMAT_STREAM_JSON

    async def resolve_binary(self) -> str | None:
        """shutil.which → login shell fallback → None."""
        found = shutil.which(self.binary)
        return found if found else await _which_via_login_shell(self.binary)

    def build_args(
        self,
        mode: str,
        model: str | None,
        mcp_port: int,
        prompt: str = "",
        session_id: str | None = None,
        config_dir: str | None = None,
        **kwargs,
    ) -> tuple[list[str], dict[str, str], list[str]]:
        raise NotImplementedError


# ── Claude ────────────────────────────────────────────────────────────────────

@dataclass
class ClaudeDef(BackendDef):
    name:          str  = "claude"
    binary:        str  = "claude"
    has_resume:    bool = True
    reads_stdin:   bool = True

    def build_args(self, mode, model, mcp_port, prompt="", session_id=None,
                   config_dir=None, agent_name=None, allowed_mcp_tools=None,
                   append_system_prompt=None, extra_args=None, **kwargs):
        config_dir  = config_dir or tempfile.gettempdir()
        config_path = mcp_config_writer.write_claude_config(config_dir, mcp_port)
        perm_mode   = "acceptEdits" if mode == "agent" else "plan"

        argv: list[str] = [
            "-p",
            "--output-format",          "stream-json",
            "--verbose",
            "--include-partial-messages",
            "--input-format",           "stream-json",
            "--mcp-config",             config_path,
            "--permission-mode",        perm_mode,
            "--permission-prompt-tool", MCP_PERMISSION_TOOL,
        ]

        if allowed_mcp_tools is None:
            argv += ["--allowedTools", MCP_BLANKET]
        elif allowed_mcp_tools:
            argv += ["--allowedTools",
                     ",".join(MCP_TOOL_PREFIX + t for t in allowed_mcp_tools)]

        if session_id:
            argv += ["--resume", session_id]
        if agent_name:
            argv += ["--agent", agent_name]
        if append_system_prompt:
            argv += ["--append-system-prompt", append_system_prompt]
        if model:
            argv += ["--model", model]
        if extra_args:
            argv += _sanitize_extra_args(extra_args)

        strip = ["CLAUDECODE", "UNITY_MCP_PORT"]
        return argv, {}, strip


# ── Codex ─────────────────────────────────────────────────────────────────────

@dataclass
class CodexDef(BackendDef):
    name:          str  = "codex"
    binary:        str  = "codex"
    has_resume:    bool = True  # resume via subcommand switch
    output_format: str  = OUTPUT_FORMAT_CODEX_JSON

    def build_args(self, mode, model, mcp_port, prompt="", session_id=None,
                   config_dir=None, extra_args=None, **kwargs):
        cmd, cmd_args = mcp_config_writer.resolve_server_cmd()

        argv: list[str] = ["exec"]
        if session_id:
            argv += ["resume", session_id, "--json",
                     "--dangerously-bypass-approvals-and-sandbox"]
        else:
            argv += ["--json", "-C", os.getcwd(), "-s", "danger-full-access"]

        argv.append("--skip-git-repo-check")

        def _toml_esc(s: str) -> str:
            return s.replace("\\", "\\\\").replace('"', '\\"')

        def _toml_arr(items: list[str]) -> str:
            return ",".join(f'"{_toml_esc(i)}"' for i in items)

        # Use the SAME server name the project .codex/config.toml uses ("unity-mcp"),
        # so this inline -c OVERRIDES the project entry instead of adding a duplicate
        # second Unity server (two servers on port 9500 → codex hangs).
        argv += [
            "-c", f'mcp_servers.unity-mcp.command="{_toml_esc(cmd)}"',
            "-c", f"mcp_servers.unity-mcp.args=[{_toml_arr(cmd_args)}]",
            "-c", "mcp_servers.unity-mcp.startup_timeout_sec=30",
            "-c", f'mcp_servers.unity-mcp.env.UNITY_MCP_PORT="{mcp_port}"',
        ]

        if model:
            argv += ["--model", model]
        if extra_args:
            argv += _sanitize_extra_args(extra_args)
        if prompt:
            argv.append(prompt)

        return argv, {"UNITY_MCP_PORT": str(mcp_port)}, []


# ── Kimi ──────────────────────────────────────────────────────────────────────

@dataclass
class KimiDef(BackendDef):
    name:          str  = "kimi"
    binary:        str  = "kimi"
    has_resume:    bool = False
    output_format: str  = OUTPUT_FORMAT_KIMI_JSON

    def build_args(self, mode, model, mcp_port, prompt="", session_id=None,
                   config_dir=None, extra_args=None, **kwargs):
        config_dir = config_dir or tempfile.gettempdir()
        mcp_config_writer.write_kimi_mcp_config(config_dir, mcp_port)

        argv: list[str] = ["-p", prompt, "--output-format", "stream-json"]
        if model:
            argv += ["--model", model]
        if extra_args:
            argv += _sanitize_extra_args(extra_args)

        return argv, {"UNITY_MCP_PORT": str(mcp_port)}, []


# ── Agy ───────────────────────────────────────────────────────────────────────

@dataclass
class AgyDef(BackendDef):
    name:          str  = "agy"
    binary:        str  = "agy"
    has_resume:    bool = False
    output_format: str  = OUTPUT_FORMAT_PLAIN_TEXT

    def build_args(self, mode, model, mcp_port, prompt="", session_id=None,
                   config_dir=None, extra_args=None, **kwargs):
        config_dir = config_dir or tempfile.gettempdir()
        mcp_config_writer.write_agy_settings(config_dir, mcp_port)

        argv: list[str] = ["-p", prompt]
        if model:
            argv += ["--model", model]
        if mode == "agent":
            argv.append("--dangerously-skip-permissions")
        if extra_args:
            argv += _sanitize_extra_args(extra_args)

        return argv, {"UNITY_MCP_PORT": str(mcp_port)}, []


# ── OpenCode ──────────────────────────────────────────────────────────────────

@dataclass
class OpenCodeDef(BackendDef):
    name:          str  = "opencode"
    binary:        str  = "opencode"
    has_resume:    bool = True  # -s <id>
    output_format: str  = OUTPUT_FORMAT_OPENCODE_JSON

    def build_args(self, mode, model, mcp_port, prompt="", session_id=None,
                   config_dir=None, extra_args=None, **kwargs):
        config_dir  = config_dir or tempfile.gettempdir()
        config_path = mcp_config_writer.write_opencode_config(config_dir, mcp_port)

        argv: list[str] = ["run", "--format", "json", "--dangerously-skip-permissions"]
        if model:
            argv += ["--model", model]
        if session_id:
            argv += ["-s", session_id]
        if extra_args:
            argv += _sanitize_extra_args(extra_args)
        argv.append(prompt)

        return argv, {"OPENCODE_CONFIG": config_path, "UNITY_MCP_PORT": str(mcp_port)}, []


# ── Registry ──────────────────────────────────────────────────────────────────

BACKENDS: dict[str, BackendDef] = {
    "claude":       ClaudeDef(),
    "codex":        CodexDef(),
    "kimi":         KimiDef(),
    "agy":          AgyDef(),
    "antigravity":  AgyDef(),
    "opencode":     OpenCodeDef(),
}
