"""Batch integrity tests: summary must match per-command [N] body markers.

Catches REPORT-class seams where C# batch drops commands or miscounts results.
All tests use on_error=continue (default) so result.n == n_sent invariant holds.
"""
from __future__ import annotations

import pytest

from tests.seams.invariants import assert_batch_report_accurate

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_n_commands_produce_n_result_lines(seam_bridge):
    """Batch N read-only commands → exactly N [index] result lines in body."""
    commands = "get_status\nget_compile_errors\nget_hierarchy depth=1"
    result = await assert_batch_report_accurate(seam_bridge, commands)
    assert result.n == 3, f"expected 3 results, got {result.n}"


async def test_error_mid_batch_does_not_drop_subsequent_commands(seam_bridge, seam_worker):
    """Error at [1] must not drop [2]-[N]. All indices must appear in body."""
    name = seam_worker.name("g2")
    try:
        commands = "\n".join([
            f"create_object name={name}",
            "nonexistent_cmd_seam_g2",
            f"set_active path=/{name} active=false",
            f"set_active path=/{name} active=true",
        ])
        result = await assert_batch_report_accurate(seam_bridge, commands)
        assert result.ok_count == 3, f"expected 3 ok, got {result.ok_count}"
        assert result.err_count == 1, f"expected 1 err, got {result.err_count}"
        # Verify [3] set_active=true actually executed — object should be active
        resp = await seam_bridge.send("get_hierarchy", {"depth": 1})
        hier = resp.get("data", "")
        active_lines = [ln for ln in hier.splitlines() if name in ln]
        assert active_lines, f"object {name} missing from hierarchy after batch"
        # Active objects do NOT end with "!" — inactive ones do
        assert not active_lines[0].rstrip().endswith("!"), (
            f"command [3] (set_active=true) was dropped — object is still inactive"
        )
    finally:
        await seam_bridge.send("delete_object", {"path": f"/{name}"})


async def test_unknown_command_in_batch_produces_error(seam_bridge):
    """Unknown command → err:1 in summary; surrounding commands continue."""
    commands = "get_status\ncompletely_unknown_seam_xyz123\nget_compile_errors"
    result = await assert_batch_report_accurate(seam_bridge, commands)
    assert result.err_count == 1, f"unknown command must produce 1 error, got {result.err_count}"
    assert result.ok_count == 2, f"surrounding commands must succeed, got {result.ok_count}"


async def test_all_read_commands_in_batch_succeed(seam_bridge):
    """5 read-only commands in one batch → ok:5 err:0."""
    commands = "\n".join([
        "get_status",
        "get_compile_errors",
        "get_hierarchy depth=1",
        "get_console",
        "get_enabled_tools",
    ])
    result = await assert_batch_report_accurate(seam_bridge, commands)
    assert result.ok_count == 5, f"expected ok:5, got ok:{result.ok_count}"
    assert result.err_count == 0, f"expected err:0, got err:{result.err_count}"


async def test_summary_ok_plus_err_equals_n(seam_bridge):
    """Invariant: ok+err == N for mixed batch (no skip with on_error=continue)."""
    commands = "get_status\nnonexistent_xyz_seam\nget_hierarchy depth=1"
    result = await assert_batch_report_accurate(seam_bridge, commands)
    # assert_batch_report_accurate already asserts result.n == 3 internally
    # Explicit assertion for documentation clarity:
    assert result.n == 3, (
        f"ok({result.ok_count})+err({result.err_count})+skip({result.skip_count}) must equal 3"
    )
