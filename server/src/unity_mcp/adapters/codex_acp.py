"""CodexAcpAdapter: Codex subprocess in ACP output mode.

Opt-in via UNITY_MCP_ACP_CODEX=1. Default path remains LegacyCliAdapter.
"""
from __future__ import annotations

from typing import TYPE_CHECKING

from .. import mcp_config_writer
from ..config.merger import SERVER_NAME as _SERVER_NAME
from .acp import _ACP_FORMAT_FLAG, _ACP_FORMAT_VALUE, AcpAgentAdapter

if TYPE_CHECKING:
    from ..backend_def import CodexDef
    from ..cli_session import SessionMeta
    from ..permission_broker import PermissionBroker

_MCP_STARTUP_TIMEOUT_SEC = 30


def _build_codex_acp_argv(meta: SessionMeta) -> list[str]:
    """Build Codex exec argv for ACP mode. Pure function, never raises.

    MCP server is injected via -c inline TOML (same as CodexDef.build_args).
    No -s approval flag: permission handling via ACP protocol.
    """
    cmd, cmd_args = mcp_config_writer.resolve_server_cmd()

    def _toml_esc(s: str) -> str:
        return s.replace("\\", "\\\\").replace('"', '\\"')

    def _toml_arr(items: list[str]) -> str:
        return ",".join(f'"{_toml_esc(i)}"' for i in items)

    argv: list[str] = ["exec"]
    if meta.internal_session_id:
        argv += ["resume", meta.internal_session_id, _ACP_FORMAT_FLAG, _ACP_FORMAT_VALUE]
    else:
        argv += [_ACP_FORMAT_FLAG, _ACP_FORMAT_VALUE, "--skip-git-repo-check"]

    argv += [
        "-c", f'mcp_servers.{_SERVER_NAME}.command="{_toml_esc(cmd)}"',
        "-c", f"mcp_servers.{_SERVER_NAME}.args=[{_toml_arr(cmd_args)}]",
        "-c", f"mcp_servers.{_SERVER_NAME}.startup_timeout_sec={_MCP_STARTUP_TIMEOUT_SEC}",
        "-c", f'mcp_servers.{_SERVER_NAME}.env.UNITY_MCP_PORT="{meta.mcp_port}"',
    ]
    if meta.model:
        argv += ["--model", meta.model]
    extra_args = meta.extra.get("extra_args", "")
    if extra_args:
        from ..backend_def import sanitize_extra_args
        argv += sanitize_extra_args(extra_args)
    if meta.prompt:
        argv.append(meta.prompt)
    return argv


class CodexAcpAdapter(AcpAgentAdapter):
    """Codex subprocess in ACP output mode. Opt-in via UNITY_MCP_ACP_CODEX=1."""

    def __init__(self, backend: CodexDef, broker: PermissionBroker) -> None:
        super().__init__(backend, broker)

    def _build_argv(self, meta: SessionMeta) -> list[str]:
        return _build_codex_acp_argv(meta)
