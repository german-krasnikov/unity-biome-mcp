"""B22a: .suite files stay a Python consumer — restart_between semantics.

Exercises the REAL unity_mcp.server.run_playtest_suite orchestration (not a
hand-rolled loop) against the shipped, unmodified ABC_shared.suite and
I123_isolated.suite files, using the same unity_mcp.server.slot patch pattern
test_run_playtest_suite.py already uses for its hermetic mock_bridge tests —
here with a REAL live bridge instead of a mock.

NOTE (2026-09-04, orchestrator-approved deferral): authored but not executed
this session. Every other live test in this package is built around
unity_state_owner's OwnershipPolicy, scoped to exactly one owned scene
(OWNED_LIVE_SCENE, a per-run GridTest.unity copy) — no existing live test has
ever switched the Editor's active scene to McpFeedbackFixture.unity, which
these DSL files require. The scene open/restore round-trip below is
best-effort and statically reviewed, not empirically verified against the
live ownership-policy teardown path. The B23 gate runs and validates this
lane; a failure there may mean OwnershipPolicy needs an explicit extension
for a second owned scene rather than a bug in this test.

Marker convention note: the prevailing tests/live/ convention is a single
`pytestmark = pytest.mark.live` (16 of 18 existing files) — pytest-asyncio
runs in `asyncio_mode = "auto"` (server/pyproject.toml), so an explicit
`pytest.mark.asyncio` is redundant for async tests here. Following the
majority precedent rather than the "both markers" premise.
"""
from unittest.mock import Mock, patch

import pytest

from tests.live.conftest import OWNED_LIVE_SCENE, _required_live_project
from unity_mcp.server import run_playtest_suite

pytestmark = pytest.mark.live

FIXTURE_SCENE = "Assets/MCPFeedbackFixture/McpFeedbackFixture.unity"
ABC_SUITE = "Assets/MCPFeedbackFixture/PlayTests/ABC_shared.suite"
I123_SUITE = "Assets/MCPFeedbackFixture/PlayTests/I123_isolated.suite"


async def _run_suite_in_fixture_scene(bridge, suite_rel_path: str, restart_between: bool) -> str:
    """Open the fixture scene, run the shipped .suite through the real
    run_playtest_suite tool, then restore OWNED_LIVE_SCENE even on failure."""
    project = _required_live_project()
    await bridge.send("scene", {"action": "open", "path": FIXTURE_SCENE})
    try:
        live_slot = Mock()
        live_slot.bridge = bridge
        with patch("unity_mcp.server.slot", live_slot):
            return await run_playtest_suite(
                suite_path=str(project / suite_rel_path),
                auto_play=True,
                restart_between=restart_between,
                stop_after=True,
            )
    finally:
        await bridge.send("scene", {"action": "open", "path": OWNED_LIVE_SCENE})


async def test_abc_suite_runs_without_restart_accumulates_state(bridge):
    """restart_between=False: A->B->C run in one continuous Play session, the
    same cumulative $state chain PlaytestCorpusPlayModeTests (B22) verifies —
    here through the .suite/run_playtest_suite path instead of direct NUnit."""
    result = await _run_suite_in_fixture_scene(bridge, ABC_SUITE, restart_between=False)
    assert "SUITE: 3/3" in result, result


async def test_i123_suite_with_restart_between_resets_state(bridge):
    """restart_between=True: each of I1/I2/I3 gets a fresh Play session — a
    stale counter carried from a prior file would fail this."""
    result = await _run_suite_in_fixture_scene(bridge, I123_SUITE, restart_between=True)
    assert "SUITE: 3/3" in result, result
