"""Scene mutation transaction — pre-flight + guarded apply (P1.4).

scene_change_plan  — compile/console/target pre-flight, returns plan_id
apply_scene_change — execute batch mutations with post-verify and save
"""
from __future__ import annotations

import hashlib
import re
import time as _time

from ._annotations import RW as _RW
from ._common import bind
from .editor_state import is_play_mode as _is_play_mode
from .editor_state import parse_editor_field as _parse_editor_field

_send = None
_args = None
_plans: dict = {}  # plan_id → plan dict, TTL 600s

# apply_scene_change promises an atomic Unity-Undo transaction. Keep this list
# deliberately narrower than the general batch surface: every command below has
# a direct, source-backed Unity Undo implementation and changes scene state only.
# Asset/file commands, nested batches, and plugin commands must use `batch` (and
# report their own partial side effects) instead of this stronger workflow.
_ATOMIC_SCENE_COMMANDS = frozenset({
    "attach_uitk",
    "auto_wire",
    "autofit_collider",
    "create_object",
    "create_ui",
    "delete_object",
    "manage_component",
    "rename_object",
    "set_active",
    "set_parent",
    "set_property",
    "set_property_delta",
    "set_rect",
    "set_sibling_index",
    "unwire_event",
    "wire_event",
})


def _make_plan_id() -> str:
    return hashlib.md5(f"{_time.time()}".encode()).hexdigest()[:6]


def _cleanup_expired() -> None:
    cutoff = _time.time() - 600
    expired = [k for k, v in _plans.items() if v["created_at"] < cutoff]
    for k in expired:
        del _plans[k]


def _compile_clean(data: str) -> bool:
    d = data.strip().lower()
    return not d or "compile clean" in d or "no compilation errors" in d or d == "no errors"


_BATCH_ROLLBACK_RE = re.compile(
    r"^[ \t]*ATOMIC_ROLLBACK[ \t]*:", re.MULTILINE,
)
_BATCH_NOTHING_TO_ROLLBACK_RE = re.compile(
    r"^[ \t]*ATOMIC_ROLLBACK[ \t]*:[ \t]*"
    r"op[ \t]+0[ \t]+failed,[ \t]+nothing[ \t]+to[ \t]+revert[ \t]*$",
    re.IGNORECASE | re.MULTILINE,
)
_BATCH_ERROR_RE = re.compile(
    r"^[ \t]*(?:\[\d+\][ \t]*)?"
    r"(?:(?i:err(?:or)?|blocked|timeout)[ \t]*:|"
    r"(?:[A-Z][A-Z0-9_-]*[ \t]+)+ERROR[ \t]*:)"
    r"|(?i:\b(?:err|timeout):\s*[1-9]\d*\b)",
    re.MULTILINE,
)
_BATCH_SUMMARY_RE = re.compile(
    r"ok:\s*(\d+)"
    r"(?:\s+err:\s*(\d+))?"
    r"(?:\s+timeout:\s*(\d+))?",
    re.IGNORECASE,
)


def _batch_state(data: str) -> str:
    """Fail-closed classification of the current BatchHelper terminal protocol."""
    data = data or ""
    if _BATCH_NOTHING_TO_ROLLBACK_RE.search(data):
        return "FAILED"
    if _BATCH_ROLLBACK_RE.search(data):
        return "ROLLED_BACK"
    if _BATCH_ERROR_RE.search(data):
        return "FAILED"

    lines = [line.strip() for line in data.splitlines() if line.strip()]
    if not lines:
        return "FAILED"
    summary = _BATCH_SUMMARY_RE.fullmatch(lines[-1])
    if summary is None:
        return "FAILED"

    ok_count = int(summary.group(1))
    err_count = int(summary.group(2) or 0)
    timeout_count = int(summary.group(3) or 0)
    return (
        "APPLIED"
        if ok_count > 0 and err_count == 0 and timeout_count == 0
        else "FAILED"
    )


def _preflight_atomic_commands(commands: str) -> str | None:
    """Return a rejection reason unless every executable line is Undo-safe."""
    executable = []
    rejected = []
    for line_number, raw_line in enumerate((commands or "").splitlines(), start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        # Mirror BatchHelper.ParseLine: only a literal space separates the
        # command token from key=value arguments. A tab-separated line would be
        # an unknown command in Unity, so reject it here too.
        first_space = line.find(" ")
        command = line if first_space < 0 else line[:first_space]
        executable.append(command)
        if command not in _ATOMIC_SCENE_COMMANDS:
            rejected.append(f"line {line_number}: {command}")

    if not executable:
        return "commands contain no executable scene mutations"
    if rejected:
        return (
            "commands are outside the Unity-Undo-safe scene allowlist: "
            + ", ".join(rejected)
            + ". Allowed commands: "
            + ", ".join(sorted(_ATOMIC_SCENE_COMMANDS))
            + ". Use batch for other commands and handle partial side effects explicitly"
        )
    return None


def _fresh_edit_mode_error(data: str | None) -> str | None:
    """Return a fail-closed reason unless a fresh state proves Edit Mode."""
    playing = _parse_editor_field(data, "playing")
    if playing is None:
        return "editor state did not report playing=true|false"
    normalized = playing.lower()
    if normalized == "true":
        return "Play Mode active"
    if normalized != "false":
        return f"unrecognized playing state '{playing}'"
    return None


def _broken_reference_count(data: str) -> int | None:
    """Parse current and legacy validate_references summaries; None means unchecked."""
    current = re.search(r"(\d+)\s+ERROR\b", data or "", re.IGNORECASE)
    if current:
        missing = re.search(r"(\d+)\s+MISSING\b", data or "", re.IGNORECASE)
        return int(current.group(1)) + (int(missing.group(1)) if missing else 0)
    legacy = re.search(r"(\d+)\s+broken\b", data or "", re.IGNORECASE)
    return int(legacy.group(1)) if legacy else None


async def scene_change_plan(
    goal: str,
    targets: str = "",
    dry_run: bool = False,
) -> str:
    """Pre-flight + plan for safe scene edit.
    1. Check Play Mode — reject if playing (mutations blocked)
    2. Check compile clean
    3. Check console for errors
    4. Resolve targets via resolve_scene_refs
    5. Take checkpoint
    6. Return plan_id + baseline status"""
    _cleanup_expired()

    # 1. Play Mode check — scene mutations are blocked during Play Mode
    editor_state = await _send("editor", {"action": "state"})
    if _is_play_mode(editor_state):
        return "FAIL: Play Mode active — exit Play Mode before planning scene changes"

    # 2. Compile check
    compile_data = await _send("get_compile_errors", {})
    if not _compile_clean(compile_data):
        return f"FAIL: compile errors\n{compile_data}"

    # 3. Resolve targets
    resolved: dict[str, str] = {}
    if targets:
        resolve_data = await _send("resolve_scene_refs", {"refs": targets})
        lines = [ln for ln in resolve_data.splitlines() if ln.strip()]
        misses = [ln for ln in lines if not ln.startswith("OK")]
        if misses:
            return "FAIL: resolve gate\n" + "\n".join(misses) + "\nplan not created — fix targets first"
        for line in lines:
            parts = line.split("\t")
            if len(parts) >= 3:
                resolved[parts[1]] = parts[2]

    resolved_str = ""
    if resolved:
        resolved_str = "\nresolved_targets=" + ", ".join(f"{k} => OK {v}" for k, v in resolved.items())

    # dry_run=True: probe only — no checkpoint, no plan stored, no Unity Undo side effects
    if dry_run:
        return f"preflight=clean\ncompile=clean\nconsole_errors=0{resolved_str}\ndry_run=true"

    # 4. Checkpoint
    checkpoint = await _send("checkpoint", {})

    # 5. Store plan
    plan_id = _make_plan_id()
    _plans[plan_id] = {
        "goal": goal, "targets": targets,
        "checkpoint": checkpoint, "created_at": _time.time(), "resolved": resolved,
    }

    return f"plan_id={plan_id}\ngoal={goal}\ncompile=clean\nconsole_errors=0{resolved_str}"


async def apply_scene_change(
    plan_id: str,
    commands: str,
    verify: bool = True,
    save: bool = True,
) -> str:
    """Execute scene mutations with atomic apply, post-verify, and optional save.
    1. Validate plan_id exists and not expired (TTL 600s)
    2. Reject empty input and commands outside the Unity-Undo-safe scene allowlist
    3. Execute an atomic, stop-on-error batch
    4. On batch failure/rollback: stop without verification or save
    5. If verify: require clean references and console before save
    6. If save: save only after a successful batch and verification
    7. Return applied, verified, and saved states separately
    Allowed commands: attach_uitk, auto_wire, autofit_collider, create_object,
    create_ui, delete_object, manage_component, rename_object, set_active,
    set_parent, set_property, set_property_delta, set_rect, set_sibling_index,
    unwire_event, wire_event. Use batch for all other command types."""
    _cleanup_expired()

    if plan_id not in _plans:
        return f"error: unknown or expired plan_id '{plan_id}'"

    rejection = _preflight_atomic_commands(commands)
    if rejection:
        return (
            "state=FAILED\n"
            f"mutations=not attempted ({rejection})\n"
            "verified=false (batch was not sent)\n"
            "saved=false"
        )

    # A plan is only a snapshot. Re-check immediately before transport so an
    # Edit→Play transition between planning and applying cannot mutate runtime
    # state. Missing, malformed, or failed state responses are not proof of
    # Edit Mode and therefore fail closed.
    try:
        editor_state = await _send("editor", {"action": "state"})
        state_error = _fresh_edit_mode_error(editor_state)
    except Exception as exc:
        state_error = f"editor state check failed ({type(exc).__name__})"
    if state_error:
        return (
            "state=FAILED\n"
            f"mutations=not attempted ({state_error})\n"
            "verified=false (batch was not sent)\n"
            "saved=false"
        )

    # Execute batch. BatchHelper's real rollback marker is ATOMIC_ROLLBACK.
    batch_exception = False
    try:
        batch_data = await _send("batch", {
            "commands": commands,
            "atomic": "true",
            "on_error": "stop",
        })
    except Exception as exc:
        batch_exception = True
        batch_data = str(exc)

    state = _batch_state(batch_data or "")
    if batch_exception and state == "APPLIED":
        state = "FAILED"
    if state != "APPLIED":
        return (
            f"state={state}\n"
            f"mutations=failed ({batch_data or 'unknown batch failure'})\n"
            "verified=false (batch did not apply successfully)\n"
            "saved=false"
        )

    refs_status = ""
    console_status = ""
    verification_ok = not verify
    if verify:
        try:
            target_path = _plans[plan_id].get("targets", "")
            vr_args = {"path": target_path} if target_path else {}
            refs_data = await _send("validate_references", vr_args)
            broken = _broken_reference_count(refs_data)
            if broken is None:
                refs_status = "\nrefs=unchecked (unrecognized response)"
            elif broken:
                refs_status = f"\nrefs=BROKEN ({broken} broken)"
            else:
                refs_status = "\nrefs=ok (0 broken)"

            if broken == 0:
                plan = _plans[plan_id]
                since = max(0.0, _time.time() - plan.get("created_at", _time.time()))
                console_data = await _send("get_console", {"level": "error,exception", "since": since})
                console_lines = [ln for ln in console_data.splitlines() if ln.strip()]
                if len(console_lines) == 1 and console_lines[0].strip().lower() == "no logs":
                    console_lines = []
                errs = len(console_lines)
                console_status = f"\nconsole={'clean' if errs == 0 else f'{errs} errors'}"
                verification_ok = errs == 0
        except Exception as e:
            refs_status = f"\nrefs=unchecked ({type(e).__name__})"
            console_status = ""
            verification_ok = False

    verified_status = (
        "\nverified=true" if verify and verification_ok
        else "\nverified=false" if verify
        else "\nverified=skipped"
    )

    # Save only when verification passed, or when the caller explicitly skipped it.
    saved_status = ""
    if save and verification_ok:
        try:
            await _send("scene", {"action": "save"})
            # Verify dirty=false — Unity may not clear synchronously if Undo group is open (P-414)
            status = await _send("get_status", {})
            dirty_after = any(ln.strip() == "dirty=True" for ln in status.splitlines())
            clean_after = any(ln.strip() == "dirty=False" for ln in status.splitlines())
            if dirty_after:
                saved_status = "\nsaved=PARTIAL dirty=true"
            elif clean_after:
                saved_status = "\nsaved=true dirty=false"
            else:
                saved_status = "\nsaved=true dirty=unknown"
        except Exception as e:
            saved_status = f"\nsaved=FAILED ({type(e).__name__})"
    elif save:
        saved_status = "\nsaved=false (verification failed)"
    else:
        saved_status = "\nunsaved=true"

    return (
        f"state={state}\nmutations=ok ({batch_data or ''})"
        f"{refs_status}{console_status}{verified_status}{saved_status}"
    )


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(scene_change_plan)
    mcp.tool(annotations=_RW)(apply_scene_change)
