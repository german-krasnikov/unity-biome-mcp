
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

    assert "merge-base --is-ancestor" in text
    assert "::error::No successful conformance run is an ancestor" in text
    assert "exit 1" in text


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
    assert "--profile public-stdio-linux-py314" in text


def test_conformance_workflow_runs_hosted_disposable_unity_workers() -> None:
    text = _workflow("ci-conformance.yml")

    assert "hosted-disposable-unity:" in text
    hosted_block = text[text.index("hosted-disposable-unity:") :]
    assert "runs-on: ${{ matrix.runner }}" in hosted_block[:500]
    assert "runner: ubuntu-latest" in hosted_block
    assert "runner: macos-latest" in hosted_block
    assert "runner: windows-2022" in hosted_block
    assert "buildalon/unity-setup" in hosted_block
    assert "buildalon/activate-unity-license" in hosted_block
    assert "scripts/run_hosted_conformance.py" in hosted_block
    assert "--unity \"${{ steps.unity-setup.outputs.unity-editor-path }}\"" in hosted_block
    assert "--source-project unity-test-project" in hosted_block
    assert "--port-a 9600" in hosted_block
    assert "--port-b 9699" in hosted_block
    assert "UNITY_MCP_ENABLE_BATCHMODE" not in text


def test_unity_tests_workflow_triggers_on_playtest_corpus() -> None:
    text = _workflow("unity-tests.yml")

    assert '"unity-test-project/Playtests/**"' in text


def test_unity_tests_workflow_keeps_editmode_lane_fast() -> None:
    # Scoped to the `test` job specifically (E06a added a sibling `playmode-test`
    # job to this same file that legitimately uses -testFilter for a narrow,
    # separate PlayMode corpus run — see test_ci_playmode_corpus_job.py).
    text = _workflow("unity-tests.yml")
    test_job = text[text.index("\n  test:") : text.index("\n  playmode-test:")]

    assert "Run EditMode Tests" in test_job
    assert "timeout-minutes: 60" in test_job
    assert "-testFilter" not in test_job
    assert "Unity EditMode test run executed zero tests" in test_job
    assert "Build Standalone Player Smoke" not in test_job
    assert "Run Player PlayTest Smoke" not in test_job
    assert "-executeMethod UnityMCP.CI.CiBuildSmoke.Build" not in test_job


def test_unity_tests_workflow_can_optionally_call_player_playtest_lane() -> None:
    text = _workflow("unity-tests.yml")

    assert "run_player_playtest:" in text
    assert "run_player_graphics_smoke:" in text
    assert "default: \"false\"" in text
    assert "inputs.run_player_playtest == 'true' && github.run_id || 'default'" in text
    assert "inputs.run_player_playtest != 'true'" in text
    assert "inputs.run_player_playtest == 'true'" in text
    assert "uses: ./.github/workflows/unity-player-playtest.yml" in text
    assert "run_graphics_smoke: ${{ inputs.run_player_graphics_smoke == 'true' }}" in text


def test_player_playtest_workflow_has_player_ci_timeout_budget() -> None:
    text = _workflow("unity-player-playtest.yml")

    assert "workflow_call:" in text
    assert "run_graphics_smoke:" in text
    assert "description: \"Run optional graphics/UI player smoke. Off by default; core CI stays text-mode.\"" in text
    assert "github.event_name == 'workflow_call' && github.run_id || 'default'" in text
    assert "timeout-minutes: 120" in text


def test_player_playtest_workflow_runs_standalone_build_smoke() -> None:
    text = _workflow("unity-player-playtest.yml")
    build_block = text[text.index("Build Standalone Player Smoke") :]

    assert "Build Standalone Player Smoke" in text
    assert "-executeMethod UnityMCP.CI.CiBuildSmoke.Build" in text
    assert "-ciBuildOutput" in text
    # ff7db948 restored the explicit override: CiBuildSmoke.SelectedScenes()
    # ignores EditorBuildSettings.scenes whenever -ciBuildScene is passed, and
    # without it the Player booted scene[0] (SampleScene) instead of GridTest,
    # breaking player_ci_smoke.playtest. EditorBuildSettings.asset still lists
    # McpFeedbackFixture separately for B22's PlayMode needs.
    assert "-ciBuildScene Assets/Scenes/GridTest.unity" in text
    assert "timeout-minutes: 35" in build_block[:300]
    assert "-quit" in build_block[:500]
    assert "player-playtest-${{ matrix.name }}" in text


def test_player_playtest_workflow_runs_player_playtest_smoke() -> None:
    text = _workflow("unity-player-playtest.yml")
    run_block = text[text.index("Run Player PlayTest Smoke") :]

    assert "Run Player PlayTest Smoke" in text
    assert "timeout-minutes: 5" in run_block[:200]
    assert "-unityMcpPlaytest " in text
    assert "player_ci_smoke.playtest" in text
    assert "-unityMcpPlaytestJson" in text
    assert "-unityMcpPlaytestJunit" in text
    assert "-unityMcpPlaytestExit" in text
    assert "Install Linux virtual display" in text
    assert "xvfb-run --auto-servernum" in text
    assert '"$PLAYER" -force-glcore "${PLAYTEST_ARGS[@]}"' in text
    assert '"$PLAYER" -nographics "${PLAYTEST_ARGS[@]}"' in text
    assert 'find "$PLAYER/Contents/MacOS"' in text
    assert "artifacts/player-playtest.json" in text
    assert "artifacts/player-playtest.xml" in text


def test_player_playtest_workflow_runs_expected_failure_smoke() -> None:
    text = _workflow("unity-player-playtest.yml")
    run_block = text[text.index("Run Player PlayTest Expected Failure Smoke") :]

    assert "Run Player PlayTest Expected Failure Smoke" in text
    assert "timeout-minutes: 5" in run_block[:200]
    assert "player_ci_expected_failure.playtest" in text
    assert "player-playtest-failure.json" in text
    assert "player-playtest-failure.xml" in text
    assert "EXPECTED_FAILURE_RC" in text
    assert 'if [[ "$EXPECTED_FAILURE_RC" -eq 0 ]]' in text
    assert "expected-failure PlayTest unexpectedly passed" in text


def test_player_playtest_workflow_keeps_graphics_smoke_manual_opt_in() -> None:
    text = _workflow("unity-player-playtest.yml")
    core_block = text[text.index("Run Player PlayTest Smoke") : text.index("Validate Player PlayTest Receipts")]
    graphics_block = text[text.index("Run Player Graphics Smoke") :]

    assert "player_ci_graphics_smoke.playtest" not in core_block
    assert "Run Player Graphics Smoke" in text
    assert "if: ${{ inputs.run_graphics_smoke == true || inputs.run_graphics_smoke == 'true' }}" in graphics_block[:250]
    assert "player_ci_graphics_smoke.playtest" in graphics_block
    assert "player-playtest-graphics.json" in graphics_block
    assert "player-playtest-graphics.xml" in graphics_block
    assert '"$PLAYER" "${GRAPHICS_ARGS[@]}"' in graphics_block
    assert '"$PLAYER" -nographics "${GRAPHICS_ARGS[@]}"' not in graphics_block


def test_player_playtest_workflow_triggers_on_text_mode_fixture_changes() -> None:
    text = _workflow("unity-player-playtest.yml")

    assert '"unity-test-project/Assets/Scenes/**"' in text
    assert '"unity-test-project/Assets/Scripts/**"' in text
    assert '"unity-test-project/Assets/StreamingAssets/**"' in text


def test_player_playtest_workflow_validates_player_playtest_receipts() -> None:
    text = _workflow("unity-player-playtest.yml")

    assert "Validate Player PlayTest Receipts" in text
    assert "Validate Player PlayTest Expected Failure Receipts" in text
    assert "player JSON receipt must be UTF-8 without BOM" in text
    assert "expected-failure PlayTest did not fail" in text
    assert "expected-failure PlayTest timeout step was not recorded" in text
    assert "player PlayTest emitted no step receipts" in text
    assert "UnityMCP.PlayerPlaytest" in text
    assert 'suite.get("failures") != "0"' in text
    assert 'suite.get("failures") == "0"' in text

    # B19: header-driven playtest_check.py replaces the hardcoded step-count
    # literals — the two retired blocks must never reappear (retirement gate).
    assert "import playtest_check" in text
    assert "player_ci_smoke.playtest" in text
    assert "player_ci_graphics_smoke.playtest" in text
    assert 'len(payload.get("steps", [])) != 14' not in text
    assert 'len(payload.get("steps", [])) != 4' not in text


def test_player_playtest_workflow_writes_evidence_receipt() -> None:
    text = _workflow("unity-player-playtest.yml")
    evidence_block = text[text.index("Write Player PlayTest Evidence") :]

    assert "scripts/write_player_playtest_evidence.py" in evidence_block[:700]
    assert '--head-sha "${{ github.sha }}"' in evidence_block[:700]
    assert '--run-id "${{ github.run_id }}"' in evidence_block[:700]
    assert '--run-attempt "${{ github.run_attempt }}"' in evidence_block[:700]
    assert '--runner-os "${{ runner.os }}"' in evidence_block[:700]
    assert '--matrix-name "${{ matrix.name }}"' in evidence_block[:700]
    assert '--player-path "${{ matrix.build-output }}"' in evidence_block[:700]
    assert "--artifacts-dir artifacts" in evidence_block[:700]
    assert "--out artifacts/player-playtest-evidence.json" in evidence_block[:700]
    assert "artifacts/player-playtest-evidence.json" in text


def test_conformance_workflow_captures_mcp_monitor_reports() -> None:
    text = _workflow("ci-conformance.yml")

    assert "scripts/monitor_mcp_processes.py --max-version-seconds 60" in text
    assert "Capture MCP monitor before conformance" in text
    assert "Capture MCP monitor after conformance" in text
    assert "Upload MCP monitor reports" in text
    assert "hosted-conformance-${{ matrix.name }}" in text
    assert "tee reports/mcp-monitor-before.json" in text
    assert "tee reports/mcp-monitor-after.json" in text

    after_monitor = text[text.index("Capture MCP monitor after conformance") :]
    assert "if: always()" in after_monitor[:200]


def test_conformance_workflow_fails_closed_instead_of_skip_green() -> None:
    text = _workflow("ci-conformance.yml")

    assert "Unity not reachable" not in text
    assert "steps.reachable.outputs.reachable" not in text
    assert "reachable=false" not in text
    assert "scripts/run_hosted_conformance.py" in text
    assert "--startup-timeout 420" in text
    assert "--timeout 300" in text
    assert "runs-on: self-hosted" not in text
    assert "inputs.project_path" not in text
    assert "CONFORMANCE_PROJECT_PATH" not in text
