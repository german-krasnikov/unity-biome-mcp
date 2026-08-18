"""Structured error classification for Unity connection failures."""
import asyncio
from dataclasses import dataclass


@dataclass
class UnityError:
    message: str
    unity_state: str   # compiling/reloading/crashed/frozen/disconnected/unknown
    is_transient: bool
    retry_after_seconds: int
    original_exception: str


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
