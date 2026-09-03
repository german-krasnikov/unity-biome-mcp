"""Cache for verification-gate predicted reads. Saves RTT by serving cached
results when agent's next call matches a prior write's mandated read.

Pure heuristic, $0 cost, deterministic. Default-on, opt-out via UNITY_MCP_PREFETCH_CACHE=0.
"""

import time
from collections import OrderedDict
from collections.abc import Callable  # noqa: TC003

# Verification gates per CLAUDE.md — write CMD → predicted next read CMD+args
# After write succeeds, fire the predicted read in BACKGROUND, populate cache.
# When agent's next call matches, serve from cache.
GATE_PRIORS: dict[str, Callable[[dict], tuple[str, dict] | None]] = {
    "set_property": lambda a: (
        ("get_component", {"path": a["path"], "type": a["component"]})
        if a.get("path") and a.get("component") else None
    ),
    "set_active": lambda a: (
        ("get_hierarchy", {"summary": "true"})
        if a.get("path") else None
    ),
    "wire_event": lambda a: (
        ("get_component", {"path": a["target"], "type": ""})
        if a.get("target") else None
    ),
    "manage_component": lambda a: (
        ("get_components_list", {"path": a["path"]})
        if a.get("action") == "add" and a.get("path") else None
    ),
    "delete_object":    lambda a: ("get_hierarchy", {"summary": "true"}),
    # ARC-5 T1: "recompile" is intentionally absent. Every entry above acks
    # AFTER its write completes synchronously — its predicted read is valid
    # the instant the ack arrives. "recompile" acks BEFORE Unity has even
    # started compiling (see console.py docstring: "Returns immediately; use
    # await_compile to block until done"). Firing get_compile_errors here
    # races a background prefetch against Unity's actual compile, populating
    # a global-key, TTL-bound cache slot with pre-compile data that
    # await_compile's own post-compile read can then reuse as stale.
}


def _frozen_args(args: dict) -> tuple:
    """Order-independent hashable representation of args dict."""
    return tuple(sorted((k, str(v)) for k, v in args.items() if not k.startswith("_")))


class PrefetchCache:
    """TTL OrderedDict cache. LRU eviction when full. Broad path-based invalidation.

    Invariant: cache ALWAYS expires entries within TTL — staleness bounded.
    """

    def __init__(self, ttl: float = 12.0, max_size: int = 64):
        self._ttl = ttl
        self._max = max_size
        self._store: OrderedDict[tuple, tuple[str, float]] = OrderedDict()
        self._stats = {"hits": 0, "misses": 0, "puts": 0, "evicts": 0, "invals": 0}

    def get(self, cmd: str, args: dict) -> str | None:
        """Returns cached result or None if miss/expired."""
        key = (cmd, _frozen_args(args))
        entry = self._store.get(key)
        if entry is None:
            self._stats["misses"] += 1
            return None
        result, expires = entry
        if time.monotonic() > expires:
            del self._store[key]
            self._stats["misses"] += 1
            return None
        self._store.move_to_end(key)
        self._stats["hits"] += 1
        return result

    def put(self, cmd: str, args: dict, result: str) -> None:
        key = (cmd, _frozen_args(args))
        self._store[key] = (result, time.monotonic() + self._ttl)
        self._store.move_to_end(key)
        self._stats["puts"] += 1
        while len(self._store) > self._max:
            self._store.popitem(last=False)
            self._stats["evicts"] += 1

    def put_synthetic(self, cmd: str, args: dict, result: str, source: str = "reflect-snapshot") -> None:
        """Like put() but tagged. TTL same as regular (self._ttl) — synthetic data may be INCOMPLETE.

        NOTE: For reflect-snapshot source, body may be missing fields not mutated, and field
        names may be lowercased per reflect parser. Agent gets a fast cache hit but should
        verify completeness if specific fields needed.
        """
        key = (cmd, _frozen_args(args))
        tagged = f"[CACHED:{source}]\n{result}"
        self._store[key] = (tagged, time.monotonic() + self._ttl)
        self._store.move_to_end(key)
        self._stats["puts_synthetic"] = self._stats.get("puts_synthetic", 0) + 1
        while len(self._store) > self._max:
            self._store.popitem(last=False)
            self._stats["evicts"] += 1

    def invalidate_path(self, path: str) -> int:
        """Drop ALL cached entries whose args contain matching path. Returns count dropped."""
        if not path:
            return 0
        to_drop = []
        for key in self._store:
            cmd, frozen = key
            for k, v in frozen:
                if k == "path" and v == path:
                    to_drop.append(key)
                    break
        for key in to_drop:
            del self._store[key]
        self._stats["invals"] += len(to_drop)
        return len(to_drop)

    def invalidate_by_path(self, path: str) -> int:
        """Drop ALL entries where any arg value matches path. Broader than invalidate_path.

        Use when the target path may appear under different arg keys (e.g. 'id' for
        get_components_list vs 'path' for get_component). Returns count dropped.
        """
        if not path:
            return 0
        to_drop = []
        for key in self._store:
            _cmd, frozen = key
            for _k, v in frozen:
                if v == path:
                    to_drop.append(key)
                    break
        for key in to_drop:
            del self._store[key]
        self._stats["invals"] += len(to_drop)
        return len(to_drop)

    def clear(self) -> None:
        self._store.clear()

    def stats(self) -> dict:
        return dict(self._stats)
