"""Telemetry/metrics registry. In-memory counters + timers + cost tracking.

Subclasses biome-mcp's MetricsCollector for the shared counter/observation/
timer/cost plumbing (p50/p95 observation stats, millisecond timer, per-
million-token cost rates all come from the base class now). Overridden where
Unity still needs its own behavior: a hard key cap on inc()/observe()
(hostile/dynamic cmd protection), a costs_usd dict keyed by feature with
Haiku per-million-token pricing (vs. base's append-only cost-event list), and
an UNITY_MCP_METRICS/UNITY_MCP_LOG_DIR jsonl sink with a buffered file handle
(biome's own sink uses a single env var holding the full path, opened
per-event).

Thread-unsafe by design — relies on CPython GIL for tiny dict[str]+=1 ops.
"""
import atexit
import json
import os
import time
from contextlib import suppress

from biome_mcp.metrics import MetricsCollector
from biome_mcp.metrics import counted as _biome_counted
from biome_mcp.metrics import timed as _biome_timed

from .metrics_report import _p95
from .metrics_report import format_report as _format_report

__all__ = [
    "MetricsRegistry", "METRICS", "timed", "counted",
    "HAIKU_IN_PER_MTOK", "HAIKU_OUT_PER_MTOK", "_p95",
]

# Hard cap on unique counter/observation keys, shared by inc()/observe() below,
# to prevent unbounded growth on hostile/dynamic cmds.
_MAX_KEYS = 512


def _env_float(name: str, default: float) -> float:
    """Read a float override from env (UNITY_MCP_* convention, see llm_config.py);
    falls back to `default` when unset or unparsable."""
    raw = os.environ.get(name)
    if not raw:
        return default
    try:
        return float(raw)
    except ValueError:
        return default


# Haiku pricing (per 1M tokens), overridable via env so deployments can repoint
# pricing without a code change (P7: no hardcoded-only config). Kept as named
# constants for backward compatibility (imported directly by tests/call sites,
# e.g. budget/cost_tracker.py) and mirrored into _PRICING for model-name lookup
# in cost().
HAIKU_IN_PER_MTOK = _env_float("UNITY_MCP_HAIKU_IN_PER_MTOK", 0.80)
HAIKU_OUT_PER_MTOK = _env_float("UNITY_MCP_HAIKU_OUT_PER_MTOK", 4.00)
_PRICING: dict[str, tuple[float, float]] = {
    "haiku": (HAIKU_IN_PER_MTOK, HAIKU_OUT_PER_MTOK),
}


class MetricsRegistry(MetricsCollector):
    """Process-wide metrics. inc() / observe() / timer() / cost() / event()."""

    def __init__(self):
        super().__init__()
        self._costs_usd: dict[str, float] = {}

    def _setup_sink(self) -> None:
        self._jsonl_f = None
        if os.environ.get("UNITY_MCP_METRICS") != "1":
            return
        log_dir = os.environ.get("UNITY_MCP_LOG_DIR")
        if not log_dir:
            return
        try:
            os.makedirs(log_dir, exist_ok=True)
            path = os.path.join(log_dir, "metrics.jsonl")
            self._jsonl_f = open(path, "a", encoding="utf-8")  # noqa: SIM115
            atexit.register(self._close)
        except Exception:
            self._jsonl_f = None

    def _close(self) -> None:
        if self._jsonl_f:
            with suppress(Exception):
                self._jsonl_f.close()
            self._jsonl_f = None

    def inc(self, key: str, n: int = 1) -> None:
        if key not in self._counters and len(self._counters) >= _MAX_KEYS:
            return
        self._counters[key] = self._counters.get(key, 0) + n

    def observe(self, key: str, value: float) -> None:
        if key not in self._observations and len(self._observations) >= _MAX_KEYS:
            return
        super().observe(key, value)

    def cost(self, feature: str, model: str, in_tok: int, out_tok: int) -> None:
        in_per_mtok, out_per_mtok = _PRICING.get(model, _PRICING["haiku"])
        usd = in_tok * in_per_mtok / 1e6 + out_tok * out_per_mtok / 1e6
        self._costs_usd[feature] = self._costs_usd.get(feature, 0) + usd
        self._costs_usd["__total__"] = self._costs_usd.get("__total__", 0) + usd

    def event(self, kind: str, **fields) -> None:
        if not self._jsonl_f:
            return
        try:
            line = json.dumps({"t": time.time(), "kind": kind, **fields}, ensure_ascii=False)
            self._jsonl_f.write(line + "\n")
            self._jsonl_f.flush()
        except Exception:
            pass

    def snapshot(self) -> dict:
        snap = super().snapshot()
        snap["costs_usd"] = dict(self._costs_usd)
        return snap

    def reset(self) -> None:
        super().reset()
        self._costs_usd.clear()

    def format_report(self) -> str:
        return _format_report(self.snapshot())


# Module singleton
METRICS = MetricsRegistry()


def timed(key: str):
    """Decorator: records ms latency for sync or async function.

    Delegates the sync/async wrapping logic to biome-mcp's `timed`, pointed at
    our own METRICS registry (duck-typed as a collector: it implements
    `.timer(key)`), so storage/units/reporting stay Unity-specific.
    """
    return _biome_timed(key, collector=METRICS)


def counted(key: str):
    """Decorator: increments counter on each call. See `timed` re: delegation."""
    return _biome_counted(key, collector=METRICS)
