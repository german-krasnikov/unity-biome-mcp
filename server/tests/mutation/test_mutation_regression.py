"""S11-S13 mutation-mode lifecycle stories: enable/disable intent, a retained-
instance body patch through the public source-patch write route, and the
ordinary-sync block while patches are active.

See Plans/MUTATION-REGRESSION-MODULE.md matrix rows S11-S13 and
Plans/Reviews/build-readiness-implementation-2026-09-06/LIVE-QUALIFICATION.md
("Actual retained-instance body patch and reload exclusion" -- same instance/
epoch/MVID after a 101->202 patch, BLOCKED in 0.305s while OnReady;
"Explicit disable completed and compiled behavior verified" -- epoch+1, Off,
new game-assembly MVID, DSL 202 recompiled).

S14-S16 (domain-reload restore/patch-persistence stories) live in
test_mutation_regression_restore.py -- split for file-size, not by behavior.
Shared enable/disable/patch flow helpers live in _canary.py.
"""
import json
import time

import pytest
from gauntlet.readiness_canary import render_dsl, target_body
from gauntlet.readiness_dsl_sdk import qualify_receipt
from mcp.server.fastmcp.exceptions import ToolError

from tests.mutation._canary import _disable_mutation_mode, _quiet, _status_field, make_raw_send
from unity_mcp.tools.diagnose import _parse_diagnose
from unity_mcp.tools.sync import _parse_stamp, _parse_status

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]

_BLOCKED_REASON = "BLOCKED: source_patch_OnReady_explicit_disable_required"


async def test_mutation_mode_enable_reports_onready(mutation_sdk):
    """S11: intent starts Off; enabling reports OnReady with the provider
    installed, and the enable itself never touches the sync epoch (only a
    later disable causes a reload)."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    before_intent = await mutation_sdk.editor(action="mutation_mode")
    assert before_intent.strip() == "mutation_mode:false", before_intent
    epoch_before, _, _ = _parse_status(await raw_send("sync_status", {}))

    try:
        await _quiet(mutation_sdk)
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        status = await raw_send("get_status", {})
        assert _status_field(status, "source_patch_state") == "OnReady", status
        assert _status_field(status, "source_patch_provider") == "installed", status
        assert _status_field(status, "source_patch_intent") == "on", status

        epoch_after, _, _ = _parse_status(await raw_send("sync_status", {}))
        assert epoch_after == epoch_before, (epoch_before, epoch_after)
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)


async def test_body_patch_retained_instance_202(owned_canary, mutation_sdk):
    """S12: with mutation ON, an ordinary .cs write is routed to the provider
    and applied in place on the retained assembly -- same sync epoch, same
    game-assembly MVID -- and the DSL observes the new body immediately.
    Negative oracle: the same canary now fails the old (101) expectation."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    try:
        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        epoch_before, _, _ = _parse_status(await raw_send("sync_status", {}))
        mvid_before = _parse_diagnose(await raw_send("diagnose", {})).mvid
        assert mvid_before, "no game-assembly MVID observed before the patch"

        write_result = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=target_body(202).decode("utf-8"))
        assert write_result.startswith("ok:write"), write_result

        raw_202 = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(raw_202), expect_failure=False)

        epoch_after, _, _ = _parse_status(await raw_send("sync_status", {}))
        assert epoch_after == epoch_before, (epoch_before, epoch_after)
        mvid_after = _parse_diagnose(await raw_send("diagnose", {})).mvid
        assert mvid_after == mvid_before, (mvid_before, mvid_after)

        with pytest.raises(ToolError) as exc_info:
            await mutation_sdk.run_playtest(
                script=render_dsl(owned_canary["object_path"], 101), format="json", timeout=10)
        qualify_receipt(json.loads(str(exc_info.value)), expect_failure=True)
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)


async def test_ordinary_sync_blocked_while_active(owned_canary, mutation_sdk):
    """S13: with a patch applied and mutation ON, the ordinary public
    sync_unity() route is denied outright -- typed BLOCKED reason, well
    under a second, epoch and compile stamp both untouched."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    try:
        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        write_result = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=target_body(202).decode("utf-8"))
        assert write_result.startswith("ok:write"), write_result

        status_before = await raw_send("sync_status", {})
        epoch_before, _, _ = _parse_status(status_before)
        stamp_before = _parse_stamp(status_before)

        # sync_unity's own wire command has no advisory-counter entry, so it
        # neither triggers nor needs a _quiet() reset here.
        started = time.monotonic()
        result = await mutation_sdk.sync_unity(timeout=30)
        elapsed = time.monotonic() - started

        assert result == _BLOCKED_REASON, result
        assert elapsed < 1.0, elapsed

        status_after = await raw_send("sync_status", {})
        epoch_after, _, _ = _parse_status(status_after)
        assert epoch_after == epoch_before, (epoch_before, epoch_after)
        assert _parse_stamp(status_after) == stamp_before, (stamp_before, _parse_stamp(status_after))
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)
