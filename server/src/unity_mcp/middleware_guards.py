"""Write-guard methods for Middleware (mixin)."""
import json
import time

from .decisions.generated.mutationpace_logic import MutationPaceInput, MutationPaceValues, mutation_pace
from .decisions.generated.starvation_logic import StarvationWarnInput, StarvationWarnValues, starvation_warn
from .decisions.generated.taint_logic import TaintWarnInput, TaintWarnValues, taint_warn
from .decisions.generated.writededup_logic import WriteDedupWarnInput, WriteDedupWarnValues, write_dedup_warn
from .decisions.generated.writepace_logic import WritePaceInput, WritePaceValues, write_pace
from .decisions.guard_facts import Verdict, guard_verdict
from .generated.timing_constants import TIMING_ROWS as _TR
from .middleware_types import (
    _RUNTIME_ONLY_CMDS,
    ACTION_READS,
    BLAST_HIGH,
    BLAST_RADIUS,
    READ_CMDS,
    SCENE_STATE_NEUTRAL_WRITES,
    VERIFICATION_NUDGE_NEUTRAL_WRITES,
    is_write,
)
from .utils import parse_kv_line


def _is_batch_readonly(commands: str) -> bool:
    """True iff every non-blank command line in batch text is a read."""
    if not commands:
        return True
    for line in commands.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        cmd, kv = parse_kv_line(line)
        if cmd in READ_CMDS:
            continue  # fast path: known read
        reads = ACTION_READS.get(cmd)
        if reads is not None:
            if kv.get("action", "") in reads:
                continue  # action-based read
            return False   # write action or absent action → conservative
        return False  # pure write cmd or unknown cmd → not readonly
    return True


class MiddlewareGuardsMixin:
    """Guard/validate write operations. Attrs live in Middleware.__init__."""

    # ── Feature 1: Retry Watchdog ─────────────────────────────────────────

    def check_retry(self, cmd: str, args: dict) -> str | None:
        # Captures are filesystem writes for authorization, but identical
        # arguments can intentionally observe a later frame and must remain
        # repeatable.
        is_write_cmd = is_write(cmd, args)
        is_screenshot = cmd == "screenshot"
        if not is_write_cmd or is_screenshot:
            return None
        h = hash((cmd, json.dumps(args, sort_keys=True)))
        now = time.monotonic()
        entry = self._retry_cache.get(h)
        within_ttl, same_generation = False, False
        if entry is not None:
            ts, gen = entry
            within_ttl = now - ts < self._RETRY_TTL
            same_generation = gen == self._retry_generation
        verdict = write_dedup_warn(WriteDedupWarnInput(
            dedup_is_write=True,
            dedup_is_screenshot=False,
            dedup_within_ttl=within_ttl,
            dedup_same_generation=same_generation,
        ))
        if verdict == WriteDedupWarnValues.WARN:
            return (f"⚠ RETRY (within {self._RETRY_TTL:.1f}s): identical {cmd}. "
                    "Re-read state before retrying.")
        self._retry_generation += 1
        self._retry_cache[h] = (now, self._retry_generation)
        self._retry_cache.move_to_end(h)
        while len(self._retry_cache) > self._RETRY_MAX:
            self._retry_cache.popitem(last=False)
        return None

    # ── Feature 3: Taint Tracking ─────────────────────────────────────────

    def check_taint(self, cmd: str, args: dict) -> str | None:
        prop = args.get("prop", "")
        value = args.get("value", "")
        verdict = taint_warn(TaintWarnInput(
            taint_is_set_property=cmd == "set_property",
            taint_prop_ends_with_reference=bool(prop and prop.endswith("Reference")),
            taint_value_is_null=not value or value == "null",
            taint_value_is_special=bool(value and (value.startswith("#") or value.startswith("&"))),
            taint_value_is_clean=value in self._clean_paths,
        ))
        if verdict == TaintWarnValues.WARN:
            return f"⚠ TAINT WARNING: '{value}' was never read. Consider get_hierarchy first."
        return None

    # ── Feature 6: Dead Write Elimination ────────────────────────────────

    def check_dead_write(self, cmd: str, args: dict) -> str | None:
        if cmd != "set_property":
            return None
        key = (args.get("path"), args.get("component"), args.get("prop"))
        prev = self._last_writes.get(key)
        if key in self._last_writes:
            self._last_writes.move_to_end(key)
            self._last_writes[key] = args.get("value")
        else:
            self._last_writes[key] = args.get("value")
            if len(self._last_writes) > self._MAX_WRITES:
                self._last_writes.popitem(last=False)
        if prev is not None:
            return f"⚠ OVERWRITE: {key[2]} was set to '{prev}' without reading. New value: '{args.get('value')}'"
        return None

    def clear_write_on_read(self, cmd: str, args: dict) -> None:
        if cmd in READ_CMDS and args.get("path"):
            path = args["path"]
            for k in [k for k in self._last_writes if k[0] == path]:
                del self._last_writes[k]

    # ── Feature N: Blast Radius Tags ─────────────────────────────────────────

    def check_blast_radius(self, cmd: str, args: dict | None = None) -> str | None:
        if cmd == "batch" and args:
            cmds_text = args.get("commands", "")
            if _is_batch_readonly(cmds_text):
                return None
            write_radii = []
            for l in cmds_text.splitlines():
                s = l.strip()
                if not s or s.startswith("#"):
                    continue
                cmd_name = parse_kv_line(s)[0]
                if cmd_name not in READ_CMDS:
                    write_radii.append(BLAST_RADIUS.get(cmd_name, 1))
            radius = max(write_radii) if write_radii else 1
        else:
            radius = BLAST_RADIUS.get(cmd, 1)
        if radius >= BLAST_HIGH:
            return f"⚠ HIGH BLAST RADIUS ({radius}): '{cmd}' affects multiple objects. Consider checkpoint first."
        return None

    # ── Feature N: Incremental Verification ──────────────────────────────────

    def check_verification_needed(self, cmd: str, args: dict | None = None) -> str | None:
        is_write_cmd = is_write(cmd, args)
        is_neutral = cmd in VERIFICATION_NUDGE_NEUTRAL_WRITES
        is_batch_ro = cmd == "batch" and bool(args) and _is_batch_readonly((args or {}).get("commands", ""))
        if is_write_cmd and not is_neutral and not is_batch_ro:
            self._mutation_count += 1
        verdict = mutation_pace(MutationPaceInput(
            mupace_is_write=is_write_cmd,
            mupace_is_neutral=is_neutral,
            mupace_is_batch_readonly=is_batch_ro,
            mupace_at_checkpoint=self._mutation_count > 0 and self._mutation_count % 10 == 0,
        ))
        if verdict == MutationPaceValues.NUDGE:
            return f"⚡ VERIFICATION CHECKPOINT ({self._mutation_count} mutations): verify state is consistent with goal before continuing."
        return None

    # ── Feature: Play Mode Required Guard ────────────────────────────────────

    def check_play_mode_required(self, cmd: str, args: dict | None = None) -> str | None:
        """Block runtime-only commands when editor is confirmed in edit mode (the DECISION guardVerdict, site py).

        An unknown play state does not block; neither does a playing editor. Gives guard_verdict every site-py fact
        this Middleware owns (not just the play ones): pyReadOnly outranks pyNotInPlay in guards.logic (priority 30
        vs 31), and that must hold regardless of whether this call or check_read_only's runs first — the priority
        lives in the DECISION, not in middleware_pipeline's call order."""
        verdict = guard_verdict("py", **self._py_guard_facts(cmd, args), play_known=self._play_state_known,
                                 play_mode=self.is_playing, runtime=cmd in _RUNTIME_ONLY_CMDS)
        return f"err: '{cmd}' requires Play Mode. Use editor(action='play') first." if verdict == Verdict.NOT_IN_PLAY else None

    # ── Feature: Read-Only Endpoint Guard (P-324) ────────────────────────────

    def _py_guard_facts(self, cmd: str, args: dict | None = None) -> dict:
        """The read-only facts of site py: read_only and mutating.

        is_write asks the registry as an authorization caller does: a command nobody knows is a write (fail
        closed). It is asked only of an endpoint that is read-only, as before: a writable one never looks at the
        call. Shared by check_read_only and check_play_mode_required so both ever see the same read-only facts,
        whichever one guard_verdict is asked by first."""
        mutating = self.is_read_only and is_write(cmd, args or {}, unknown_is_write=True)
        return {"read_only": self.is_read_only, "mutating": mutating}

    def check_read_only(self, cmd: str, args: dict) -> str | None:
        """Block mutation commands when the endpoint is in read-only mode (the DECISION guardVerdict, site py)."""
        verdict = guard_verdict("py", **self._py_guard_facts(cmd, args), play_known=self._play_state_known,
                                 play_mode=self.is_playing, runtime=cmd in _RUNTIME_ONLY_CMDS)
        if verdict == Verdict.READ_ONLY_BLOCKED:
            return f"READ_ONLY_BLOCKED: '{cmd}' is a mutation command; endpoint is read-only"
        return None

    # ── Feature N: Starvation Monitor ────────────────────────────────────────

    def check_starvation(self, result: str) -> str:
        h = hash(result[:200])
        self._response_hashes.append(h)
        verdict = starvation_warn(StarvationWarnInput(
            starv_window_full=len(self._response_hashes) == 5,
            starv_all_same=len(set(self._response_hashes)) == 1,
        ))
        if verdict == StarvationWarnValues.WARN:
            result += "\n⚠ STARVATION: last 5 calls returned same result. Try different approach or re-read state."
        return result

    # ── Feature N: Alive Check ────────────────────────────────────────────────

    def dedup_error(self, cmd: str, result: str) -> str:
        """Collapse a repeated identical error to '(repeated Nx) ...' form."""
        if not result:
            return result
        key = (cmd, result)
        count = self._error_dedup.get(key, 0)
        self._error_dedup[key] = count + 1
        if len(self._error_dedup) > 256:
            self._error_dedup.popitem(last=False)
        if count == 0:
            return result
        return f"(repeated {count + 1}x) {result}"

    def check_alive(self) -> bool:
        return (time.time() - self._last_success) < _TR[0].aliveCheckTtlS

    # ── Feature 12: Workflow Phase FSM ───────────────────────────────────────

    def transition(self, cmd: str, args: dict | None = None) -> str | None:
        changes_scene_state = (
            is_write(cmd, args)
            and cmd not in SCENE_STATE_NEUTRAL_WRITES
            and not (cmd == "batch" and args and _is_batch_readonly((args or {}).get("commands", "")))
        )
        if not changes_scene_state:
            self._consecutive_writes = 0
        else:
            self._consecutive_writes += 1
        verdict = write_pace(WritePaceInput(
            pace_changes_scene_state=changes_scene_state,
            pace_consecutive_writes=self._consecutive_writes,
        ))
        if verdict == WritePaceValues.NUDGE:
            return f"⚡ {self._consecutive_writes} consecutive writes without reading. Consider verifying state."
        return None

    # ── Feature: Batch Conflict Scan ─────────────────────────────────────────

    def scan_batch_conflicts(self, commands: str) -> str | None:
        """Detect conflicts in batch command text. Returns warning or None."""
        if not commands:
            return None
        lines = [l.strip() for l in commands.splitlines() if l.strip()]
        warnings: list[str] = []
        write_keys: dict[tuple, int] = {}  # (path, component, prop) → line index
        deleted_paths: set[str] = set()
        created_names: set[str] = set()

        for i, line in enumerate(lines):
            cmd, kv = parse_kv_line(line)

            if cmd == "set_property":
                key = (kv.get("path"), kv.get("component"), kv.get("prop"))
                if key in write_keys:
                    warnings.append(f"⚠ BATCH: duplicate write to prop '{key[2]}' on {key[0]}")
                else:
                    write_keys[key] = i
                path = kv.get("path", "")
                if path in deleted_paths:
                    warnings.append(f"⚠ BATCH: referencing deleted object '{path}'")

            elif cmd == "create_object":
                created_names.add(kv.get("name", ""))

            elif cmd == "delete_object":
                path = kv.get("path", "")
                obj_id = kv.get("id", "")
                conflict_key = obj_id or path
                name = path.split("/")[-1] if path else ""
                if name and name in created_names:
                    warnings.append(f"⚠ BATCH: create+delete '{name}' is a no-op")
                if conflict_key and conflict_key in deleted_paths:
                    warnings.append(f"⚠ BATCH: double-delete on '{conflict_key}'")
                if conflict_key:
                    deleted_paths.add(conflict_key)

            elif cmd == "manage_component":
                path = kv.get("path", "")
                if path in deleted_paths:
                    warnings.append(f"⚠ BATCH: referencing deleted object '{path}'")

        return "\n".join(warnings) if warnings else None

    # ── Feature: Post-mutation Snapshot Verification ─────────────────────────

    def verify_snapshot(self, result: str, prop: str, value: str) -> str:
        """Parse snapshot in set_property response and verify prop=value was written."""
        has_snapshot = any(l.strip().startswith("[") and l.strip().endswith("]") for l in result.splitlines())
        if not has_snapshot:
            return result
        prop_lower = prop.lower()
        for line in result.splitlines():
            if ": " not in line:
                continue
            key, actual = line.split(": ", 1)
            if key.strip().lower() == prop_lower:
                actual = actual.strip()
                if actual == value:
                    return result + f"\n[VERIFIED: {prop}={value}]"
                return result + f"\n[VERIFY FAIL: expected {value}, got {actual}]"
        return result

    def log_mutation(self, cmd: str, args: dict, result: str) -> None:
        if self._mutation_log and is_write(cmd, args):
            self._mutation_log.write(json.dumps({
                "t": round(time.time(), 2), "cmd": cmd,
                "args": {k: v for k, v in args.items() if v is not None},
                "result": result[:200],
            }, ensure_ascii=False) + "\n")
            self._mutation_log.flush()
