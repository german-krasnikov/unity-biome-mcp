"""Optional wire-cassette recorder for `UnityBridge.send()`.

Set UNITY_MCP_TRACE_FILE to a path and every completed send()/response pair
(success and error alike) is appended as one JSONL line in the shape
`FakeUnityServer.load_cassette` (tests/wire/helpers/fake_server.py) can
replay directly: {"cmd": ..., "args": ..., "response": {"ok", "data", "error"}}.

Recording is strictly opt-in and must never break send(): an unwritable
trace path logs one warning and the recorder goes silent for the rest of
the process.
"""
import json
import logging
import os
from functools import lru_cache
from pathlib import Path

logger = logging.getLogger(__name__)

TRACE_FILE_ENV = "UNITY_MCP_TRACE_FILE"

_warned = False


@lru_cache(maxsize=1)
def _trace_path() -> Path | None:
    """Read UNITY_MCP_TRACE_FILE once per process, not on every send()."""
    raw = os.environ.get(TRACE_FILE_ENV)
    return Path(raw) if raw else None


def reset_for_tests() -> None:
    """Test-only: clear the cached env resolution and the one-shot warning
    flag so xdist workers and test order never leak one test's env var
    into another's."""
    global _warned
    _trace_path.cache_clear()
    _warned = False


def _as_cassette_response(result: dict) -> dict:
    """Normalize a wire response to the cassette shape `load_cassette`
    expects. The wire protocol's error key is `err` (see CommandRouter /
    ScriptedUnityPeer); the cassette format's key is `error` — this is the
    one place that translation happens."""
    return {
        "ok": bool(result.get("ok", True)),
        "data": result.get("data", ""),
        "error": result.get("err", result.get("error", "")),
    }


def record(cmd: str, args: dict, result: dict) -> None:
    """Append one cassette-shaped JSONL line for a completed send(), if
    UNITY_MCP_TRACE_FILE is set. Never raises."""
    global _warned
    path = _trace_path()
    if path is None:
        return
    line = json.dumps(
        {"cmd": cmd, "args": args, "response": _as_cassette_response(result)},
        ensure_ascii=False,
    )
    try:
        with open(path, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except OSError as exc:
        if not _warned:
            logger.warning("cassette recorder: could not write to %s: %s", path, exc)
            _warned = True
