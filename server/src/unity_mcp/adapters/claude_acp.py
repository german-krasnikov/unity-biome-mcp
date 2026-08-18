"""ClaudeAcpAdapter: claude-agent-acp subprocess in ACP output mode."""

import tempfile

from .. import mcp_config_writer
from ..backend_def import ClaudeAcpDef  # noqa: TC001
from ..cli_session import SessionMeta  # noqa: TC001
from ..permission_broker import PermissionBroker  # noqa: TC001
from .acp import AcpAgentAdapter


def _build_claude_acp_argv(meta: SessionMeta) -> list[str]:
    """Build claude-agent-acp exec argv. Never raises.

    TODO: verify flag names via `claude-agent-acp --help` after install.
    """
    config_dir  = meta.config_dir or tempfile.gettempdir()
    config_path = mcp_config_writer.write_claude_config(config_dir, meta.mcp_port)

    argv: list[str] = []
    if meta.internal_session_id:
        argv += ["--resume", meta.internal_session_id]  # TODO: verify
    if meta.model:
        argv += ["--model", meta.model]                 # TODO: verify
    argv += ["--mcp-config", config_path]               # TODO: verify
    extra_args = meta.extra.get("extra_args", "")
    if extra_args:
        from ..backend_def import sanitize_extra_args
        argv += sanitize_extra_args(extra_args)
    if meta.prompt:
        argv.append(meta.prompt)
    return argv


class ClaudeAcpAdapter(AcpAgentAdapter):
    """claude-agent-acp subprocess in ACP output mode."""

    def __init__(self, backend: ClaudeAcpDef, broker: PermissionBroker) -> None:
        super().__init__(backend, broker)

    def _build_argv(self, meta: SessionMeta) -> list[str]:
        return _build_claude_acp_argv(meta)
