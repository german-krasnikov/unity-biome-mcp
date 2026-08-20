"""Tests for validate_references UnityEvent overload identity (Subtask 5, REF-035).

Guards that validate_references passes through broken-event entries from Unity,
including distinguishing overloads that share a method name but differ by signature.
"""
from unittest.mock import AsyncMock

import unity_mcp.tools.batch as _mod


def _patch(monkeypatch, response: str):
    monkeypatch.setattr(_mod, "_send", AsyncMock(return_value=response))
    monkeypatch.setattr(_mod, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})


async def test_ref_unity_event_invalid_target_method_flagged(monkeypatch):
    """validate_references response with broken UnityEvent → result reports the broken event path."""
    _patch(monkeypatch, (
        "[ERROR] /Player|EventTrigger|onClick -> MyScript.OnClick (target method missing)\n"
        "1 ERROR, 0 OK"
    ))

    result = await _mod.validate_references(path="/Player")

    assert "onClick" in result
    assert "missing" in result


async def test_ref_multiple_overloads_same_method_name_flagged_separately(monkeypatch):
    """Two broken events with same method name but different signatures → both appear, distinguished."""
    _patch(monkeypatch, (
        "[ERROR] /Button|Button|onClick -> MyScript.SetValue(int) (method not found)\n"
        "[ERROR] /Toggle|Toggle|onValueChanged -> MyScript.SetValue(bool) (method not found)\n"
        "2 ERROR, 0 OK"
    ))

    result = await _mod.validate_references(path="/")

    assert "SetValue(int)" in result
    assert "SetValue(bool)" in result
