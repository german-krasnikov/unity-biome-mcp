"""Unit tests for minimize_repro.py delta-debugger."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from minimize_repro import minimize  # noqa: E402


def _default_criterion(steps: list[dict]) -> bool:
    return any(not s.get("ok", True) for s in steps)


def _make_steps(*records: tuple) -> list[dict]:
    """Build step dicts from (cmd, args, ok) tuples."""
    result = []
    for i, (cmd, args, ok) in enumerate(records, start=1):
        step: dict = {"seq": i, "cmd": cmd, "args": args, "ok": ok}
        if not ok:
            step["err"] = "fail"
        result.append(step)
    return result


def test_minimize_removes_irrelevant_steps():
    """10 steps, only step 7 has ok=False → minimized to 1 step."""
    steps = _make_steps(
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_hierarchy", {}, False),   # only failure
        ("get_status", {}, True),
        ("get_status", {}, True),
        ("get_status", {}, True),
    )
    result = minimize(steps, _default_criterion)
    assert len(result) == 1
    assert result[0]["cmd"] == "get_hierarchy"
    assert result[0]["ok"] is False


def test_minimize_keeps_causal_chain():
    """Custom criterion keeps create + failing read; irrelevant middle step removed."""
    steps = [
        {"seq": 1, "cmd": "create_object", "args": {"name": "foo"}, "ok": True, "data": "created"},
        {"seq": 2, "cmd": "get_status",    "args": {},               "ok": True, "data": "ok"},
        {"seq": 3, "cmd": "get_component", "args": {"path": "/foo"}, "ok": False, "err": "not found"},
    ]

    def criterion(ss: list[dict]) -> bool:
        """Fail only when both a create_object AND the failing get_component are present."""
        has_create = any(s["cmd"] == "create_object" for s in ss)
        fail = next((s for s in ss if not s.get("ok", True)), None)
        if fail is None:
            return False
        return has_create and "foo" in str(fail.get("args", {}))

    result = minimize(steps, criterion)
    assert len(result) == 2
    cmds = {s["cmd"] for s in result}
    assert "create_object" in cmds
    assert "get_component" in cmds


def test_minimize_fail_on_cmd_filter():
    """Only the targeted command's failure counts; other ok=False steps ignored."""
    steps = [
        {"seq": 1, "cmd": "get_status",    "args": {}, "ok": False},  # not target
        {"seq": 2, "cmd": "get_hierarchy",  "args": {}, "ok": False},  # target failure
        {"seq": 3, "cmd": "get_status",    "args": {}, "ok": True},
    ]

    def criterion(ss: list[dict]) -> bool:
        return any(s["cmd"] == "get_hierarchy" and not s.get("ok", True) for s in ss)

    result = minimize(steps, criterion)
    assert len(result) == 1
    assert result[0]["cmd"] == "get_hierarchy"


def test_minimize_empty_trace():
    """Empty input → empty output."""
    result = minimize([], _default_criterion)
    assert result == []


def test_minimize_all_pass():
    """No failures → output equals input unchanged."""
    steps = [
        {"seq": 1, "cmd": "get_status",    "args": {}, "ok": True},
        {"seq": 2, "cmd": "get_hierarchy",  "args": {}, "ok": True},
        {"seq": 3, "cmd": "inspect",        "args": {}, "ok": True},
    ]
    result = minimize(steps, _default_criterion)
    assert result == steps
