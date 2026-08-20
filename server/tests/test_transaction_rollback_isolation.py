"""Tests for MCP-TXN-027, MCP-MUT-028, MCP-DIFF-029, MCP-RO-030:
Mutation/transaction rollback isolation and read-only uniform enforcement.
"""
import time
import pytest
from mcp.server.fastmcp.exceptions import ToolError
import unity_mcp.tools.transaction as tr
from unity_mcp.middleware import Middleware


_EDIT_MODE_STATE = "playing:False\npaused:False\ncompiling:False"


async def _default_send(cmd, args, **kw):
    if cmd == "editor": return _EDIT_MODE_STATE
    if cmd == "get_compile_errors": return "compile clean"
    if cmd == "get_console": return ""
    if cmd == "checkpoint": return "cp_abc123"
    if cmd == "batch": return "ok:1"
    if cmd == "validate_references": return "0 broken"
    if cmd == "scene": return "saved"
    if cmd == "get_status": return "dirty=False"
    return ""


@pytest.fixture(autouse=True)
def patch_send(monkeypatch):
    monkeypatch.setattr(tr, "_send", _default_send)
    tr._plans.clear()


def _insert_plan() -> str:
    plan_id = "t3st01"
    tr._plans[plan_id] = {
        "goal": "test goal", "targets": "", "checkpoint": "cp",
        "created_at": time.time(), "resolved": {},
    }
    return plan_id


# ── MCP-TXN-027: Rollback isolation ──────────────────────────────────────────

async def test_apply_scene_change_calls_rollback_on_batch_failure(monkeypatch):
    """Batch exception → state=FAILED, no verify/save calls after failure.

    Note: apply_scene_change relies on Unity-side atomic undo (on_error=stop),
    NOT an explicit Python-side undo command. This test verifies the Python
    side fails fast and surfaces the error (not silent).
    """
    plan_id = _insert_plan()
    calls: list[str] = []

    async def failing_batch_send(cmd, args, **kw):
        calls.append(cmd)
        if cmd == "editor":
            return _EDIT_MODE_STATE
        if cmd == "batch":
            raise ConnectionError("Unity disconnected mid-batch")
        raise AssertionError(f"unexpected call after batch failure: {cmd}")

    monkeypatch.setattr(tr, "_send", failing_batch_send)
    result = await tr.apply_scene_change(plan_id, "create_object name=A")

    assert result.startswith("state=FAILED"), f"Expected FAILED, got: {result}"
    assert "ConnectionError" in result or "disconnected" in result
    assert "validate_references" not in calls
    assert "scene" not in calls


async def test_apply_scene_change_rollback_not_called_on_success(monkeypatch):
    """Batch success → state=APPLIED, mutations=ok. No undo command ever sent."""
    plan_id = _insert_plan()
    calls: list[str] = []

    async def tracking_send(cmd, args, **kw):
        calls.append(cmd)
        return await _default_send(cmd, args, **kw)

    monkeypatch.setattr(tr, "_send", tracking_send)
    result = await tr.apply_scene_change(plan_id, "create_object name=A")

    assert result.startswith("state=APPLIED"), f"Expected APPLIED, got: {result}"
    assert "mutations=ok" in result
    assert "undo" not in calls


# ── MCP-MUT-028: dry_run postcondition truth ──────────────────────────────────

async def test_dry_run_postcondition_truth_sends_no_mutations(monkeypatch):
    """scene_change_plan(dry_run=True) must send zero mutation commands."""
    calls: list[str] = []
    mutation_cmds = {
        "batch", "set_property", "create_object", "delete_object",
        "manage_component", "checkpoint",
    }

    async def tracking_send(cmd, args, **kw):
        calls.append(cmd)
        return await _default_send(cmd, args, **kw)

    monkeypatch.setattr(tr, "_send", tracking_send)
    result = await tr.scene_change_plan("test goal", dry_run=True)

    assert "dry_run=true" in result
    mutation_calls = [c for c in calls if c in mutation_cmds]
    assert mutation_calls == [], f"dry_run sent mutation commands: {mutation_calls}"


# ── MCP-DIFF-029: snapshot/diff GUID identity gap ────────────────────────────

def test_diff_uses_guid_not_path_for_identity():
    """Gap documentation: _plans stores no GUID-indexed snapshot.

    MCP-DIFF-029: cross-session semantic identity via stable GUID is absent.
    A plan created for /OldPath remains valid after rename — no path-vs-GUID
    diff is performed. This test turns RED if a snapshot key is added,
    prompting test #4 to be upgraded to verify actual GUID equivalence.
    """
    plan_id = _insert_plan()
    plan = tr._plans[plan_id]

    # Operational keys are present
    assert "goal" in plan
    assert "checkpoint" in plan
    assert "created_at" in plan
    # Gap: no GUID-indexed snapshot for semantic identity comparison
    assert "snapshot" not in plan
    assert "guid_map" not in plan


# ── MCP-RO-030: Read-only uniform enforcement ─────────────────────────────────

def test_readonly_blocks_manage_component():
    """manage_component must be blocked in read-only mode (gap in existing coverage)."""
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("manage_component", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


async def test_readonly_blocks_all_mutation_tools_not_just_set_property(monkeypatch):
    """is_read_only=True: create_object, delete_object, manage_component all blocked,
    and apply_scene_change surfaces READ_ONLY_BLOCKED from a readonly batch call."""
    mw = Middleware()
    mw.is_read_only = True

    for cmd in ("create_object", "delete_object", "manage_component"):
        result = mw.check_read_only(cmd, {})
        assert result is not None and "READ_ONLY_BLOCKED" in result, (
            f"{cmd} not blocked by read-only middleware"
        )

    # apply_scene_change: _send("batch") raises ToolError (simulating readonly wrap_send)
    plan_id = _insert_plan()

    async def readonly_send(cmd, args, **kw):
        if cmd == "editor":
            return _EDIT_MODE_STATE
        if cmd == "batch":
            raise ToolError("READ_ONLY_BLOCKED: batch is a write command")
        return ""

    monkeypatch.setattr(tr, "_send", readonly_send)
    result = await tr.apply_scene_change(plan_id, "create_object name=A")

    assert result.startswith("state=FAILED"), f"Expected FAILED, got: {result}"
    assert "READ_ONLY_BLOCKED" in result


# ── MCP-TXN-027: Foreign dirty capture in save scope ─────────────────────────

async def test_apply_scene_change_detects_preexisting_dirty_in_receipt(monkeypatch):
    """When editor state reports dirty:True before batch, result contains foreign_dirty:true."""
    plan_id = _insert_plan()

    async def dirty_send(cmd, args, **kw):
        if cmd == "editor":
            return "playing:False\npaused:False\ncompiling:False\ndirty:True"
        return await _default_send(cmd, args, **kw)

    monkeypatch.setattr(tr, "_send", dirty_send)
    result = await tr.apply_scene_change(plan_id, "create_object name=A")

    assert "foreign_dirty:true" in result, f"Expected foreign_dirty:true in result, got:\n{result}"


async def test_apply_scene_change_clean_scene_no_foreign_dirty_flag(monkeypatch):
    """When editor state reports dirty:False before batch, no foreign_dirty in result."""
    plan_id = _insert_plan()

    async def clean_send(cmd, args, **kw):
        if cmd == "editor":
            return "playing:False\npaused:False\ncompiling:False\ndirty:False"
        return await _default_send(cmd, args, **kw)

    monkeypatch.setattr(tr, "_send", clean_send)
    result = await tr.apply_scene_change(plan_id, "create_object name=A")

    assert "foreign_dirty" not in result, f"Expected no foreign_dirty in result, got:\n{result}"


async def test_apply_scene_change_dirty_check_happens_before_batch(monkeypatch):
    """Editor state (dirty check) is called before batch execution."""
    plan_id = _insert_plan()
    call_order: list[str] = []

    async def ordered_send(cmd, args, **kw):
        call_order.append(cmd)
        if cmd == "editor":
            return "playing:False\npaused:False\ncompiling:False\ndirty:True"
        return await _default_send(cmd, args, **kw)

    monkeypatch.setattr(tr, "_send", ordered_send)
    await tr.apply_scene_change(plan_id, "create_object name=A")

    editor_idx = next(i for i, c in enumerate(call_order) if c == "editor")
    batch_idx = next(i for i, c in enumerate(call_order) if c == "batch")
    assert editor_idx < batch_idx, (
        f"editor check (idx={editor_idx}) must precede batch (idx={batch_idx}): {call_order}"
    )
