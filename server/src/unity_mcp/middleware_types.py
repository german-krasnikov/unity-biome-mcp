"""Constants and CircuitBreaker for Unity MCP middleware."""
import time


_STRIP_CMDS: frozenset = frozenset({"get_component", "inspect", "get_object_detail"})

BLAST_RADIUS = {
    "get_hierarchy": 0, "get_component": 0, "inspect": 0, "screenshot": 0,
    "query_state": 0, "get_object_detail": 0, "find_objects": 0,
    "set_property": 1, "set_active": 1, "set_material": 1, "set_runtime_property": 1,
    "create_object": 2, "manage_component": 2, "wire_event": 2,
    "delete_object": 3, "scene": 3, "batch": 3,
}

WRITE_CMDS = {
    "set_property", "set_property_delta", "create_object", "delete_object", "manage_component",
    "wire_event", "set_active", "set_material", "set_runtime_property", "set_rect", "move_to",
    "batch", "animation", "timeline", "animator", "particle", "shader",
    "material", "prefab", "scriptable_object", "asset", "scene",
    "create_ui", "execute_code", "menu", "project_settings", "set_parent", "unwire_event",
    "transfer_object", "rename_object", "set_sibling_index",
}

READ_CMDS = {
    # Scene inspection
    "get_hierarchy", "get_component", "inspect", "get_object_detail",
    "get_components_list", "find_objects", "search_scene",
    "query_state", "get_spatial_context", "scan_scene",
    # Console / compile
    "get_console", "get_compile_errors", "validate_references",
    # Screenshots
    "screenshot", "screenshot_compare",
    # Editor state (read-only actions; 'editor' itself handled specially)
    "get_selection", "get_capabilities",
    # Alias / connection / meta
    "alias_status", "get_aliases", "list_connections", "get_enabled_tools",
    "budget_status", "permission_prompt",
    # Testing
    "get_test_results", "get_test_progress", "get_test_count",
    # Profiling / debug
    "get_frame_stats", "get_memory", "get_metrics", "get_perf",
    "get_watches", "debug", "debug_animator", "debug_physics", "profile",
    # Diff / audit / health
    "object_diff", "scene_diff", "scene_health", "material_audit",
    "analyze_lod_culling", "render_analyze", "fingerprint",
    "validate_layout", "check_colliders", "spatial_query",
    # Assets / schema / code (read-only)
    "get_schema", "get_changes",
    "compile_preflight", "await_compile", "auto_fix", "diagnose",
    # Session (listing / loading to memory, no scene mutation)
    "list_scenarios", "list_skills", "list_templates",
    "load_scenario", "load_session",
    # LLM (no scene mutation)
    "ask", "ask_user",
}

# editor actions that are reads; all others (play/stop/pause/step/select) are writes
_EDITOR_READ_ACTIONS: frozenset[str] = frozenset({"state", "project_path"})

# Reads safe to serve from PrefetchCache (both above-circuit and pre-TCP paths).
_READ_CACHEABLE = frozenset({
    "get_component", "get_hierarchy", "get_components_list", "inspect", "get_compile_errors",
})

# Commands that require Play Mode. Blocked by fail-fast guard before TCP when
# is_playing is confirmed False. Derived from CommandRouter registrations (runtime: true).
# Note: fuzz_playtest sends TCP cmd "run_playtest" — not listed here directly.
# Note: watch_remove/clear/reset/get_watches are intentionally excluded (safe outside Play Mode).
_RUNTIME_ONLY_CMDS: frozenset[str] = frozenset({
    "invoke_method", "set_runtime_property",
    "wait_until", "move_to", "query_state", "test_step",
    "run_playtest",
    "get_perf", "get_frame_stats", "debug_animator", "debug_physics",
    "watch_add",
    "profile",
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
        # HALF_OPEN: allow only the first probe request
        if self._probe_in_flight:
            return False
        self._probe_in_flight = True
        return True

    def get_status(self) -> str:
        return ["CLOSED", "OPEN", "HALF_OPEN"][self.state]

    def remaining(self) -> float:
        return max(0.0, self.cooldown - (time.monotonic() - self.opened_at))
