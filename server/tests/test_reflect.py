"""Tests for Asymmetric Reflection layer (reflect package).

Tests use mock response strings — no real Unity bridge required.
"""
import os
import pytest_asyncio
from unittest.mock import AsyncMock

from unity_mcp.reflect import reflect, Mismatch, _RULES, _registry_count
from unity_mcp.metrics import METRICS


# ── helpers ──────────────────────────────────────────────────────────────────

def _snap(fields: dict) -> str:
    """Build a fake response string with a snapshot block."""
    lines = ["ok: property set", "---"]
    for k, v in fields.items():
        lines.append(f"  {k}: {v}")
    return "\n".join(lines)


async def _dummy_send(cmd, args, timeout=30.0):
    return "ok"


# ── 1. set_property happy path ────────────────────────────────────────────────

async def test_set_property_match():
    resp = _snap({"health": "100"})
    result = await reflect("set_property", {"prop": "health", "value": "100"}, resp, _dummy_send)
    assert result is None


# ── 2. set_property mismatch ─────────────────────────────────────────────────

async def test_set_property_mismatch():
    resp = _snap({"health": "99"})
    result = await reflect("set_property", {"prop": "health", "value": "100"}, resp, _dummy_send)
    assert isinstance(result, Mismatch)
    assert "health" in result.msg.lower() and "99" in result.msg and "100" in result.msg


# ── 3. float tolerance — should NOT mismatch ─────────────────────────────────

async def test_set_property_float_tolerance():
    resp = _snap({"speed": "0.9999998"})
    result = await reflect("set_property", {"prop": "speed", "value": "1.0"}, resp, _dummy_send)
    assert result is None


# ── 4. vector tolerance ───────────────────────────────────────────────────────

async def test_set_property_vector_tolerance():
    resp = _snap({"position": "(1.0000001, 2, 3)"})
    result = await reflect(
        "set_property",
        {"prop": "transform.position", "value": "(1,2,3)"},
        resp,
        _dummy_send,
    )
    assert result is None


# ── 5. dry_run skipped ────────────────────────────────────────────────────────

async def test_set_property_dry_run_skipped():
    resp = _snap({"health": "0"})
    result = await reflect(
        "set_property",
        {"prop": "health", "value": "100", "dry_run": "true"},
        resp,
        _dummy_send,
    )
    assert result is None


# ── 6. response contains "Failed" → skip ─────────────────────────────────────

async def test_set_property_failed_skipped():
    resp = "Failed: property not found\n---\nhealth: 0"
    result = await reflect("set_property", {"prop": "health", "value": "100"}, resp, _dummy_send)
    assert result is None


# ── 7. no snapshot block → None (silent — cannot verify) ─────────────────────

async def test_set_property_no_snapshot():
    # When C# can't find the object (no --- block), we cannot verify — stay silent
    resp = "ok: property set"
    result = await reflect("set_property", {"prop": "health", "value": "100"}, resp, _dummy_send)
    assert result is None


# ── 8. set_active match ───────────────────────────────────────────────────────

async def test_set_active_match():
    resp = "ok active=true\n---\nname: /Player"
    result = await reflect("set_active", {"active": "true"}, resp, _dummy_send)
    assert result is None


# ── 9. set_active mismatch ────────────────────────────────────────────────────

async def test_set_active_mismatch():
    # asked to activate, but response says inactive
    resp = "ok active=false\n---\nname: /Player"
    result = await reflect("set_active", {"active": "true"}, resp, _dummy_send)
    assert isinstance(result, Mismatch)


# ── 10. create_object wrong parent ────────────────────────────────────────────

async def test_create_object_wrong_parent():
    # created under /Root but expected under /Canvas
    resp = "Created Cube at /Root/Cube"
    result = await reflect(
        "create_object",
        {"name": "Cube", "parent": "/Canvas"},
        resp,
        _dummy_send,
    )
    assert isinstance(result, Mismatch)


# ── 11. create_object path ends with correct name ────────────────────────────

async def test_create_object_in_subtree():
    resp = "Created Cube at /Canvas/Cube"
    result = await reflect(
        "create_object",
        {"name": "Cube", "parent": "/Canvas"},
        resp,
        _dummy_send,
    )
    assert result is None


# ── 12. no rule for unknown cmd ───────────────────────────────────────────────

async def test_no_rule_for_unknown_cmd():
    before = METRICS._counters.get("reflect.skipped_no_rule", 0)
    result = await reflect("unknown_xyz_cmd", {"foo": "bar"}, "response", _dummy_send)
    assert result is None
    assert METRICS._counters.get("reflect.skipped_no_rule", 0) == before + 1


# ── 13. rule crash → silent None + counter ────────────────────────────────────

async def test_rule_crashed():
    from unity_mcp.reflect import register_rule

    @register_rule("__crash_test__")
    async def _bad_rule(args, response, send_fn):
        raise RuntimeError("boom")

    before = METRICS._counters.get("reflect.rule_crashed", 0)
    result = await reflect("__crash_test__", {}, "resp", _dummy_send)
    assert result is None
    assert METRICS._counters.get("reflect.rule_crashed", 0) == before + 1

    # Cleanup: remove the test rule so it doesn't pollute other tests
    del _RULES["__crash_test__"]


# ── 14. batch dispatch — one failing sub-op ──────────────────────────────────

async def test_batch_dispatch():
    # Two sub-ops: first has matching health=100, second has health=50 but expected 99
    batch_resp2 = (
        "[1] ok set_property health=100\n"
        "---\n"
        "health: 100\n"
        "---end---\n"
        "[2] ok set_property health=99\n"
        "---\n"
        "health: 50\n"
    )
    # The batch rule parses sub-commands from args.commands
    commands = "set_property path=/P component=A prop=health value=100\nset_property path=/P component=A prop=health value=99"
    result = await reflect(
        "batch",
        {"commands": commands},
        batch_resp2,
        _dummy_send,
    )
    # Second sub-op has mismatch (expected 99, got 50)
    assert isinstance(result, Mismatch)
    assert "2" in result.msg and "health" in result.msg


# ── 15. batch caps warnings at 3 with "(N more)" suffix ──────────────────────

async def test_batch_caps_at_3():
    # 5 set_property commands each with value=100, but snapshot shows 0
    n = 5
    lines_cmds = "\n".join(
        f"set_property path=/P component=A prop=f{i} value=100" for i in range(n)
    )
    # Build a batch response where each sub-op snapshot returns 0
    resp_blocks = ""
    for i in range(n):
        resp_blocks += f"[{i+1}] ok\n---\nf{i}: 0\n"

    result = await reflect("batch", {"commands": lines_cmds}, resp_blocks, _dummy_send)
    assert isinstance(result, Mismatch)
    assert "(2 more)" in result.msg


# ── 16. env var UNITY_MCP_REFLECT=0 disables middleware injection ─────────────

async def test_env_disable(monkeypatch):
    """When UNITY_MCP_REFLECT=0, wrap_send must not append [REFLECT:...]."""
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    from unity_mcp.middleware import wrap_send, WRITE_CMDS

    async def fake_send(cmd, args, timeout=30.0):
        # Return a mismatch-triggering response: no snapshot block
        return "ok no snapshot here"

    wrapped = wrap_send(fake_send)
    result = await wrapped("set_property", {"prop": "health", "value": "100"})
    assert "[REFLECT:" not in result


# ── 17. _no_reflect kwarg skips reflection ───────────────────────────────────

async def test_no_reflect_kwarg(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    from unity_mcp.middleware import wrap_send

    async def fake_send(cmd, args, timeout=30.0):
        return "ok no snapshot"

    wrapped = wrap_send(fake_send)
    result = await wrapped("set_property", {"prop": "health", "value": "100", "_no_reflect": True})
    assert "[REFLECT:" not in result


# ── 18. set_runtime_property real C# format: field=value ─────────────────────

async def test_set_runtime_property_real_format():
    # C# RuntimeHelper returns "health=100" — no --- block
    result = await reflect("set_runtime_property", {"field": "health", "value": "100"}, "health=100", _dummy_send)
    assert result is None


async def test_set_runtime_property_mismatch_real():
    # C# returned "health=99" but we expected 100
    result = await reflect("set_runtime_property", {"field": "health", "value": "100"}, "health=99", _dummy_send)
    assert isinstance(result, Mismatch)
    assert "health" in result.msg and "99" in result.msg and "100" in result.msg


# ── 19. manage_component remove real C# format ───────────────────────────────

async def test_manage_component_remove_real_format():
    # C# ExecManageComponent (Cycle 6d): "Removed: Rigidbody2D. Remaining: Transform,BoxCollider2D"
    resp = "Removed: Rigidbody2D. Remaining: Transform,BoxCollider2D"
    result = await reflect("manage_component", {"action": "remove", "type": "Rigidbody2D", "path": "/Enemy"}, resp, _dummy_send)
    assert result is None


async def test_manage_component_add_uses_type_key():
    # C# ExecManageComponent (Cycle 6d): "Added: Rigidbody2D. Components: Transform,Rigidbody2D"
    resp = "Added: Rigidbody2D. Components: Transform,Rigidbody2D"
    result = await reflect("manage_component", {"action": "add", "type": "Rigidbody2D", "path": "/Enemy"}, resp, _dummy_send)
    assert result is None


# ── 20. delete_object real C# format: "Deleted #123" ────────────────────────

async def test_delete_object_real_format():
    # C# ExecDeleteObject returns "Deleted #123"
    result = await reflect("delete_object", {"id": 123}, "Deleted #123", _dummy_send)
    assert result is None


async def test_delete_object_with_path_arg():
    # path in args but C# never echoes it in response — must not fire Mismatch
    result = await reflect("delete_object", {"path": "/Enemy"}, "Deleted #456", _dummy_send)
    assert result is None


# ── 21. set_property no snapshot → silent None ───────────────────────────────

async def test_set_property_no_snapshot_silent():
    # C# returns "health = 100" when FindObject fails (no --- block) — should be None not Mismatch
    result = await reflect("set_property", {"prop": "health", "value": "100"}, "health = 100 (was 50)", _dummy_send)
    assert result is None


# ── 22. ObjectReference format: path + #instanceId ──────────────────────────

async def test_object_reference_format():
    # C# serializes as "/Enemy/Head #12345"
    # User passed value="/Enemy/Head"
    resp = "target = /Enemy/Head #12345 (was None)\n---\n  target: /Enemy/Head #12345"
    result = await reflect("set_property", {"prop": "target", "value": "/Enemy/Head"}, resp, _dummy_send)
    assert result is None


# ── 23. Color RGB vs RGBA ────────────────────────────────────────────────────

async def test_color_rgb_vs_rgba():
    # User passed (1,0,0), Unity returns (1.00, 0.00, 0.00, 1.00)
    resp = "color = (1.00, 0.00, 0.00, 1.00) (was (0, 0, 0, 1))\n---\n  color: (1.00, 0.00, 0.00, 1.00)"
    result = await reflect("set_property", {"prop": "color", "value": "(1,0,0)"}, resp, _dummy_send)
    assert result is None


# ── 24. _no_reflect stripped from bridge args ────────────────────────────────

async def test_no_reflect_stripped_from_bridge_args(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    from unity_mcp.middleware import wrap_send

    captured_args = {}

    async def capturing_send(cmd, args, timeout=30.0):
        captured_args.update(args)
        return "health = 100"  # no snapshot

    wrapped = wrap_send(capturing_send)
    await wrapped("set_property", {"prop": "health", "value": "100", "_no_reflect": True})
    assert "_no_reflect" not in captured_args


# ── 25. verify_snapshot disabled when reflect is on ──────────────────────────

async def test_verify_snapshot_disabled_when_reflect_on(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    from unity_mcp.middleware import wrap_send

    # verify_snapshot fires on set_property only when snapshot has [Component] line
    # Return a response that would trigger verify_snapshot but should be suppressed
    async def fake_send(cmd, args, timeout=30.0):
        return "prop = 100 (was 0)\n[Transform]\nprop: 999"

    wrapped = wrap_send(fake_send)
    result = await wrapped("set_property", {"prop": "prop", "value": "100"})
    assert "[VERIFIED:" not in result and "[VERIFY FAIL:" not in result


# ── 26. mismatch msg with ] sanitized ────────────────────────────────────────

async def test_mismatch_msg_with_bracket(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    from unity_mcp.middleware import wrap_send

    async def fake_send(cmd, args, timeout=30.0):
        # Return snapshot that will cause mismatch with ] in msg
        return "health = 99 (was 100)\n---\n  health: 99"

    wrapped = wrap_send(fake_send)
    result = await wrapped("set_property", {"prop": "health", "value": "100"})
    assert "[REFLECT:" in result
    # Find the reflect block and check no unmatched ]
    start = result.index("[REFLECT:")
    segment = result[start:]
    # The ] that closes [REFLECT: should be the last one; no extra ] inside msg
    inner = segment[len("[REFLECT:"):]
    close = inner.index("]")
    msg_content = inner[:close]
    assert "]" not in msg_content


# ── 27. wire_event — no confirmation → mismatch ──────────────────────────────

async def test_wire_event_no_confirmation_mismatch():
    result = await reflect("wire_event", {"event": "OnClick", "target": "/Btn"}, "Failed: target not found", _dummy_send)
    assert isinstance(result, Mismatch)
    assert "wire_event" in result.msg.lower()


async def test_wire_event_wired_confirmation_passes():
    result = await reflect("wire_event", {"event": "OnClick", "target": "/Btn"}, "Wired: OnClick → /Btn.OnPress", _dummy_send)
    assert result is None


async def test_wire_event_connected_confirmation_passes():
    result = await reflect("wire_event", {"event": "OnValueChanged", "target": "/Slider"}, "Connected: OnValueChanged to /Slider.Handle", _dummy_send)
    assert result is None


async def test_wire_event_empty_response_mismatch():
    result = await reflect("wire_event", {"event": "OnClick"}, "", _dummy_send)
    assert isinstance(result, Mismatch)


# ── set_property_delta ────────────────────────────────────────────────────────

async def test_set_property_delta_arrow_format():
    # C# returns "100 → 105" — should be silent (delta is relative, can't verify absolute)
    result = await reflect("set_property_delta", {"prop": "health", "value": "5"}, "100 → 105", _dummy_send)
    assert result is None


async def test_set_property_delta_error_returns_none():
    result = await reflect("set_property_delta", {"prop": "health", "value": "5"}, "Failed: property not found", _dummy_send)
    assert result is None


async def test_set_property_delta_empty_response():
    # No arrow, no error — unexpected format, stay silent
    result = await reflect("set_property_delta", {"prop": "health", "value": "5"}, "ok no snapshot", _dummy_send)
    assert result is None


# ── 28. ObjectReference $HEX format (Task 4) ────────────────────────────────

async def test_dollar_hex_in_object_ref_value_accepted():
    """C# now outputs '/Enemy/Head $3E8' — reflect must treat as match for '/Enemy/Head'."""
    resp = "target = /Enemy/Head $3E8\n---\n  target: /Enemy/Head $3E8"
    result = await reflect("set_property", {"prop": "target", "value": "/Enemy/Head"}, resp, _dummy_send)
    assert result is None


async def test_dollar_hex_without_path_mismatch():
    """Wrong path with $HEX must still fire a mismatch."""
    resp = "target = /Enemy/Body $3E8\n---\n  target: /Enemy/Body $3E8"
    result = await reflect("set_property", {"prop": "target", "value": "/Enemy/Head"}, resp, _dummy_send)
    assert isinstance(result, Mismatch)
