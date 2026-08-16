"""Anti-hallucination + speed middleware for Unity Biome MCP.

Enable with env var: UNITY_MCP_MIDDLEWARE=1
Each feature is independent and stateless per Middleware instance.
"""
import atexit
import os
import time
from collections import OrderedDict, deque

from .middleware_async import MiddlewareAsyncMixin
from .middleware_guards import MiddlewareGuardsMixin
from .middleware_paths import PathResolverMixin

# Re-export for backward compat
from .middleware_pipeline import wrap_send  # noqa: F401
from .middleware_reads import MiddlewareReadsMixin
from .middleware_types import (
    _READ_CACHEABLE,
    _STRIP_CMDS,
    BLAST_RADIUS,
    READ_CMDS,
    WRITE_CMDS,
    CircuitBreaker,
)
from .prefetch_cache import PrefetchCache

__all__ = [
    "Middleware", "CircuitBreaker", "wrap_send",
    "WRITE_CMDS", "READ_CMDS", "BLAST_RADIUS", "_STRIP_CMDS", "_READ_CACHEABLE",
]


class Middleware(MiddlewareGuardsMixin, MiddlewareReadsMixin, MiddlewareAsyncMixin, PathResolverMixin):
    """Anti-hallucination + speed + logging features."""

    def __init__(self):
        self._retry_cache: OrderedDict = OrderedDict()  # h -> (timestamp, retry_gen)
        self._retry_generation: int = 0
        self._RETRY_TTL = float(os.environ.get("UNITY_MCP_RETRY_TTL", "5.0"))
        self._RETRY_MAX = 32
        self.confidence: float = 1.0
        self.sampling: SamplingService | None = None  # type: ignore[name-defined]  # noqa: F821
        self._mutation_log = None
        log_dir = os.environ.get("UNITY_MCP_LOG_DIR")
        if log_dir:
            os.makedirs(log_dir, exist_ok=True)
            self._mutation_log = open(os.path.join(log_dir, "mutations.jsonl"), "a", encoding="utf-8")  # noqa: SIM115
            atexit.register(lambda: self._mutation_log.close() if self._mutation_log else None)
        self._clean_paths: OrderedDict = OrderedDict()
        self._MAX_PATHS = 256
        self.call_count: int = 0
        self._last_hierarchy_call: int = 0
        self.known_paths: set = set()
        self.path_to_scene: dict = {}
        self._alias_cache: dict = {}  # name → "path|comp|field" — cleared on reset_session; bounded by scene size
        self.is_playing: bool = False
        self.is_read_only: bool = os.environ.get("UNITY_MCP_READ_ONLY", "0") == "1"
        self._play_state_known: bool = False
        self._play_state_ts: float = 0.0  # timestamp of last non-editor play state update
        self._last_writes: OrderedDict = OrderedDict()
        self._MAX_WRITES = 128
        self._circuit_ready_fn = None
        self.circuit: CircuitBreaker = CircuitBreaker(
            is_ready_fn=lambda: self._circuit_ready_fn and self._circuit_ready_fn()
        )
        self._error_dedup: OrderedDict = OrderedDict()
        self._negative_path_cache: dict = {}
        self._NEGATIVE_PATH_TTL: float = 10.0
        self._response_hashes: deque = deque(maxlen=5)
        self._mutation_count: int = 0
        self._last_success: float = time.time()
        self._consecutive_writes: int = 0
        self.scene_brief: SceneBrief | None = None  # type: ignore[name-defined]  # noqa: F821
        self._component_cache: OrderedDict = OrderedDict()  # path -> {component_names}
        self._MAX_COMPONENTS = 256
        # Tier C features
        self.speculation = None
        self.lessons = None
        self.recorder = None
        self.watchdog = None
        self.session = None
        self.inferrer = None
        self.hinter = None
        # Distiller (Cycle 5b / 5d)
        self._recent_focus: deque = deque(maxlen=8)
        self._distiller_enabled: bool = os.environ.get("UNITY_MCP_DISTILL", "0") == "1"
        self._distiller = None  # lazy init
        self._distill_cache: OrderedDict = OrderedDict()
        self._MAX_DISTILL_CACHE = 64
        self._haiku_in_flight: set = set()
        self._bg_tasks: set = set()  # prevent GC of fire-and-forget tasks
        # Disambiguator (Cycle 5d Item 1)
        self._disambig_enabled: bool = os.environ.get("UNITY_MCP_DISAMBIG", "1") != "0"
        self._disambig = None  # lazy
        # PrefetchCache (Item 1)
        self._prefetch_cache: PrefetchCache | None = (
            PrefetchCache() if os.environ.get("UNITY_MCP_PREFETCH_CACHE", "1") != "0" else None
        )
        # HierarchyDiff (Item 2)
        self._last_hierarchy_full: str | None = None
        self._hierarchy_call_id: int = 0
        # SchemaGuard
        self.schema_cache = None
        self.schema_guard = None
        if os.environ.get("UNITY_MCP_VALIDATE", "1") != "0":
            from .schema_cache import SchemaCache
            from .schema_guard import SchemaGuard
            self.schema_cache = SchemaCache()
            self.schema_guard = SchemaGuard(self, self.schema_cache)

    def invalidate_component_cache(self, path: str) -> None:
        """Drop cached component data for path (call after manage_component).

        Clears both _component_cache and PrefetchCache entries that reference
        this path under any arg key (handles get_components_list using 'id').
        """
        self._component_cache.pop(path, None)
        if self._prefetch_cache is not None:
            self._prefetch_cache.invalidate_by_path(path)

    def get_components_for_path(self, path: str):
        return self._component_cache.get(path)

    def get_known_component_types(self) -> set:
        types: set = set()
        for comps in self._component_cache.values():
            types.update(comps)
        return types

    def reset_session(self) -> None:
        """Drop volatile in-flight state on reconnect."""
        self._retry_cache.clear()
        self._error_dedup.clear()
        self._negative_path_cache.clear()
        self._response_hashes.clear()
        self._last_writes.clear()
        self.is_playing = False
        self._play_state_known = False
        self.circuit = CircuitBreaker(
            is_ready_fn=lambda: self._circuit_ready_fn and self._circuit_ready_fn()
        )
        if self.schema_cache is not None:
            self.schema_cache.invalidate_all()
        self._component_cache.clear()
        self.known_paths.clear()
        self.path_to_scene.clear()
        if self._prefetch_cache is not None:
            self._prefetch_cache.clear()
        self._last_hierarchy_full = None
        self._hierarchy_call_id = 0
        self._last_hierarchy_call = 0
        self._alias_cache = {}
        for task in list(self._bg_tasks):
            task.cancel()
        self._bg_tasks.clear()
