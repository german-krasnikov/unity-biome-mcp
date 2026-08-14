"""Chat session identity: immutable value object + atomic context-file writer."""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


@dataclass(frozen=True)
class ChatSessionIdentity:
    internal_session_id: str   # uuid4
    conversation_id:     str   # uuid4, stable per relay lifetime
    token_hash_prefix:   str   # sha256(token).hex()[:16] — filename + file field
    started_at_utc:      str   # ISO 8601
    backend:             str
    mode:                str
    mcp_port:            int
    project_fingerprint: str   # sha256(cwd or "").hex()[:8]


def _token_hash_prefix(session_token_hex: str) -> str:
    """Return sha256(raw_token_bytes).hex()[:16]. Token is consumed once and discarded."""
    try:
        return hashlib.sha256(bytes.fromhex(session_token_hex)).hexdigest()[:16]
    except ValueError:
        return ""


def new_session_identity(
    conversation_id:   str,
    session_token_hex: str,   # raw hex from C#; never stored
    backend:           str,
    mode:              str,
    mcp_port:          int,
    config_dir:        str | None,
    project_id:        str | None = None,  # T19: stable cross-path identity from C#
) -> ChatSessionIdentity:
    """Construct ChatSessionIdentity from relay args. Raw token is hashed, not stored.

    project_id: if supplied (from client_hello projectId), fingerprint is
    sha256(project_id)[:12] — stable across path moves. Falls back to
    sha256(cwd)[:8] for backward compat with old plugin versions.
    """
    if project_id:
        fingerprint = hashlib.sha256(project_id.encode()).hexdigest()[:12]
    else:
        cwd = config_dir or ""
        fingerprint = hashlib.sha256(cwd.encode()).hexdigest()[:12]
    return ChatSessionIdentity(
        internal_session_id = str(uuid.uuid4()),
        conversation_id     = conversation_id,
        token_hash_prefix   = _token_hash_prefix(session_token_hex),
        started_at_utc      = datetime.now(timezone.utc).isoformat(),
        backend             = backend,
        mode                = mode,
        mcp_port            = mcp_port,
        project_fingerprint = fingerprint,
    )


def write_session_context(
    identity: ChatSessionIdentity,
    context_dir: Path | None = None,
) -> Path:
    """Write JSON context file atomically (tmp + os.replace). chmod 0o600. Non-critical."""
    from .paths import chat_sessions_dir
    target_dir = context_dir if context_dir is not None else chat_sessions_dir()
    target_dir.mkdir(parents=True, exist_ok=True)

    dest = target_dir / f"{identity.token_hash_prefix}.json"
    tmp  = target_dir / f".tmp-{identity.internal_session_id}"

    payload = {
        "schema_version":      1,
        "internal_session_id": identity.internal_session_id,
        "conversation_id":     identity.conversation_id,
        "token_hash":          identity.token_hash_prefix,
        "started_at_utc":      identity.started_at_utc,
        "backend":             identity.backend,
        "mode":                identity.mode,
        "mcp_port":            identity.mcp_port,
        "project_fingerprint": identity.project_fingerprint,
    }

    try:
        tmp.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
        tmp.chmod(0o600)
        os.replace(tmp, dest)
    except OSError:
        with contextlib.suppress(OSError):
            tmp.unlink()

    return dest


def cleanup_stale_sessions(
    context_dir: Path | None = None,
    ttl_s: float = 86400,
) -> None:
    """Remove context files older than ttl_s seconds. Best-effort; ignores errors."""
    from .paths import chat_sessions_dir
    target_dir = context_dir if context_dir is not None else chat_sessions_dir()
    if not target_dir.exists():
        return

    cutoff = time.time() - ttl_s
    for f in target_dir.glob("*.json"):
        with contextlib.suppress(OSError):
            if f.stat().st_mtime < cutoff:
                f.unlink()
    for f in target_dir.glob(".tmp-*"):
        with contextlib.suppress(OSError):
            f.unlink()
