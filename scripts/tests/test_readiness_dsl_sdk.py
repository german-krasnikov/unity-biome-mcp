import copy
import json

import pytest
from gauntlet.readiness_canary import FIXTURES
from gauntlet.readiness_dsl_sdk import decode_tool_response, qualify_receipt, run_phase, sdk_session
from mcp.types import CallToolResult


def test_actual_live_expected_assert_error_is_decoded_and_qualified():
    response = CallToolResult.model_validate_json((FIXTURES / "expected-assert-error.json").read_text())
    decoded = json.loads(decode_tool_response("run_playtest", response, expect_failure=True))
    assert decoded["run_id"] == "d17458c1"
    qualify_receipt(decoded, expect_failure=True)


@pytest.mark.parametrize("tool,expected_failure", [("editor", True), ("run_playtest", False)])
def test_error_receipt_is_never_allowed_for_other_tools_or_positive_intent(tool, expected_failure):
    response = CallToolResult.model_validate_json((FIXTURES / "expected-assert-error.json").read_text())
    with pytest.raises(ValueError):
        decode_tool_response(tool, response, expect_failure=expected_failure)


def test_plain_tool_error_is_not_a_successful_negative_control():
    response = CallToolResult(isError=True, content=[{
        "type": "text", "text": "Error executing tool run_playtest: connection lost"}])
    with pytest.raises(ValueError):
        decode_tool_response("run_playtest", response, expect_failure=True)


def receipt(assert_ok=True):
    return {
        "schema_version": 1, "run_id": "owned-run",
        "passed": 2 if assert_ok else 1, "failed": 0 if assert_ok else 1,
        "steps": [
            {"index": 0, "type": "Invoke", "ok": True, "raw_passed": True, "expected_fail": False, "console_errored": False},
            {"index": 1, "type": "Assert", "ok": assert_ok, "raw_passed": assert_ok, "expected_fail": False, "console_errored": False},
        ],
        "outer": {"teardown_ok": True, "scene_clean": True},
    }


@pytest.mark.parametrize("negative", [False, True])
def test_independent_ledger_acceptance_positive_and_real_assert_negative(negative):
    qualify_receipt(receipt(not negative), expect_failure=negative)


@pytest.mark.parametrize("damage", ["empty", "invoke_failed", "wrong_count", "dirty", "inverted", "no_id", "console_error"])
def test_ledger_never_calls_arbitrary_error_expected_red(damage):
    data = copy.deepcopy(receipt(False))
    if damage == "empty":
        data["steps"] = []
    elif damage == "invoke_failed":
        data["steps"][0]["ok"] = False
    elif damage == "wrong_count":
        data["passed"] = 2
    elif damage == "dirty":
        data["outer"]["scene_clean"] = False
    elif damage == "inverted":
        data["steps"][1]["expected_fail"] = True
    elif damage == "console_error":
        data["steps"][1]["console_errored"] = True
    else:
        data["run_id"] = ""
    with pytest.raises(ValueError):
        qualify_receipt(data, expect_failure=True)


@pytest.mark.asyncio
async def test_real_sdk_and_production_registration_preserve_dsl_and_read_before_effect(tmp_path):
    calls = []
    name = "__BiomeReadiness_" + "a" * 32

    async def send(command, args, timeout=0):
        calls.append((command, args, timeout))
        if command == "editor":
            return str(tmp_path) if args["action"] == "project_path" else "playing:False\ncompiling:False\n"
        if command == "get_components_list":
            # Captured live public response: this tool has no ownership metadata.
            return "BuildReadinessCanaryProbe"
        if command == "get_object_detail":
            return (f"name: {name}\nactive: true\ntag: Untagged\nlayer: Default\n"
                    "---\n[Transform]\nm_LocalRotation: (0, 0, 0)\n"
                    "m_LocalPosition: (0, 0, 0)\nm_LocalScale: (1, 1, 1)\n"
                    "---\n[BuildReadinessCanaryProbe]\nObserved: 101")
        if command == "run_playtest":
            return json.dumps(receipt(False))
        raise AssertionError(command)

    async with sdk_session(send) as session:
        result = await run_phase(session, tmp_path, -712, name, 202, True, tmp_path / "evidence")
    assert result["qualified"] is True
    assert [call[0] for call in calls] == ["editor", "editor", "get_object_detail", "run_playtest", "editor"]
    assert calls[2][1] == {"id": -712, "_no_distill": True}
    wire = calls[3][1]
    assert wire["format"] == "json"
    assert "fresh" not in wire
    assert "snapshot_on_failure" not in wire
    assert "INVOKE #-712 BuildReadinessCanaryProbe Sample" in wire["script"]


@pytest.mark.asyncio
async def test_retargeted_instance_id_stops_before_dsl(tmp_path):
    calls = []

    async def send(command, args, timeout=0):
        calls.append(command)
        if command == "editor":
            return str(tmp_path) if args["action"] == "project_path" else "playing:False\ncompiling:False\n"
        return "name: GridTest\nBuildReadinessCanaryProbe\n"

    async with sdk_session(send) as session:
        with pytest.raises(ValueError, match="ownership"):
            await run_phase(session, tmp_path, -712, "__BiomeReadiness_" + "a" * 32, 101, False, tmp_path / "evidence")
    assert "run_playtest" not in calls
