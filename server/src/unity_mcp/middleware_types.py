"""Constants and CircuitBreaker for Unity Biome MCP middleware."""
import time

from .tools.tool_specs import _SPECS as _TOOL_SPECS

_STRIP_CMDS: frozenset = frozenset({"get_component", "inspect", "get_object_detail"})

BLAST_RADIUS = {
    "get_hierarchy": 0, "get_component": 0, "inspect": 0, "screenshot": 0,
    "query_state": 0, "get_object_detail": 0, "find_objects": 0,
    "set_property": 1, "set_active": 1, "set_material": 1, "set_runtime_property": 1,
    "create_object": 2, "manage_component": 2, "wire_event": 2,
    "delete_object": 4, "execute_code": 5, "scene": 3, "batch": 3,
}

# Derived from _SPECS — tool_specs.py is now the single source of truth.
# Kept as mutable set for:
#   plugin_api.register_read_cmds / register_write_cmds (.update())
#   server._warm_cmd_flags (.update() from C# get_capabilities)
WRITE_CMDS: set[str] = {
    n for n, s in _TOOL_SPECS.items()
    if s.mutability == 'write' and s.category != '_INTERNAL'
}
READ_CMDS: set[str] = {
    n for n, s in _TOOL_SPECS.items()
    if s.mutability == 'read'
}
_RUNTIME_ONLY_CMDS: set[str] = {
    n for n, s in _TOOL_SPECS.items()
    if s.runtime_only
}
# watch_add: raw C# sub-command (not an MCP tool, absent from _SPECS).
# Must remain runtime-only for the play-mode pre-gate in middleware_guards.
_RUNTIME_ONLY_CMDS.add("watch_add")
# set_runtime_property: Python MCP tool removed but C# handler still exists.
# Middleware keeps routing it correctly (Play Mode gate + write classification).
_RUNTIME_ONLY_CMDS.add("set_runtime_property")
WRITE_CMDS.add("set_runtime_property")

# editor actions that are reads; all others (play/stop/pause/step/select) are writes
_EDITOR_READ_ACTIONS: frozenset[str] = frozenset({"state", "project_path"})

# Per-cmd frozenset of action values that are reads.
# Conservative: absent/unknown action → write.
# "editor" included to absorb the _is_batch_readonly special-case.
ACTION_READS: dict[str, frozenset[str]] = {
    "animation":         frozenset({"get", "get_events", "get_clip_path"}),
    "timeline":          frozenset({"get", "get_bindings"}),
    "animator":          frozenset({"get", "get_blend_tree"}),
    "particle":          frozenset({"get"}),
    "shader":            frozenset({"get", "graph_get"}),
    "material":          frozenset({"get", "list_properties", "list_slots", "get_errors", "list_shaders"}),
    "prefab":            frozenset({"get_overrides"}),
    "scriptable_object": frozenset({"get", "list_types", "find"}),
    "asset":             frozenset({"find", "get_info", "validate_move", "get_dependencies",
                                    "find_dependents", "export_package"}),
    "scene":             frozenset({"list"}),
    "project_settings":  frozenset({"get"}),
    "menu":              frozenset({"list"}),
    "editor":            _EDITOR_READ_ACTIONS,  # same object — no duplication
    "bake":              frozenset({"status", "settings"}),
    "package":           frozenset({"list", "search"}),
    "scene_environment": frozenset({"get"}),
}


def is_write(cmd: str, args: dict | None = None) -> bool:
    """Return True iff cmd+args represents a mutation.

    For action-parameterised commands in WRITE_CMDS, check args["action"].
    Unknown/absent action → True (conservative).
    Commands not in WRITE_CMDS are never writes (returns False).
    """
    if cmd not in WRITE_CMDS:
        return False
    reads = ACTION_READS.get(cmd)
    if reads is None:
        return True  # plain write cmd, no action map
    return (args or {}).get("action", "") not in reads


# Reads safe to serve from PrefetchCache (both above-circuit and pre-TCP paths).
_READ_CACHEABLE = frozenset({
    "get_component", "get_hierarchy", "get_components_list", "inspect", "get_compile_errors",
})


class CircuitBreaker:
    CLOSED, OPEN, HALF_OPEN = 0, 1, 2

    def __init__(self, threshold: int = 3, cooldown: float = 15.0, is_ready_fn=None):
        self.state = self.CLOSED
        self.failures = 0
        self.threshold = threshold
        self.cooldown = cooldown
        self.opened_at = 0.0
        self._probe_in_flight: bool = False
        self._is_ready_fn = is_ready_fn

    def record_success(self) -> None:
        self.failures = 0
        self.state = self.CLOSED
        self._probe_in_flight = False

    def release_probe(self) -> None:
        self._probe_in_flight = False

    def record_failure(self) -> None:
        self.failures += 1
        if self.failures >= self.threshold:
            self.state = self.OPEN
            self.opened_at = time.monotonic()

    def allow_request(self) -> bool:
        if self.state == self.CLOSED:
            return True
        if self.state == self.OPEN:
            # Check external readiness signal (e.g. compile state) before time-based cooldown
            if self._is_ready_fn is not None:
                try:
                    if self._is_ready_fn():
                        self.state = self.HALF_OPEN
                        self._probe_in_flight = True
                        return True
                except Exception:
                    pass
            if time.monotonic() - self.opened_at > self.cooldown:
                self.state = self.HALF_OPEN
                self._probe_in_flight = True
                return True
            return False
        # HALF_OPEN: allow all concurrent requests so no request is falsely
        # reported as "Circuit OPEN" while a probe is in flight (P-092).
        return True

    def get_status(self) -> str:
        return ["CLOSED", "OPEN", "HALF_OPEN"][self.state]

    def remaining(self) -> float:
        return max(0.0, self.cooldown - (time.monotonic() - self.opened_at))
