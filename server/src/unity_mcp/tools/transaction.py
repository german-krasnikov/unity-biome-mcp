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

_send = None
_args = None
_plans: dict = {}  # plan_id → plan dict, TTL 600s


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


async def scene_change_plan(
    goal: str,
    targets: str = "",
    dry_run: bool = True,
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

    # 2. Console check
    console_data = await _send("get_console", {"level": "error,exception"})
    console_errors = sum(1 for ln in console_data.splitlines() if ln.strip())

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

    # 4. Checkpoint
    checkpoint = await _send("checkpoint", {})

    # 5. Store plan
    plan_id = _make_plan_id()
    _plans[plan_id] = {
        "goal": goal, "targets": targets,
        "checkpoint": checkpoint, "created_at": _time.time(), "resolved": resolved,
    }

    resolved_str = ""
    if resolved:
        resolved_str = "\nresolved_targets=" + ", ".join(f"{k} => OK {v}" for k, v in resolved.items())

    return f"plan_id={plan_id}\ngoal={goal}\ncompile=clean\nconsole_errors={console_errors}{resolved_str}"


async def apply_scene_change(
    plan_id: str,
    commands: str,
    verify: bool = True,
    save: bool = True,
) -> str:
    """Execute scene mutations with pre-check, post-verify, and optional save.
    1. Validate plan_id exists and not expired (TTL 600s)
    2. Execute batch commands
    3. If verify: validate_references + console check
    4. If save: save scene
    5. Return mutation summary"""
    _cleanup_expired()

    if plan_id not in _plans:
        return f"error: unknown or expired plan_id '{plan_id}'"

    # Execute batch
    batch_data = await _send("batch", {"commands": commands})

    # Verify — G5: wrap in try/except so NullRef from validate_references doesn't surface
    refs_status = console_status = ""
    if verify:
        try:
            target_path = _plans[plan_id].get("targets", "")
            vr_args = {"path": target_path} if target_path else {}
            refs_data = await _send("validate_references", vr_args)
            m = re.search(r"(\d+)\s+broken", refs_data or "")
            broken = int(m.group(1)) if m else 0
            refs_status = f"\nrefs={'ok (0 broken)' if broken == 0 else f'BROKEN ({broken} broken)'}"

            console_data = await _send("get_console", {"level": "error,exception"})
            errs = sum(1 for ln in console_data.splitlines() if ln.strip())
            console_status = f"\nconsole={'clean' if errs == 0 else f'{errs} errors'}"
        except Exception as e:
            refs_status = f"\nrefs=unchecked ({type(e).__name__})"
            console_status = ""

    # Save
    saved_status = ""
    if save:
        try:
            await _send("scene", {"action": "save"})
            saved_status = "\nsaved=true"
        except Exception as e:
            saved_status = f"\nsaved=FAILED ({type(e).__name__})"
    else:
        saved_status = "\nunsaved=true"

    state = "ROLLED_BACK" if "<rollback>" in (batch_data or "").lower() else "APPLIED"
    return f"state={state}\nmutations=ok ({batch_data or ''}){refs_status}{console_status}{saved_status}"


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(scene_change_plan)
    mcp.tool(annotations=_RW)(apply_scene_change)
