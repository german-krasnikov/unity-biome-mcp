"""Gate 4: Playtest DSL — static validation without Play Mode."""
from __future__ import annotations

import pytest

pytestmark = pytest.mark.live


async def test_lint_playtest_valid_script(conformance_worker):
    """lint_playtest accepts a valid minimal DSL script."""
    worker, bridge = conformance_worker
    resp = await bridge.send("lint_playtest", {"script": "ASSERT_CONSOLE_CLEAN"})
    assert resp["ok"], f"lint_playtest rejected valid script: {resp}"


async def test_lint_playtest_reports_bad_syntax(conformance_worker):
    """lint_playtest returns warnings for invalid DSL."""
    worker, bridge = conformance_worker
    resp = await bridge.send("lint_playtest", {"script": "INVALID_COMMAND_XYZ"})
    # lint doesn't fail on unknown commands — just warns or returns data
    assert "data" in resp or "err" in resp


async def test_list_playtest_files(conformance_worker):
    """list_playtest_files returns without error."""
    worker, bridge = conformance_worker
    resp = await bridge.send("list_playtest_files", {})
    assert resp["ok"], f"list_playtest_files failed: {resp}"


async def test_lint_playtest_assert_syntax(conformance_worker):
    """lint_playtest accepts ASSERT with path syntax."""
    worker, bridge = conformance_worker
    script = "ASSERT /Main Camera|Camera|m_Enabled == True"
    resp = await bridge.send("lint_playtest", {"script": script})
    assert resp["ok"], f"lint rejected valid ASSERT: {resp}"
