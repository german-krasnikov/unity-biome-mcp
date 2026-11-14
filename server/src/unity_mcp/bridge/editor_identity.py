"""Editor identity capture for session identity enforcement (MCP-SESS-024)."""
from dataclasses import dataclass


class _CandidateIdentityError(ConnectionError):
    """A TCP endpoint answered, but could not prove it is the intended Editor."""


@dataclass(frozen=True)
class EditorIdentity:
    """Immutable editor identity captured on first successful handshake (MCP-SESS-024)."""
    project_id: str
    project_path: str
