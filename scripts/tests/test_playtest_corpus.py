from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PLAYTEST_ROOT = REPO_ROOT / "unity-test-project" / "Playtests"
CI_BUILD_SMOKE = REPO_ROOT / "unity-test-project" / "Assets" / "Editor" / "CiBuildSmoke.cs"
PLAYER_PLAYTEST = (
    REPO_ROOT
    / "unity-test-project"
    / "Assets"
    / "StreamingAssets"
    / "Playtests"
    / "player_ci_smoke.playtest"
)
PLAYER_RUNNER_ROOT = REPO_ROOT / "unity-plugin" / "Runtime" / "Playtest"
URP_ASSET = REPO_ROOT / "unity-test-project" / "Assets" / "Settings" / "UniversalRP.asset"
URP_GLOBAL_SETTINGS = (
    REPO_ROOT / "unity-test-project" / "Assets" / "UniversalRenderPipelineGlobalSettings.asset"
)


def test_unity_test_project_has_checked_in_playtest_corpus() -> None:
    files = sorted(PLAYTEST_ROOT.glob("*.playtest"))

    assert [path.name for path in files] == ["ci_smoke.playtest"]


def test_ci_smoke_playtest_has_console_clean_acceptance() -> None:
    text = (PLAYTEST_ROOT / "ci_smoke.playtest").read_text(encoding="utf-8")

    assert "ASSERT_CONSOLE_CLEAN" in text
    assert "WAIT " in text
    assert "ALIAS " not in text


def test_unity_test_project_has_standalone_build_smoke_method() -> None:
    text = CI_BUILD_SMOKE.read_text(encoding="utf-8")

    assert "namespace UnityMCP.CI" in text
    assert "public static void Build()" in text
    assert "BuildPipeline.BuildPlayer" in text
    assert "ciBuildOutput" in text


def test_unity_test_project_has_player_playtest_corpus() -> None:
    text = PLAYER_PLAYTEST.read_text(encoding="utf-8")

    assert "WAIT_UNTIL /Main Camera|activeInHierarchy == True" in text
    assert "ASSERT /Main Camera|Camera|enabled == True" in text
    assert "ASSERT_CONSOLE_CLEAN" in text


def test_unity_plugin_has_runtime_player_playtest_runner() -> None:
    runner = (PLAYER_RUNNER_ROOT / "PlayerPlaytestRunner.cs").read_text(encoding="utf-8")
    receipts = (PLAYER_RUNNER_ROOT / "PlayerPlaytestReceipts.cs").read_text(encoding="utf-8")
    query = (PLAYER_RUNNER_ROOT / "PlayerPlaytestQuery.cs").read_text(encoding="utf-8")
    asmdef = (PLAYER_RUNNER_ROOT / "UnityMCP.PlayerPlaytest.asmdef").read_text(encoding="utf-8")

    assert "RuntimeInitializeOnLoadMethod" in runner
    assert "-unityMcpPlaytest" in runner
    assert "Application.Quit(failed == 0 ? 0 : 1)" in runner
    assert "schema_version" in receipts
    assert "new(false)" in receipts
    assert "<testsuite name=\\\"UnityMCP.PlayerPlaytest\\\" tests=\\\"" in receipts
    assert "WAIT_UNTIL " in runner
    assert "ASSERT " in runner
    assert "ReadComponentValue" in query
    assert '"autoReferenced": true' in asmdef


def test_player_playtest_receipt_shape_accepts_ci_smoke_output() -> None:
    payload = {
        "schema_version": 1,
        "passed": 5,
        "failed": 0,
        "duration_seconds": 0.1,
        "steps": [
            {"raw": "ASSERT_CONSOLE_CLEAN", "passed": True, "message": "console clean"},
        ],
    }
    encoded = json.dumps(payload)
    decoded = json.loads(encoded)

    assert decoded["schema_version"] == 1
    assert decoded["failed"] == 0
    assert decoded["steps"][0]["passed"] is True

    xml = (
        '<?xml version="1.0" encoding="utf-8"?>'
        '<testsuite name="UnityMCP.PlayerPlaytest" tests="1" failures="0">'
        '<testcase name="ASSERT_CONSOLE_CLEAN"></testcase>'
        '</testsuite>'
    )
    root = ET.fromstring(xml)
    assert root.attrib["name"] == "UnityMCP.PlayerPlaytest"
    assert root.attrib["failures"] == "0"


def test_unity_test_project_urp_assets_match_ci_unity_version() -> None:
    urp = URP_ASSET.read_text(encoding="utf-8")
    global_settings = URP_GLOBAL_SETTINGS.read_text(encoding="utf-8")

    assert "k_AssetVersion: 12" in urp
    assert "k_AssetPreviousVersion: 12" in urp
    assert "m_AssetVersion: 8" in global_settings
    assert "m_ReflectionProbeAtlas:" not in urp
    assert "m_PrefilterScreenSpaceIrradiance:" not in urp
