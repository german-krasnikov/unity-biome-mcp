"""Gate 4: Playtest DSL — static validation without Play Mode."""

import pytest

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_lint_playtest_valid_script(conformance_worker):
    """lint_playtest accepts a valid minimal DSL script."""
    worker, bridge = conformance_worker
    resp = await bridge.send("lint_playtest", {"script": "ASSERT_CONSOLE_CLEAN"})
    assert resp["ok"], f"lint_playtest rejected valid script: {resp}"


async def test_lint_playtest_reports_bad_syntax(conformance_worker):
    """lint_playtest classifies an unrecognized DSL verb as an ERROR before any
    Play Mode side effect (PlaytestParser's `default: throw new ArgumentException`
    is caught by PlaytestLinter's parse pass and surfaced as a parse-error ERROR)."""
    worker, bridge = conformance_worker
    resp = await bridge.send("lint_playtest", {"script": "INVALID_COMMAND_XYZ"})
    assert resp["ok"] is False, f"expected lint_playtest to reject an unknown command: {resp}"
    assert "Unknown command: INVALID_COMMAND_XYZ" in resp["err"], f"unexpected classification: {resp}"


async def test_list_playtest_files(conformance_worker):
    """list_playtest_files returns without error."""
    worker, bridge = conformance_worker
    resp = await bridge.send("list_playtest_files", {})
    assert resp["ok"], f"list_playtest_files failed: {resp}"


async def test_lint_playtest_assert_syntax(conformance_worker):
    """lint_playtest accepts ASSERT with path syntax."""
    worker, bridge = conformance_worker
    script = "ASSERT /Main Camera|Camera|m_Enabled == True\nASSERT_CONSOLE_CLEAN"
    resp = await bridge.send("lint_playtest", {"script": script})
    assert resp["ok"], f"lint rejected valid ASSERT: {resp}"
