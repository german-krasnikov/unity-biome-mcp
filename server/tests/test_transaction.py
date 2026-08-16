"""Tests for scene_change_plan + apply_scene_change (P1.4 transaction tools)."""
import time
import time as _time
from unittest.mock import AsyncMock

import pytest
import unity_mcp.tools.transaction as tr


_EDIT_MODE_STATE = "playing:False\npaused:False\ncompiling:False"


async def _default_send(cmd, args, **kw):
    if cmd == "get_compile_errors": return "compile clean"
    if cmd == "get_console": return ""
    if cmd == "resolve_scene_refs": return "OK\t$target\t/Path"
    if cmd == "checkpoint": return "cp_abc123"
    if cmd == "batch": return "ok:3"
    if cmd == "validate_references": return "0 broken"
    if cmd == "scene": return "saved"
    if cmd == "editor": return _EDIT_MODE_STATE
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

    async def test_preexisting_console_errors_no_longer_block_plan(self, monkeypatch):
        """WP6: pre-existing console errors must NOT block plan creation."""
        async def noisy_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "get_compile_errors": return "compile clean"
            if cmd == "checkpoint": return "cp_ok"
            return ""

        monkeypatch.setattr(tr, "_send", noisy_send)
        result = await tr.scene_change_plan("wire unlock", dry_run=False)

        assert "plan_id=" in result
        assert tr._plans

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


    async def test_compile_clean_csharp_sentinel(self, monkeypatch):
        """C# sentinel 'No compilation errors' must not block plan creation."""
        async def cs_send(cmd, args, **kw):
            if cmd == "get_compile_errors": return "No compilation errors"
            if cmd == "get_console": return ""
            if cmd == "checkpoint": return "cp_ok"
            return ""
        monkeypatch.setattr(tr, "_send", cs_send)
        result = await tr.scene_change_plan("test goal")
        assert "plan_id=" in result, f"plan not created: {result}"
        assert "compile=clean" in result

    async def test_play_mode_rejected(self, monkeypatch):
        """G20: scene_change_plan must reject when Unity is in Play Mode (C# EditorStateHelper format)."""
        async def playing_send(cmd, args, **kw):
            if cmd == "editor": return "playing:True\npaused:False\ncompiling:False\n"
            return "compile clean"
        monkeypatch.setattr(tr, "_send", playing_send)
        result = await tr.scene_change_plan("mutate scene")
        assert "FAIL" in result.upper()
        assert "play" in result.lower()
        assert not tr._plans

    async def test_compile_clean_csharp_sentinel_with_period(self, monkeypatch):
        """C# sentinel 'No compilation errors.' (period suffix) must also pass."""
        async def cs_send(cmd, args, **kw):
            if cmd == "get_compile_errors": return "No compilation errors."
            if cmd == "get_console": return ""
            if cmd == "checkpoint": return "cp_ok"
            return ""
        monkeypatch.setattr(tr, "_send", cs_send)
        result = await tr.scene_change_plan("test goal")
        assert "plan_id=" in result, f"plan not created: {result}"
        assert "compile=clean" in result

    async def test_dry_run_true_returns_preflight_no_plan_id(self):
        result = await tr.scene_change_plan("wire unlock", dry_run=True)
        assert "preflight=clean" in result
        assert "plan_id=" not in result
        assert "dry_run=true" in result
        assert not tr._plans

    async def test_dry_run_true_does_not_call_checkpoint(self, monkeypatch):
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            return await _default_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.scene_change_plan("goal", dry_run=True)
        assert "checkpoint" not in calls

    async def test_dry_run_true_still_runs_all_preflights(self, monkeypatch):
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            return await _default_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.scene_change_plan("goal", dry_run=True)
        assert "editor" in calls
        assert "get_compile_errors" in calls
        # get_console removed from plan pre-flight (WP6: noise filtering moved to apply-time)

    async def test_dry_run_false_creates_plan_and_checkpoint(self):
        result = await tr.scene_change_plan("wire unlock", dry_run=False)
        assert "plan_id=" in result
        assert tr._plans

    async def test_dry_run_true_resolves_targets_in_response(self):
        result = await tr.scene_change_plan("goal", targets="$target", dry_run=True)
        assert "resolved_targets=" in result
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
        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "mutations=ok" in result
        assert "refs=ok" in result
        assert "console=clean" in result
        assert "verified=true" in result
        assert "saved=true" in result

    async def test_batch_is_atomic_and_stops_on_error(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[tuple[str, dict]] = []

        async def tracking_send(cmd, args, **kw):
            calls.append((cmd, dict(args)))
            return await _default_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.apply_scene_change(plan_id, "create_object name=A")

        batch_args = next(args for cmd, args in calls if cmd == "batch")
        assert [cmd for cmd, _ in calls[:2]] == ["editor", "batch"]
        assert batch_args == {
            "commands": "create_object name=A",
            "atomic": "true",
            "on_error": "stop",
        }

    @pytest.mark.parametrize(
        "batch_data",
        ["", "   \n", "unrecognized batch output", "ok", "ok:0", "1/1 ok"],
    )
    async def test_unrecognized_or_nonpositive_batch_output_fails_closed(
        self, monkeypatch, batch_data
    ):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def adversarial_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor":
                return _EDIT_MODE_STATE
            if cmd == "batch":
                return batch_data
            raise AssertionError(f"unexpected call after unproven batch result: {cmd}")

        monkeypatch.setattr(tr, "_send", adversarial_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.startswith("state=FAILED")
        assert "verified=false" in result
        assert "saved=false" in result
        assert calls == ["editor", "batch"]

    def test_batch_state_accepts_only_current_positive_terminal_summary(self):
        assert tr._batch_state("created A\nok:1") == "APPLIED"
        assert tr._batch_state("ok:2 err:0 timeout:0") == "APPLIED"
        assert tr._batch_state("This is an error: ordinary prose\nok:1") == "APPLIED"
        assert (
            tr._batch_state("DRY-RUN RESOLVE ERROR: missing target\nok:1")
            == "FAILED"
        )
        assert tr._batch_state("ok:1 err:0 timeout:1") == "FAILED"

    @pytest.mark.parametrize("commands", ["", "  \n# comment only\n\t"])
    async def test_empty_or_comment_only_commands_are_rejected_before_send(
        self, monkeypatch, commands
    ):
        plan_id = self._insert_plan()
        send = AsyncMock()
        monkeypatch.setattr(tr, "_send", send)

        result = await tr.apply_scene_change(plan_id, commands)

        assert result.startswith("state=FAILED")
        assert "mutations=not attempted" in result
        assert "no executable scene mutations" in result
        assert "verified=false" in result
        assert "saved=false" in result
        send.assert_not_awaited()

    async def test_unsafe_mixed_batch_is_rejected_whole_before_send(self, monkeypatch):
        """A file write followed by a failure can never be labelled rolled back."""
        plan_id = self._insert_plan()
        send = AsyncMock()
        monkeypatch.setattr(tr, "_send", send)
        commands = (
            "create_object name=A\n"
            "uitk_file action=create_uss path=Assets/UI/A.uss content=x\n"
            "missing_plugin_command value=1"
        )

        result = await tr.apply_scene_change(plan_id, commands)

        assert result.startswith("state=FAILED")
        assert "uitk_file" in result
        assert "missing_plugin_command" in result
        assert "ROLLED_BACK" not in result
        send.assert_not_awaited()

    @pytest.mark.parametrize("command", ["batch", "execute_code", "asset", "prefab"])
    async def test_non_scene_or_nested_commands_are_rejected(
        self, monkeypatch, command
    ):
        plan_id = self._insert_plan()
        send = AsyncMock()
        monkeypatch.setattr(tr, "_send", send)

        result = await tr.apply_scene_change(plan_id, f"{command} value=x")

        assert result.startswith("state=FAILED")
        assert command in result
        send.assert_not_awaited()

    async def test_preflight_matches_batch_literal_space_command_grammar(
        self, monkeypatch
    ):
        plan_id = self._insert_plan()
        send = AsyncMock()
        monkeypatch.setattr(tr, "_send", send)

        result = await tr.apply_scene_change(plan_id, "create_object\tname=A")

        assert result.startswith("state=FAILED")
        assert "create_object\tname=A" in result
        send.assert_not_awaited()

    async def test_expired_plan(self):
        plan_id = "expired1"
        tr._plans[plan_id] = {
            "goal": "x", "targets": "", "checkpoint": "",
            "created_at": 0, "resolved": {},  # epoch 0 = expired
        }
        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "unknown" in result or "expired" in result

    async def test_unknown_plan(self):
        result = await tr.apply_scene_change("nope123", "[]")
        assert "unknown" in result or "expired" in result

    async def test_verify_fails(self, monkeypatch):
        plan_id = self._insert_plan()

        async def broken_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "validate_references": return "5 broken refs"
            if cmd == "batch": return "ok:3"
            return ""
        monkeypatch.setattr(tr, "_send", broken_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "BROKEN" in result
        assert "verified=false" in result
        assert "saved=false (verification failed)" in result

    async def test_no_verify(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            return "saved"
        monkeypatch.setattr(tr, "_send", tracking_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", verify=False
        )
        assert "validate_references" not in calls
        assert calls.count("get_console") == 0
        assert "verified=skipped" in result

    async def test_no_save(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "validate_references": return "0 broken"
            if cmd == "batch": return "ok:1"
            return ""
        monkeypatch.setattr(tr, "_send", tracking_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", save=False
        )
        assert "scene" not in calls
        assert "unsaved=true" in result

    async def test_save_failure_reported_in_response(self, monkeypatch):
        plan_id = self._insert_plan()

        async def failing_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:2"
            if cmd == "validate_references": return "0 broken"
            if cmd == "get_console": return ""
            if cmd == "scene": raise TimeoutError("TCP timeout")
            return ""
        monkeypatch.setattr(tr, "_send", failing_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "saved=FAILED (TimeoutError)" in result
        assert "state=APPLIED" in result

    async def test_no_save_flag_reports_unsaved(self, monkeypatch):
        plan_id = self._insert_plan()

        async def tracking_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            if cmd == "get_console": return ""
            return ""
        monkeypatch.setattr(tr, "_send", tracking_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", save=False
        )
        assert "unsaved=true" in result

    async def test_save_success_confirmed_by_response(self, monkeypatch):
        plan_id = self._insert_plan()

        async def ok_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            if cmd == "get_console": return ""
            if cmd == "scene": return "ok saved"
            return ""
        monkeypatch.setattr(tr, "_send", ok_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "saved=true" in result

    async def test_verify_clean_refs(self, monkeypatch):
        plan_id = self._insert_plan()

        async def clean_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "validate_references": return "0 broken"
            if cmd == "batch": return "ok:2"
            return ""
        monkeypatch.setattr(tr, "_send", clean_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", save=False
        )
        assert "refs=ok" in result

    async def test_apply_graceful_on_validate_error(self, monkeypatch):
        # G5: apply_scene_change must not propagate exception from validate_references
        plan_id = self._insert_plan()

        async def err_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:2"
            if cmd == "validate_references": raise Exception("NullReferenceException: verify phase")
            if cmd == "scene": return "saved"
            return ""
        monkeypatch.setattr(tr, "_send", err_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")
        assert "mutations=ok" in result
        assert "refs=unchecked" in result
        assert "saved=false (verification failed)" in result

    async def test_batch_rollback_stops_before_verify_and_save(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def rollback_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor":
                return _EDIT_MODE_STATE
            if cmd == "batch":
                return "[0] ok\n[1] err: missing target\nATOMIC_ROLLBACK: reverted ops 0..0\nok:1 err:1"
            raise AssertionError(f"unexpected call after rollback: {cmd}")

        monkeypatch.setattr(tr, "_send", rollback_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.startswith("state=ROLLED_BACK")
        assert "verified=false" in result
        assert "saved=false" in result
        assert calls == ["editor", "batch"]

    async def test_batch_failure_without_rollback_is_not_reported_as_applied(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def failed_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor":
                return _EDIT_MODE_STATE
            if cmd == "batch":
                return "[0] err: READ_ONLY_BLOCKED\nok:0 err:1"
            raise AssertionError(f"unexpected call after failure: {cmd}")

        monkeypatch.setattr(tr, "_send", failed_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.startswith("state=FAILED")
        assert calls == ["editor", "batch"]

    async def test_multiline_handler_error_is_classified_as_failed(self, monkeypatch):
        """A warning line cannot hide a later handler-returned err: line."""
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def failed_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor":
                return _EDIT_MODE_STATE
            if cmd == "batch":
                return (
                    "[0] warn: import emitted a warning\n"
                    "err: USS import failed\n"
                    "ok:0 err:1"
                )
            raise AssertionError(f"unexpected call after failure: {cmd}")

        monkeypatch.setattr(tr, "_send", failed_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert result.startswith("state=FAILED")
        assert "ROLLED_BACK" not in result
        assert calls == ["editor", "batch"]

    async def test_console_errors_block_save(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def console_error_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 ERROR, 4 OK"
            if cmd == "get_console": return "NullReferenceException: boom"
            raise AssertionError(f"save must not run after console failure: {cmd}")

        monkeypatch.setattr(tr, "_send", console_error_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert "console=1 errors" in result
        assert "saved=false (verification failed)" in result
        assert "scene" not in calls

    async def test_unrecognized_reference_response_blocks_save(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def unknown_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "validation finished"
            raise AssertionError(f"no later call expected: {cmd}")

        monkeypatch.setattr(tr, "_send", unknown_send)
        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert "refs=unchecked (unrecognized response)" in result
        assert "saved=false (verification failed)" in result
        assert calls == ["editor", "batch", "validate_references"]

    async def test_dirty_flag_verified_partial_after_save(self, monkeypatch):
        """P-414: saved=PARTIAL dirty=true when scene stays dirty post-save."""
        plan_id = self._insert_plan()

        async def fake_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            if cmd == "get_console": return ""
            if cmd == "scene": return "Assets/Scenes/Test.unity"  # save "succeeds"
            if cmd == "get_status": return "scene=Test\ndirty=True\nplaying=False"
            return ""
        monkeypatch.setattr(tr, "_send", fake_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", save=True
        )
        assert "saved=PARTIAL dirty=true" in result, f"Expected PARTIAL but got: {result}"

    async def test_dirty_flag_verified_clean_after_save(self, monkeypatch):
        """P-414: saved=true dirty=false when scene is clean after save."""
        plan_id = self._insert_plan()

        async def fake_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            if cmd == "get_console": return ""
            if cmd == "scene": return "Assets/Scenes/Test.unity"
            if cmd == "get_status": return "scene=Test\ndirty=False\nplaying=False"
            return ""
        monkeypatch.setattr(tr, "_send", fake_send)

        result = await tr.apply_scene_change(
            plan_id, "create_object name=A", save=True
        )
        assert "saved=true dirty=false" in result, f"Expected clean but got: {result}"

    @pytest.mark.parametrize(
        "editor_state",
        [
            "playing:True\npaused:False\ncompiling:False",
            "",
            "state: editing",
            "playing:maybe",
            "err: disconnected",
        ],
    )
    async def test_fresh_editor_state_blocks_apply_before_batch(
        self, monkeypatch, editor_state
    ):
        """A valid Edit-Mode plan cannot be applied after state changes or goes unknown."""
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def state_send(cmd, args, **kw):
            calls.append(cmd)
            if cmd == "editor":
                return editor_state
            raise AssertionError(f"batch/verify/save must not run: {cmd}")

        monkeypatch.setattr(tr, "_send", state_send)
        result = await tr.apply_scene_change(plan_id, "set_parent path=/A parent=/B")

        assert result.startswith("state=FAILED")
        assert "mutations=not attempted" in result
        assert "verified=false (batch was not sent)" in result
        assert "saved=false" in result
        assert calls == ["editor"]

    async def test_fresh_editor_state_exception_blocks_apply(self, monkeypatch):
        plan_id = self._insert_plan()
        calls: list[str] = []

        async def failed_state(cmd, args, **kw):
            calls.append(cmd)
            raise ConnectionError("Unity disconnected")

        monkeypatch.setattr(tr, "_send", failed_state)
        result = await tr.apply_scene_change(plan_id, "set_parent path=/A parent=/B")

        assert result.startswith("state=FAILED")
        assert "editor state check failed (ConnectionError)" in result
        assert calls == ["editor"]


class TestConsoleNoiseFiltering:
    """WP6: scene_change_plan must ignore pre-existing console noise."""

    def _insert_plan_with_mark(self) -> str:
        plan_id = "wp6test"
        tr._plans[plan_id] = {
            "goal": "test", "targets": "", "checkpoint": "cp",
            "created_at": _time.time(), "resolved": {},
        }
        return plan_id

    async def test_scene_change_plan_ignores_preexisting_console_noise(self, monkeypatch):
        """Plan must be created even when get_console returns 1000 error lines."""
        async def noisy_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "get_compile_errors": return "compile clean"
            if cmd == "get_console": return "\n".join(f"[Error] old noise {i}" for i in range(1000))
            if cmd == "checkpoint": return "cp_ok"
            if cmd == "resolve_scene_refs": return ""
            return ""
        monkeypatch.setattr(tr, "_send", noisy_send)

        result = await tr.scene_change_plan("wire unlock", dry_run=False)

        assert "plan_id=" in result, f"plan not created despite noisy console: {result}"
        assert tr._plans

    async def test_scene_change_plan_no_console_tcp_call(self, monkeypatch):
        """scene_change_plan must NOT call get_console at all."""
        calls: list[str] = []

        async def tracking_send(cmd, args, **kw):
            calls.append(cmd)
            return await _default_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.scene_change_plan("goal", dry_run=False)

        assert "get_console" not in calls, f"get_console was called: {calls}"

    async def test_apply_uses_time_scoped_console_check(self, monkeypatch):
        """apply_scene_change must pass since= to get_console (time-scoped, not all-time)."""
        plan_id = self._insert_plan_with_mark()
        console_args_seen: list[dict] = []

        async def tracking_send(cmd, args, **kw):
            if cmd == "get_console":
                console_args_seen.append(dict(args))
            return await _default_send(cmd, args, **kw)

        monkeypatch.setattr(tr, "_send", tracking_send)
        await tr.apply_scene_change(plan_id, "create_object name=A")

        assert console_args_seen, "get_console was not called during verify"
        assert "since" in console_args_seen[0], (
            f"get_console called without 'since' arg: {console_args_seen[0]}"
        )

    async def test_apply_clean_with_preexisting_noise(self, monkeypatch):
        """Pre-plan noise ignored; zero new errors after apply → verified=true, saved=true."""
        plan_id = self._insert_plan_with_mark()

        async def noisy_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            # Simulated: no NEW errors since plan creation
            if cmd == "get_console": return ""
            if cmd == "scene": return "saved"
            if cmd == "get_status": return "dirty=False"
            return ""
        monkeypatch.setattr(tr, "_send", noisy_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert "console=clean" in result
        assert "verified=true" in result

    async def test_apply_detects_new_errors_via_since(self, monkeypatch):
        """New errors that appear after plan creation must block save."""
        plan_id = self._insert_plan_with_mark()

        async def new_error_send(cmd, args, **kw):
            if cmd == "editor": return _EDIT_MODE_STATE
            if cmd == "batch": return "ok:1"
            if cmd == "validate_references": return "0 broken"
            # One new error appeared after plan creation
            if cmd == "get_console": return "NullReferenceException: just happened"
            raise AssertionError(f"save must not run after new error: {cmd}")
        monkeypatch.setattr(tr, "_send", new_error_send)

        result = await tr.apply_scene_change(plan_id, "create_object name=A")

        assert "console=1 errors" in result
        assert "saved=false (verification failed)" in result
