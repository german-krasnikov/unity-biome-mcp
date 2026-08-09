from __future__ import annotations

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]


def _workflow(name: str) -> str:
    return (REPO_ROOT / ".github" / "workflows" / name).read_text(encoding="utf-8")


def test_release_publish_job_depends_on_same_workflow_preflight() -> None:
    text = _workflow("release.yml")

    assert "preflight:" in text
    assert "publish:" in text
    assert "needs: [preflight]" in text or "needs: preflight" in text
    assert "bash scripts/release.sh --preflight" in text


def test_release_preflight_blocks_conformance_sha_mismatch() -> None:
    text = _workflow("release-preflight.yml")
    mismatch_block = text[text.index('if [ "$RUN_SHA" != "$GITHUB_SHA" ]') :]

    assert "::error::Conformance passed on" in mismatch_block
    assert "exit 1" in mismatch_block
    assert "::warning::Conformance passed on" not in mismatch_block


def test_conformance_workflow_triggers_on_all_runtime_surfaces() -> None:
    text = _workflow("ci-conformance.yml")

    for path in (
        "'server/**'",
        "'scripts/**'",
        "'unity-plugin/**'",
        "'unity-plugin-reload/**'",
        "'unity-test-project/**'",
        "'.github/workflows/ci-conformance.yml'",
    ):
        assert path in text


def test_conformance_workflow_runs_tracked_attested_public_profile() -> None:
    text = _workflow("ci-conformance.yml")

    assert "attested-public-stdio:" in text
    assert "scripts/attested_conformance_runner.py" in text
    assert "--policy scripts/gauntlet/release-policy.json" in text
    assert "--profile public-stdio-linux-py312" in text


def test_unity_tests_workflow_triggers_on_playtest_corpus() -> None:
    text = _workflow("unity-tests.yml")

    assert '"unity-test-project/Playtests/**"' in text


def test_unity_tests_workflow_runs_standalone_build_smoke() -> None:
    text = _workflow("unity-tests.yml")

    assert "Build Standalone Player Smoke" in text
    assert "-executeMethod UnityMCP.CI.CiBuildSmoke.Build" in text
    assert "-ciBuildOutput" in text
    assert "player-build-${{ matrix.name }}" in text


def test_conformance_workflow_captures_mcp_monitor_reports() -> None:
    text = _workflow("ci-conformance.yml")

    assert "scripts/monitor_mcp_processes.py --max-version-seconds 60" in text
    assert "Capture MCP monitor before conformance" in text
    assert "Capture MCP monitor after conformance" in text
    assert "Upload MCP monitor reports" in text
    assert "mcp-monitor-single" in text
    assert "mcp-monitor-dual" in text

    after_monitor = text[text.index("Capture MCP monitor after conformance") :]
    assert "if: always()" in after_monitor[:200]
