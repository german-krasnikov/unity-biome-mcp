"""P-098: apply_scene_change validate_references arg threading + state field."""
import time
import pytest
import unity_mcp.tools.transaction as tr


_EDIT_MODE_STATE = "playing:False\npaused:False\ncompiling:False"


async def _ok_send(cmd, args, **kw):
    if cmd == "editor": return _EDIT_MODE_STATE
    if cmd == "batch": return "ok:3"
    if cmd == "validate_references": return "0 broken"
    if cmd == "get_console": return ""
    if cmd == "scene": return "saved"
    return ""


@pytest.fixture(autouse=True)
def patch_send(monkeypatch):
    monkeypatch.setattr(tr, "_send", _ok_send)
    tr._plans.clear()


def _insert_plan(targets: str = "") -> str:
    plan_id = "p098test"
    tr._plans[plan_id] = {
        "goal": "test", "targets": targets, "checkpoint": "cp",
        "created_at": time.time(), "resolved": {},
    }
    return plan_id


class TestValidateRefsArgThreading:
    async def test_apply_passes_target_path_to_validate_refs(self, monkeypatch):
        """P-098: validate_references must receive path when plan has targets."""
        plan_id = _insert_plan(targets="/Root/Player")
        calls: list[tuple] = []

        async def tracking_send(cmd, args, **kw):
            calls.append((cmd, dict(args)))
            return await _ok_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.apply_scene_change(plan_id, "create_object name=A")

        vr_calls = [(c, a) for c, a in calls if c == "validate_references"]
        assert vr_calls, "validate_references was not called"
        assert vr_calls[0][1] == {"path": "/Root/Player"}

    async def test_apply_verify_skips_validate_when_no_targets(self, monkeypatch):
        """P-098: no targets → validate_references called with {} (no regression)."""
        plan_id = _insert_plan(targets="")
        calls: list[tuple] = []

        async def tracking_send(cmd, args, **kw):
            calls.append((cmd, dict(args)))
            return await _ok_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.apply_scene_change(plan_id, "create_object name=A")

        vr_calls = [(c, a) for c, a in calls if c == "validate_references"]
        assert vr_calls, "validate_references was not called"
        assert vr_calls[0][1] == {}


class TestStateField:
    async def test_apply_returns_APPLIED_on_success(self):
        plan_id = _insert_plan()
        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "state=APPLIED" in result

    async def test_apply_returns_ROLLED_BACK_when_batch_reports_rollback(self, monkeypatch):
        plan_id = _insert_plan()

        async def rollback_send(cmd, args, **kw):
            if cmd == "batch": return "ATOMIC_ROLLBACK: reverted ops 0..0\nok:1 err:1"
            return await _ok_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", rollback_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "state=ROLLED_BACK" in result

    async def test_op_zero_nothing_to_revert_is_FAILED_first_line(self, monkeypatch):
        plan_id = _insert_plan()

        async def no_rollback_send(cmd, args, **kw):
            if cmd == "batch":
                return (
                    "[0] err: missing target\n"
                    "ATOMIC_ROLLBACK: op 0 failed, nothing to revert\n"
                    "ok:0 err:1"
                )
            return await _ok_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", no_rollback_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.splitlines()[0] == "state=FAILED"
        assert "state=ROLLED_BACK" not in result

    async def test_real_reverted_prefix_is_ROLLED_BACK_first_line(self, monkeypatch):
        plan_id = _insert_plan()

        async def rollback_send(cmd, args, **kw):
            if cmd == "batch":
                return (
                    "[0] ok\n[1] err: missing target\n"
                    "ATOMIC_ROLLBACK: reverted ops 0..0\n"
                    "ok:1 err:1"
                )
            return await _ok_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", rollback_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.splitlines()[0] == "state=ROLLED_BACK"

    async def test_state_field_is_first_line(self):
        plan_id = _insert_plan()
        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        first_line = result.splitlines()[0]
        assert first_line.startswith("state=")
