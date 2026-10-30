"""Constants and CircuitBreaker for Unity Biome MCP middleware."""
import time

from .decisions.generated.mutability_logic import IsMutatingInput, is_mutating
from .tools._annotations import _INTERNAL_RETRY_SAFE_CMDS as _KNOWN_SAFE_READS
from .tools.playtest_async import _RUNNING_PHASE_PREFIX as _PLAYTEST_RUNNING_PREFIX
from .tools.tool_specs import _SPECS as _TOOL_SPECS

# get_status/sync_status: pure-read wire commands with no ToolSpec entry
# (audited in tools/_annotations.py). Reused here, not duplicated, so the
# read-only gate and the retry-safety allowlist can never drift apart.

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
    if s.mutability == 'write'
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

# navmesh_query delegates to the conditional C# command name "navmesh".
# Keep the raw transport alias fail-closed as a write-capable command too.
WRITE_CMDS.add("navmesh")

# These commands require write authorization because they create project-local
# artifacts, but they do not change Unity scene state. Keep that distinction
# explicit for scene-cache invalidation and scene-mutation guidance.
# run_playtest / run_playtest_suite execute in Play Mode; Edit Mode scene state
# is unchanged, so they must not count against the consecutive-write guard
# (MCP-GUARD-007).
SCENE_STATE_NEUTRAL_WRITES: frozenset[str] = frozenset({
    "screenshot",
    "run_playtest",
    "run_playtest_suite",
    "start_playtest",           # same Play-mode-neutral scene-state axis as run_playtest
})

# check_verification_needed's advisory every-10th-mutation nudge must count an
# Edit-mode run_playtest as a real scene-state write: C03 wired `MCP
# create_object`/`set_property` steps through CommandRouter.ProcessAsync, so a
# playtest script can now mutate the scene directly, not just verify it.
# transition()'s consecutive-write guard (MCP-GUARD-007),
# _maybe_prefetch_background's cache invalidation, and _reset_write_caches's
# diff-cache reset stay on SCENE_STATE_NEUTRAL_WRITES unchanged — those are
# Play-mode-oriented consumers where treating run_playtest as "verification,
# not a blind write" remains correct (a playtest run typically ends in
# ASSERTs), so the run_playtest classification isn't silently broadened there.
VERIFICATION_NUDGE_NEUTRAL_WRITES: frozenset[str] = (
    SCENE_STATE_NEUTRAL_WRITES - frozenset({"run_playtest", "start_playtest"})
)

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
                                    "find_dependents"}),
    "scene":             frozenset({"list"}),
    "project_settings":  frozenset({"get"}),
    "menu":              frozenset({"list"}),
    "editor":            _EDITOR_READ_ACTIONS,  # same object — no duplication
    "bake":              frozenset({"status", "settings"}),
    "package":           frozenset({"list", "search"}),
    "scene_environment": frozenset({"get"}),
    "uitk_file":         frozenset({"read"}),
    "navmesh":           frozenset({"sample", "path", "raycast", "status", "get_settings"}),
    "navmesh_query":     frozenset({"sample", "path", "raycast", "status", "get_settings"}),
    "profile":           frozenset({"status", "analyze", "compare", "list_sessions"}),
}


def _switch(args: dict, key: str) -> str:
    """absent, off (the literal false: False or "false") or on (any other value) of a switch argument."""
    if key not in args:
        return "absent"
    return "off" if args[key] in (False, "false") else "on"


def is_write(cmd: str, args: dict | None = None, *, unknown_is_write: bool = False) -> bool:
    """Return True iff cmd+args represents a mutation.

    The rule is the DECISION isMutating of dsl/decisions/mutability.logic (printed into
    decisions/generated/mutability_logic.py); this function reads its facts from the registries and the arguments:
    call.base (read, write or unknown: the live sets, which plugin_api.register_read_cmds/register_write_cmds and
    server._warm_cmd_flags change), call.actionRead (args["action"] is one of ACTION_READS[cmd]), and the literals of the
    switch arguments (doctor fix, get_metrics reset, wait_until abort_on_fail, get_changes clear, editor enable).

    A command that is in neither WRITE_CMDS nor READ_CMDS is unknown: the unknown_is_write kwarg decides. False (the
    default) is what every advisory caller gets -- check_retry/check_verification_needed/transition/log_mutation treat an
    unrecognized cmd as a non-write; the worst case is a missed nudge. Read-only AUTHORIZATION callers (check_read_only in
    middleware_guards.py and server.py) pass True: a wire command absent from _SPECS must fail closed, not silently pass
    through, in read-only mode. Unknown/absent action of an action-parameterised write command -> True (conservative).
    """
    args = args or {}
    action = args.get("action", "")
    return is_mutating(IsMutatingInput(
        call_base="read" if cmd in _KNOWN_SAFE_READS else "write" if cmd in WRITE_CMDS else "read" if cmd in READ_CMDS else "unknown",
        call_cmd=str(cmd),
        call_unknown_is_write=unknown_is_write,
        call_action_read=action in ACTION_READS.get(cmd, ()),
        args_action=str(action),
        args_has_enable="enable" in args,
        # doctor is observational by default, but fix=True deletes stale local discovery files: any truthy value is a write.
        args_fix="absent" if "fix" not in args else "on" if args["fix"] else "off",
        args_reset=_switch(args, "reset"),
        args_abort_on_fail=_switch(args, "abort_on_fail"),
        args_clear=_switch(args, "clear"),
    ))


# Commands whose OWN send exception is treated as an uncertain scenario
# outcome (wrapped()'s exception-fencing branch). Distinct from
# SCENE_STATE_NEUTRAL_WRITES: that frozenset answers "does this count as a
# blind write for the advisory guard" (transition()); this answers "can this
# command's outcome make scene-derived caches stale."
PLAYTEST_SCENARIO_CMDS: frozenset[str] = frozenset(
    {"start_playtest", "run_playtest", "run_playtest_suite", "get_playtest_run"}
)


def is_scenario_terminal(cmd: str, result: str) -> bool:
    """True when `result` proves a playtest scenario reached a terminal
    state. run_playtest is one blocking round trip — any response (pass,
    fail, Unity-side error) is already the finished scenario. get_playtest_run
    is the async poll command; "phase=running" is the only non-terminal
    shape. start_playtest only acks dispatch and is deliberately excluded —
    its response never carries an outcome."""
    if cmd == "run_playtest":
        return True
    if cmd == "get_playtest_run":
        return not (result or "").strip().startswith(_PLAYTEST_RUNNING_PREFIX)
    return False


# Reads safe to serve from PrefetchCache (both above-circuit and pre-TCP paths).
# ARC-5 T2 contract: membership here is the SOLE "safe to serve up to TTL
# stale" gate. Any read whose truth can change autonomously — no tracked
# write in this pipeline invalidates it (manual edit, Hot Reload, another
# session, plain wall-clock progress) — must stay OUT of this set, regardless
# of whether any GATE_PRIORS entry currently targets it. get_compile_errors
# is intentionally excluded for exactly this reason (see prefetch_cache.py's
# GATE_PRIORS comment for the paired write-side fix).
_READ_CACHEABLE = frozenset({
    "get_component", "get_hierarchy", "get_components_list", "inspect",
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
