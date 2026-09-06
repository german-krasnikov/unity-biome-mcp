"""Tests for _classify_outcome — pass/fail/error playtest outcome classification."""
import json

from unity_mcp.tools.runtime import _classify_outcome


def _ledger_json(step_ok: bool, teardown_ok: bool = True) -> str:
    """Canonical B16 JSON receipt shape with a single step."""
    return json.dumps({
        "schema_version": 1,
        "run_id": "r1",
        "passed": 1 if step_ok else 0,
        "failed": 0 if step_ok else 1,
        "duration_seconds": 0.1,
        "steps": [{
            "index": 0, "type": "Assert", "ok": step_ok, "ms": 1.0,
            "source_file": "f.playtest", "source_line": 1,
            "raw_passed": step_ok, "expected_fail": False,
        }],
        "outer": {"teardown_ok": teardown_ok, "scene_clean": True},
        "text_report": "whatever",
    })


def test_classify_text_pass():
    assert _classify_outcome("PLAYTEST: 3/3 (1.0s) OK", "text") == "pass"


def test_classify_text_fail():
    assert _classify_outcome("PLAYTEST: 1/3 (1.0s)\n[2] FAIL: HP", "text") == "fail"


def test_classify_text_aborted():
    assert _classify_outcome("PLAYTEST: 1/1 (1.0s)\n[2] ABORTED: timeout", "text") == "fail"


def test_classify_text_error_zero_total():
    assert _classify_outcome("PLAYTEST: 0/0 ERROR: timeout", "text") == "error"


def test_classify_text_empty():
    assert _classify_outcome("", "text") == "error"


def test_classify_text_console_err():
    assert _classify_outcome("PLAYTEST: 2/2 (1.0s)\n[1] CONSOLE_ERR msg", "text") == "fail"


def test_classify_json_pass():
    assert _classify_outcome(_ledger_json(step_ok=True), "json") == "pass"


def test_classify_json_fail():
    assert _classify_outcome(_ledger_json(step_ok=False), "json") == "fail"


def test_classify_json_malformed():
    assert _classify_outcome("not json at all", "json") == "error"


def test_classify_json_empty_steps():
    receipt = json.dumps({
        "passed": 0, "failed": 0, "text_report": "",
        "outer": {"teardown_ok": True}, "steps": [],
    })
    assert _classify_outcome(receipt, "json") == "error"


def test_classify_json_contradictory_aggregate():
    """Aggregate says failed=1 but the only step's own ok is true — the receipt
    disagrees with itself and is uninterpretable, not a genuine fail."""
    receipt = json.dumps({
        "schema_version": 1, "passed": 1, "failed": 1,
        "outer": {"teardown_ok": True, "scene_clean": True},
        "steps": [{
            "index": 0, "type": "Assert", "ok": True, "ms": 1.0,
            "source_file": "f.playtest", "source_line": 1,
            "raw_passed": True, "expected_fail": False,
        }],
    })
    assert _classify_outcome(receipt, "json") == "error"


def test_classify_json_teardown_fail():
    assert _classify_outcome(_ledger_json(step_ok=True, teardown_ok=False), "json") == "fail"


def test_classify_log_ok_with_failing_assert():
    """A trailing OK token must not override a partial ratio + FAIL marker."""
    assert _classify_outcome("PLAYTEST: 1/2 (1.0s) OK\n[2] FAIL", "text") == "fail"


def test_format_per_call_no_module_state():
    """format is a per-call parameter — interleaved json/text calls never bleed
    state into each other."""
    json_receipt = _ledger_json(step_ok=True)
    text_fail = "PLAYTEST: 1/3 (1.0s)\n[2] FAIL"
    assert _classify_outcome(json_receipt, "json") == "pass"
    assert _classify_outcome(text_fail, "text") == "fail"
    assert _classify_outcome(json_receipt, "json") == "pass"
