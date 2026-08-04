"""Telemetry/metrics registry. In-memory counters + timers + cost tracking.

Thread-unsafe by design — relies on CPython GIL for tiny dict[str]+=1 ops.
Optional jsonl event log when UNITY_MCP_METRICS=1 + UNITY_MCP_LOG_DIR set.
"""
import atexit
import inspect
import json
import os
import statistics
import time
from collections import defaultdict, deque
from collections.abc import Iterator
from contextlib import contextmanager

from .metrics_report import _p95
from .metrics_report import format_report as _format_report

# Re-exports for backward compatibility
__all__ = [
    "MetricsRegistry", "METRICS", "timed", "counted",
    "HAIKU_IN_PER_MTOK", "HAIKU_OUT_PER_MTOK", "_p95",
]

# Haiku pricing (per 1M tokens)
HAIKU_IN_PER_MTOK = 0.80
HAIKU_OUT_PER_MTOK = 4.00


class MetricsRegistry:
    """Process-wide metrics. inc() / observe() / timer() / cost() / event()."""

    def __init__(self):
        self._counters: defaultdict[str, int] = defaultdict(int)
        self._observations: defaultdict[str, deque] = defaultdict(lambda: deque(maxlen=256))
        self._costs_usd: defaultdict[str, float] = defaultdict(float)
        self._jsonl_f = None
        self._start_ts = time.time()
        self._setup_sink()

    def _setup_sink(self) -> None:
        if os.environ.get("UNITY_MCP_METRICS") != "1":
            return
        log_dir = os.environ.get("UNITY_MCP_LOG_DIR")
        if not log_dir:
            return
        try:
            os.makedirs(log_dir, exist_ok=True)
            path = os.path.join(log_dir, "metrics.jsonl")
            self._jsonl_f = open(path, "a", encoding="utf-8")
            atexit.register(self._close)
        except Exception:
            self._jsonl_f = None

    def _close(self) -> None:
        if self._jsonl_f:
            try:
                self._jsonl_f.close()
            except Exception:
                pass
            self._jsonl_f = None

    def inc(self, key: str, n: int = 1) -> None:
        # Hard cap on unique keys to prevent unbounded growth on hostile/dynamic cmds
        if key not in self._counters and len(self._counters) >= 512:
            return
        self._counters[key] += n

    def observe(self, key: str, value: float) -> None:
        if key not in self._observations and len(self._observations) >= 512:
            return
        self._observations[key].append(value)

    @contextmanager
    def timer(self, key: str) -> Iterator[None]:
        t0 = time.perf_counter()
        try:
            yield
        finally:
            self.observe(key, (time.perf_counter() - t0) * 1000.0)

    def cost(self, feature: str, model: str, in_tok: int, out_tok: int) -> None:
        usd = in_tok * HAIKU_IN_PER_MTOK / 1e6 + out_tok * HAIKU_OUT_PER_MTOK / 1e6
        self._costs_usd[feature] += usd
        self._costs_usd["__total__"] += usd

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
        return {
            "uptime_s": time.time() - self._start_ts,
            "counters": dict(self._counters),
            "observations": {
                k: {
                    "n": len(v),
                    "p50": statistics.median(v) if v else 0,
                    "p95": _p95(v) if len(v) >= 20 else (max(v) if v else 0),
                    "avg": statistics.mean(v) if v else 0,
                }
                for k, v in self._observations.items()
            },
            "costs_usd": dict(self._costs_usd),
        }

    def reset_counter(self, key: str) -> None:
        """Reset a single counter to zero. Useful in tests for baseline isolation."""
        self._counters[key] = 0

    def reset(self) -> None:
        self._counters.clear()
        self._observations.clear()
        self._costs_usd.clear()
        self._start_ts = time.time()

    def snapshot_and_reset(self) -> dict:
        """Atomic snapshot + reset to avoid losing concurrent writes."""
        snap = self.snapshot()
        self.reset()
        return snap

    def format_report(self) -> str:
        return _format_report(self.snapshot())


# Module singleton
METRICS = MetricsRegistry()


def timed(key: str):
    """Decorator: records ms latency for sync or async function."""
    def decorator(fn):
        if inspect.iscoroutinefunction(fn):
            async def awrapper(*args, **kwargs):
                with METRICS.timer(key):
                    return await fn(*args, **kwargs)
            return awrapper
        else:
            def wrapper(*args, **kwargs):
                with METRICS.timer(key):
                    return fn(*args, **kwargs)
            return wrapper
    return decorator


def counted(key: str):
    """Decorator: increments counter on each call."""
    def decorator(fn):
        if inspect.iscoroutinefunction(fn):
            async def awrapper(*args, **kwargs):
                METRICS.inc(key)
                return await fn(*args, **kwargs)
            return awrapper
        else:
            def wrapper(*args, **kwargs):
                METRICS.inc(key)
                return fn(*args, **kwargs)
            return wrapper
    return decorator
