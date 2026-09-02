"""Structured error classification for Unity connection failures."""
import asyncio
import enum
from dataclasses import dataclass

from mcp.server.fastmcp.exceptions import ToolError


class UnityUnavailableError(ToolError):
    """ToolError wrapper raised by server.py's _send_raw for a ConnectionError,
    TimeoutError, or OSError coming out of bridge.send() (DomainReloadError
    included, as a ConnectionError subclass). A poll loop that owns its own
    deadline (sync_unity, await_compile) can catch this alongside the raw
    exception types and keep polling instead of aborting on the first
    transient disconnect (C1 r2 #7)."""


class SessionIdentityMismatch(ConnectionError):
    """Non-retryable: reconnect landed on a different Unity Editor or project (MCP-SESS-024)."""


class CapacityBusyError(ConnectionError):
    """Retryable: Unity TCP server is at MaxClients capacity (MCP-CAP-025).

    Sent by Unity with typed JSON before closing the connection gracefully.
    """

    def __init__(self, message: str, retry_after_seconds: float = 5.0,
                 capacity: int = 0, active: int = 0) -> None:
        super().__init__(message)
        self.retry_after_seconds = retry_after_seconds
        self.capacity = capacity
        self.active = active


class UncertainDeliveryError(ConnectionError):
    """A mutating frame may have executed, so the transport must not resend it.

    Callers can query the same bridge ledger with ``op_id``; parsing the human
    message is never required.
    """

    def __init__(self, *, cmd: str, op_id: str, delivery: enum.Enum | str) -> None:
        super().__init__(
            f"Command {cmd!r} was sent; outcome is uncertain and "
            "the unsafe command was not retried"
        )
        self.cmd = cmd
        self.op_id = op_id
        self.delivery = delivery


@dataclass
class UnityError:
    message: str
    unity_state: str   # compiling/reloading/crashed/frozen/disconnected/unknown
    is_transient: bool
    retry_after_seconds: int
    original_exception: str


class FailureCategory(enum.Enum):
    """Typed protocol-level cause for a command failure (MCP-DIAG-009)."""
    TRANSPORT_CLOSED = "transport_closed"
    CAPACITY_BUSY = "capacity_busy"
    SESSION_MISMATCH = "session_mismatch"
    TIMEOUT = "timeout"
    COMPILE_PENDING = "compile_pending"
    PLAY_NOT_READY = "play_not_ready"
    PROTOCOL_ERROR = "protocol_error"
    COMMAND_NOT_FOUND = "command_not_found"
    UNKNOWN = "unknown"


def categorize_failure(exc: Exception) -> tuple[FailureCategory, str]:
    """Map an exception to a typed FailureCategory and human-readable detail.

    Returns (category, detail) where category is machine-readable and detail
    is a human-readable description of the failure cause.
    """
    # Check subclasses before base classes (MRO-safe order)
    if isinstance(exc, CapacityBusyError):
        return FailureCategory.CAPACITY_BUSY, str(exc)
    if isinstance(exc, SessionIdentityMismatch):
        return FailureCategory.SESSION_MISMATCH, str(exc)
    if isinstance(exc, (asyncio.TimeoutError, TimeoutError)):
        return FailureCategory.TIMEOUT, str(exc) or "Operation timed out"
    if isinstance(exc, ConnectionError):
        return FailureCategory.TRANSPORT_CLOSED, str(exc)
    return FailureCategory.UNKNOWN, str(exc)


def classify_failure(exc: Exception, probe_busy: bool, remaining: float) -> UnityError:
    exc_name = type(exc).__name__
    # Import here to avoid circular import
    from unity_mcp.bridge import DomainReloadError

    if isinstance(exc, DomainReloadError):
        return UnityError("Unity domain reload in progress", "reloading", True,
                          int(remaining or 5), exc_name)
    if isinstance(exc, asyncio.IncompleteReadError):
        if probe_busy:
            return UnityError("Unity reloading", "reloading", True, int(remaining), exc_name)
        return UnityError("Unity connection lost", "crashed", False, 0, exc_name)
    if isinstance(exc, ConnectionRefusedError):
        if probe_busy:
            return UnityError("Unity compiling", "compiling", True, int(remaining), exc_name)
        return UnityError("Unity not running", "disconnected", False, 0, exc_name)
    if isinstance(exc, TimeoutError):
        if probe_busy:
            return UnityError("Unity busy", "frozen", True, min(30, int(remaining)), exc_name)
        return UnityError("Unity not responding", "frozen", False, 0, exc_name)
    return UnityError(f"Connection error: {exc}", "unknown", False, 0, exc_name)
