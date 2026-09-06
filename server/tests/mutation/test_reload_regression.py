"""S3 (byte-stale detection) and S6 (auto-repair) reload regression stories.

See Plans/MUTATION-REGRESSION-MODULE.md section 4 (matrix rows S3/S6) and
Plans/Reviews/build-readiness-implementation-2026-09-06/LIVE-QUALIFICATION.md
(same-size/same-mtime 101->202: a global Refresh gave 0 Csc and kept 101;
typed exact-source import gave Csc/DLL/PDB and DSL 202).
"""
import json
import os

import pytest
from gauntlet.readiness_canary import OwnedFileEdit, render_dsl, target_body
from gauntlet.readiness_dsl_sdk import qualify_receipt
from mcp.server.fastmcp.exceptions import ToolError

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]


def _apply_same_stamp_edit(edit: OwnedFileEdit, value: int) -> None:
    """Write `value`'s body with identical byte length, then force the original mtime back."""
    before_stat = edit.before_stat
    edit.replace(target_body(value))
    os.utime(edit.path, ns=(before_stat.st_atime_ns, before_stat.st_mtime_ns))
    after_stat = edit.path.stat()
    assert after_stat.st_size == before_stat.st_size, "same-size precondition violated"
    assert after_stat.st_mtime_ns == before_stat.st_mtime_ns, "same-mtime precondition violated"
    assert edit.path.read_bytes() != edit.before, "edit did not actually change bytes"


async def test_dsl_baseline_101(owned_canary, mutation_sdk):
    """Positive control: the harness itself (create+compile+DSL) proves 101 before any edit."""
    raw = await mutation_sdk.run_playtest(
        script=render_dsl(owned_canary["object_path"], 101), format="json", timeout=10)
    qualify_receipt(json.loads(raw), expect_failure=False)


async def test_byte_stale_same_metadata_detected(owned_canary, mutation_sdk, tmp_path):
    """S3: a same-size/same-mtime source edit is invisible to Bee but caught by diagnose()."""
    before = await mutation_sdk.diagnose(expected_compile=False)
    assert before == "CLEAN-LIVE", before

    edit = OwnedFileEdit(owned_canary["target_path"], tmp_path)
    _apply_same_stamp_edit(edit, 202)

    after = await mutation_sdk.diagnose(expected_compile=False)
    assert after == "FAIL:stale-dll", after


async def test_byte_stale_auto_repair(owned_canary, mutation_sdk, tmp_path):
    """S6: ordinary public sync_unity() self-heals the same defect via a targeted import."""
    edit = OwnedFileEdit(owned_canary["target_path"], tmp_path)
    _apply_same_stamp_edit(edit, 202)

    result = await mutation_sdk.sync_unity(timeout=120)
    assert result == "sync clean", result  # not MANUAL-REQUIRED / REIMPORT-NEEDED

    raw_202 = await mutation_sdk.run_playtest(
        script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
    qualify_receipt(json.loads(raw_202), expect_failure=False)

    with pytest.raises(ToolError) as exc_info:
        await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 101), format="json", timeout=10)
    qualify_receipt(json.loads(str(exc_info.value)), expect_failure=True)
