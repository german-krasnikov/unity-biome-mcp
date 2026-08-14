"""Tests for session_identity.py — ChatSessionIdentity + atomic writer + cleanup."""
import json
import os
import time

# ─── Token hash prefix ──────────────────────────────────────────────────────

def test_make_token_hash_prefix_is_16_chars():
    from unity_mcp.session_identity import _token_hash_prefix
    result = _token_hash_prefix("a" * 64)
    assert len(result) == 16


def test_make_token_hash_prefix_deterministic():
    from unity_mcp.session_identity import _token_hash_prefix
    token = "deadbeef" * 8  # 64 hex chars
    r1 = _token_hash_prefix(token)
    r2 = _token_hash_prefix(token)
    assert r1 == r2


# ─── write_session_context ───────────────────────────────────────────────────

def _make_identity(token="a" * 64, backend="claude", mode="ask", port=9500):
    from unity_mcp.session_identity import new_session_identity
    return new_session_identity(
        conversation_id="conv-id-1",
        session_token_hex=token,
        backend=backend,
        mode=mode,
        mcp_port=port,
        config_dir=None,
    )


def test_write_session_context_creates_file(tmp_path):
    from unity_mcp.session_identity import write_session_context
    identity = _make_identity()
    dest = write_session_context(identity, context_dir=tmp_path)
    assert dest.exists()


def test_write_session_context_uses_tmp_replace(tmp_path, monkeypatch):
    import unity_mcp.session_identity as si
    from unity_mcp.session_identity import write_session_context

    replace_calls = []
    real_replace = os.replace

    def counting_replace(src, dst):
        replace_calls.append((src, dst))
        real_replace(src, dst)

    monkeypatch.setattr(si.os, "replace", counting_replace)

    identity = _make_identity()
    write_session_context(identity, context_dir=tmp_path)

    assert len(replace_calls) == 1


def test_write_session_context_permissions(tmp_path):
    from unity_mcp.session_identity import write_session_context
    identity = _make_identity()
    dest = write_session_context(identity, context_dir=tmp_path)
    mode = dest.stat().st_mode
    # Last 3 octal digits must be "600" (owner rw, group none, other none)
    assert oct(mode)[-3:] == "600"


def test_write_session_context_no_raw_token_bytes(tmp_path):
    from unity_mcp.session_identity import write_session_context
    token = "deadbeef" * 8  # 64 hex chars, recognizable pattern
    identity = _make_identity(token=token)
    dest = write_session_context(identity, context_dir=tmp_path)
    content = dest.read_text(encoding="utf-8")
    assert token not in content
    assert token.upper() not in content


def test_write_session_context_json_fields(tmp_path):
    from unity_mcp.session_identity import write_session_context
    identity = _make_identity(backend="codex", mode="agent", port=9601)
    dest = write_session_context(identity, context_dir=tmp_path)
    data = json.loads(dest.read_text(encoding="utf-8"))
    assert data["schema_version"] == 1
    assert "token_hash" in data
    assert data["backend"] == "codex"
    assert data["mode"] == "agent"
    assert data["mcp_port"] == 9601
    assert "internal_session_id" in data
    assert "conversation_id" in data
    assert "started_at_utc" in data


# ─── new_session_identity — project_id (C1) ─────────────────────────────────

def test_new_session_identity_project_id_produces_12_char_fingerprint():
    """With project_id: fingerprint is sha256(project_id)[:12] — 12 chars."""
    import hashlib
    from unity_mcp.session_identity import new_session_identity
    project_id = "cloud-abc-123"
    identity = new_session_identity(
        conversation_id="c", session_token_hex="a" * 64,
        backend="claude", mode="ask", mcp_port=9500,
        config_dir=None, project_id=project_id,
    )
    expected = hashlib.sha256(project_id.encode()).hexdigest()[:12]
    assert identity.project_fingerprint == expected
    assert len(identity.project_fingerprint) == 12


def test_new_session_identity_without_project_id_produces_12_char_fingerprint():
    """Without project_id: fingerprint falls back to sha256(config_dir or '')[:12]."""
    from unity_mcp.session_identity import new_session_identity
    identity = new_session_identity(
        conversation_id="c", session_token_hex="a" * 64,
        backend="claude", mode="ask", mcp_port=9500,
        config_dir=None,
    )
    assert len(identity.project_fingerprint) == 12


# ─── cleanup_stale_sessions ──────────────────────────────────────────────────

def test_cleanup_stale_sessions_removes_old(tmp_path):
    from unity_mcp.session_identity import cleanup_stale_sessions
    old_file = tmp_path / "abc123.json"
    old_file.write_text("{}", encoding="utf-8")
    # Set mtime to 25 hours ago (older than default 24h TTL)
    old_mtime = time.time() - 90000
    os.utime(old_file, (old_mtime, old_mtime))

    cleanup_stale_sessions(context_dir=tmp_path, ttl_s=86400)
    assert not old_file.exists()


def test_token_hash_prefix_invalid_hex_returns_empty():
    """Non-hex input must not crash — returns '' instead of raising ValueError."""
    from unity_mcp.session_identity import _token_hash_prefix
    assert _token_hash_prefix("not-valid-hex!") == ""


def test_token_hash_prefix_odd_length_returns_empty():
    """Odd-length hex string raises ValueError in fromhex — returns ''."""
    from unity_mcp.session_identity import _token_hash_prefix
    assert _token_hash_prefix("abc") == ""  # odd length


def test_cleanup_stale_sessions_keeps_fresh(tmp_path):
    from unity_mcp.session_identity import cleanup_stale_sessions
    fresh_file = tmp_path / "fresh123.json"
    fresh_file.write_text("{}", encoding="utf-8")
    # mtime is now (default) — well within TTL

    cleanup_stale_sessions(context_dir=tmp_path, ttl_s=86400)
    assert fresh_file.exists()


# ── Fingerprint length consistency ───────────────────────────────────────────

def test_fingerprint_without_project_id_is_12_chars():
    """cwd fallback fingerprint must be 12 chars (same as project_id path)."""
    from unity_mcp.session_identity import new_session_identity
    identity = new_session_identity(
        conversation_id="conv-1",
        session_token_hex="a" * 64,
        backend="claude", mode="ask",
        mcp_port=9500, config_dir="/some/path",
        project_id=None,
    )
    assert len(identity.project_fingerprint) == 12
