"""S11-S13 mutation-mode lifecycle stories: enable/disable intent, a retained-
instance body patch through the public source-patch write route, and the
ordinary-sync block while patches are active.

See Plans/MUTATION-REGRESSION-MODULE.md matrix rows S11-S13 and
Plans/Reviews/build-readiness-implementation-2026-09-06/LIVE-QUALIFICATION.md
("Actual retained-instance body patch and reload exclusion" -- same instance/
epoch/MVID after a 101->202 patch, BLOCKED in 0.305s while OnReady;
"Explicit disable completed and compiled behavior verified" -- epoch+1, Off,
new game-assembly MVID, DSL 202 recompiled).
"""
import asyncio
import json
import time

import pytest
from gauntlet.readiness_canary import render_dsl, target_body
from gauntlet.readiness_dsl_sdk import qualify_receipt
from mcp.server.fastmcp.exceptions import ToolError

from tests.mutation._canary import make_raw_send
from unity_mcp.tools.diagnose import _parse_diagnose
from unity_mcp.tools.sync import _parse_stamp, _parse_status

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]

_SETTLE_TIMEOUT_S = 90.0
_SETTLE_POLL_S = 1.0
_BLOCKED_REASON = "BLOCKED: source_patch_OnReady_explicit_disable_required"


def _status_field(status_text: str, key: str) -> str:
    prefix = f"{key}="
    for line in status_text.splitlines():
        if line.startswith(prefix):
            return line[len(prefix):].strip()
    raise AssertionError(f"{key}= missing from status:\n{status_text}")


async def _quiet(mutation_sdk) -> None:
    """Reset the middleware's consecutive-write advisory counter (any read
    call clears it -- MCP-GUARD-007 in middleware_guards.py). owned_canary's
    own setup (sync/create_object/execute_code) already leaves it primed, and
    the advisory only ever prefixes a WRITE command's own result text -- it
    would break this test's exact-string assertions on a write's result (e.g.
    'mutation_mode:true', 'ok:write'). Call this right before any such write."""
    await mutation_sdk.diagnose(expected_compile=False)


async def _wait_source_patch_off(raw_send) -> None:
    """Poll the public get_status route until source_patch_state settles to Off
    (explicit disable's own causal reload has finished and reconciled).
    The disable's reload drops the TCP connection mid-poll; a connection
    error here just means "not observed yet", matching the reconnect-and-
    retry pattern in test_lost_ack_regression.py::_wait_stable."""
    deadline = time.monotonic() + _SETTLE_TIMEOUT_S
    status = ""
    while time.monotonic() < deadline:
        try:
            status = await raw_send("get_status", {})
        except (ConnectionError, OSError):
            await asyncio.sleep(_SETTLE_POLL_S)
            continue
        if _status_field(status, "source_patch_state") == "Off":
            return
        await asyncio.sleep(_SETTLE_POLL_S)
    raise AssertionError(f"source_patch_state never reached Off within {_SETTLE_TIMEOUT_S}s; last={status!r}")


async def _disable_mutation_mode(mutation_sdk, raw_send) -> None:
    """Cleanup: explicit disable, wait for Off, then a clean settle sync.
    Callers wrap this in `finally` so a mid-test assertion failure still
    leaves the shared worker mutation_mode:false and sync-able for the next
    test/fixture teardown that shares this worker."""
    intent = await mutation_sdk.editor(action="mutation_mode")  # read; always hint-free
    if intent.strip() == "mutation_mode:true":
        await mutation_sdk.editor(action="mutation_mode", enable=False)
        await _wait_source_patch_off(raw_send)
    settle = await mutation_sdk.sync_unity(timeout=120)
    assert settle in ("sync clean", "sync clean (no compile needed)"), settle


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
