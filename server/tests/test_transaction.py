"""Tests for scene_change_plan + apply_scene_change (P1.4 transaction tools)."""
import time
import pytest
import unity_mcp.tools.transaction as tr


async def _default_send(cmd, args, **kw):
    if cmd == "get_compile_errors": return "compile clean"
    if cmd == "get_console": return ""
    if cmd == "resolve_scene_refs": return "OK\t$target\t/Path"
    if cmd == "checkpoint": return "cp_abc123"
    if cmd == "batch": return "3/3 ok"
    if cmd == "validate_references": return "0 broken"
    if cmd == "scene": return "saved"
    return ""


@pytest.fixture(autouse=True)
def patch_send(monkeypatch):
    monkeypatch.setattr(tr, "_send", _default_send)
    tr._plans.clear()


class TestSceneChangePlan:
    async def test_compile_clean(self):
        result = await tr.scene_change_plan("wire tractor unlock")
        assert "plan_id=" in result
        assert "compile=clean" in result
        assert "console_errors=0" in result
        # plan must be stored
        plan_id = next(l.split("=", 1)[1] for l in result.splitlines() if l.startswith("plan_id="))
        assert plan_id in tr._plans

    async def test_compile_error(self, monkeypatch):
        async def bad_send(cmd, args, **kw):
            if cmd == "get_compile_errors": return "error CS0234: bad type"
            return ""
        monkeypatch.setattr(tr, "_send", bad_send)
        result = await tr.scene_change_plan("wire unlock")
        assert result.startswith("FAIL:")
        assert not tr._plans  # plan not created

    async def test_target_miss(self, monkeypatch):
        async def miss_send(cmd, args, **kw):
            if cmd == "get_compile_errors": return "compile clean"
            if cmd == "get_console": return ""
            if cmd == "resolve_scene_refs": return "MISS\t$missing_ref\tnot found"
            if cmd == "checkpoint": return "cp_abc"
            return ""
        monkeypatch.setattr(tr, "_send", miss_send)
        result = await tr.scene_change_plan("wire unlock", targets="$missing_ref")
        assert "FAIL: resolve gate" in result
        assert "plan not created" in result
        assert not tr._plans


class TestApplySceneChange:
    def _insert_plan(self) -> str:
        plan_id = "t3st01"
        tr._plans[plan_id] = {
            "goal": "test goal", "targets": "", "checkpoint": "cp",
            "created_at": time.time(), "resolved": {},
        }
        return plan_id

    async def test_valid_plan(self):
        plan_id = self._insert_plan()
        result = await tr.apply_scene_change(plan_id, "[]")
        assert "mutations=ok" in result
        assert "refs=ok" in result
        assert "console=clean" in result
        assert "saved=true" in result

    async def test_expired_plan(self):
        plan_id = "expired1"
        tr._plans[plan_id] = {
            "goal": "x", "targets": "", "checkpoint": "",
            "created_at": 0, "resolved": {},  # epoch 0 = expired
        }
        result = await tr.apply_scene_change(plan_id, "[]")
        assert "unknown" in result or "expired" in result

    async def test_unknown_plan(self):
        result = await tr.apply_scene_change("nope123", "[]")
        assert "unknown" in result or "expired" in result

    async def test_verify_fails(self, monkeypatch):
        plan_id = self._insert_plan()

        async def broken_send(cmd, args, **kw):
            if cmd == "validate_references": return "5 broken refs"
            if cmd == "batch": return "3/3 ok"
            return ""
        monkeypatch.setattr(tr, "_send", broken_send)

        result = await tr.apply_scene_change(plan_id, "[]")
        assert "BROKEN" in result

    async def test_no_verify(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "batch": return "ok"
            return "saved"
        monkeypatch.setattr(tr, "_send", tracking_send)

        await tr.apply_scene_change(plan_id, "[]", verify=False)
        assert "validate_references" not in calls
        assert calls.count("get_console") == 0

    async def test_no_save(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "validate_references": return "0 broken"
            if cmd == "batch": return "ok"
            return ""
        monkeypatch.setattr(tr, "_send", tracking_send)

        result = await tr.apply_scene_change(plan_id, "[]", save=False)
        assert "scene" not in calls
        assert "saved" not in result

    async def test_verify_clean_refs(self, monkeypatch):
        plan_id = self._insert_plan()

        async def clean_send(cmd, args, **kw):
            if cmd == "validate_references": return "0 broken"
            if cmd == "batch": return "2/2 ok"
            return ""
        monkeypatch.setattr(tr, "_send", clean_send)

        result = await tr.apply_scene_change(plan_id, "[]", save=False)
        assert "refs=ok" in result
