"""TDD tests for reflect/factory.py — make_ok_rule, make_no_error_rule, make_action_guard."""
import pytest

from unity_mcp.reflect import _RULES, Mismatch


# Each test uses a unique cmd prefix to avoid _RULES collisions across tests.
_PFX = "_trf_"   # test reflect factory prefix


# ── make_ok_rule ──────────────────────────────────────────────────────────────

async def test_make_ok_rule_happy_path():
    from unity_mcp.reflect.factory import make_ok_rule
    cmd = f"{_PFX}ok_happy"
    make_ok_rule(cmd, ("reverted", "nothing"))
    rule = _RULES[cmd]
    result = await rule({}, "reverted 1 turn(s)", None)
    assert result is None


async def test_make_ok_rule_mismatch():
    from unity_mcp.reflect.factory import make_ok_rule
    cmd = f"{_PFX}ok_miss"
    make_ok_rule(cmd, ("reverted", "nothing"))
    rule = _RULES[cmd]
    result = await rule({}, "some unrelated response", None)
    assert isinstance(result, Mismatch)
    assert "reverted" in result.msg or "nothing" in result.msg


async def test_make_ok_rule_fail_open_on_error():
    """When C# returns an error, ok_rule returns None (fail-open)."""
    from unity_mcp.reflect.factory import make_ok_rule
    cmd = f"{_PFX}ok_err"
    make_ok_rule(cmd, ("reverted",))
    rule = _RULES[cmd]
    result = await rule({}, "Error: operation failed", None)
    assert result is None


async def test_make_ok_rule_fail_open_on_failed():
    from unity_mcp.reflect.factory import make_ok_rule
    cmd = f"{_PFX}ok_failed"
    make_ok_rule(cmd, ("reverted",))
    rule = _RULES[cmd]
    result = await rule({}, "Failed to find object", None)
    assert result is None


async def test_make_ok_rule_case_insensitive():
    """Token check is case-insensitive."""
    from unity_mcp.reflect.factory import make_ok_rule
    cmd = f"{_PFX}ok_case"
    make_ok_rule(cmd, ("OK",))
    rule = _RULES[cmd]
    result = await rule({}, "ok", None)
    assert result is None


# ── make_no_error_rule ────────────────────────────────────────────────────────

async def test_make_no_error_rule_clean_response():
    from unity_mcp.reflect.factory import make_no_error_rule
    cmd = f"{_PFX}noerr_clean"
    make_no_error_rule(cmd)
    rule = _RULES[cmd]
    result = await rule({}, "Moved /Obj → Scene2", None)
    assert result is None


async def test_make_no_error_rule_error_token():
    from unity_mcp.reflect.factory import make_no_error_rule
    cmd = f"{_PFX}noerr_err"
    make_no_error_rule(cmd)
    rule = _RULES[cmd]
    result = await rule({}, "Error: object not found", None)
    assert isinstance(result, Mismatch)


async def test_make_no_error_rule_failed_token():
    from unity_mcp.reflect.factory import make_no_error_rule
    cmd = f"{_PFX}noerr_fail"
    make_no_error_rule(cmd)
    rule = _RULES[cmd]
    result = await rule({}, "Failed to process", None)
    assert isinstance(result, Mismatch)


async def test_make_no_error_rule_exception_token():
    from unity_mcp.reflect.factory import make_no_error_rule
    cmd = f"{_PFX}noerr_exc"
    make_no_error_rule(cmd)
    rule = _RULES[cmd]
    result = await rule({}, "Exception: NullRef", None)
    assert isinstance(result, Mismatch)


async def test_make_no_error_rule_empty_response():
    """Empty response is not an error — silent."""
    from unity_mcp.reflect.factory import make_no_error_rule
    cmd = f"{_PFX}noerr_empty"
    make_no_error_rule(cmd)
    rule = _RULES[cmd]
    result = await rule({}, "", None)
    assert result is None


# ── make_action_guard ─────────────────────────────────────────────────────────

async def test_make_action_guard_read_action_skips():
    """Read action returns None without calling inner."""
    from unity_mcp.reflect.factory import make_action_guard, _make_no_error_fn
    inner = _make_no_error_fn("_inner")
    guarded = make_action_guard("_test", frozenset({"get", "list"}), inner)
    result = await guarded({"action": "get"}, "Error: blah", None)
    assert result is None  # skipped because "get" is a read action


async def test_make_action_guard_write_action_calls_inner():
    """Write action calls inner (no_error_fn detects error → Mismatch)."""
    from unity_mcp.reflect.factory import make_action_guard, _make_no_error_fn
    inner = _make_no_error_fn("_inner2")
    guarded = make_action_guard("_test2", frozenset({"get"}), inner)
    result = await guarded({"action": "set"}, "Error: failed", None)
    assert isinstance(result, Mismatch)


async def test_make_action_guard_write_action_clean_response():
    """Write action with clean response → None."""
    from unity_mcp.reflect.factory import make_action_guard, _make_no_error_fn
    inner = _make_no_error_fn("_inner3")
    guarded = make_action_guard("_test3", frozenset({"get"}), inner)
    result = await guarded({"action": "set"}, "applied", None)
    assert result is None


async def test_make_action_guard_no_action_arg_calls_inner():
    """Missing action arg = write → calls inner."""
    from unity_mcp.reflect.factory import make_action_guard, _make_no_error_fn
    inner = _make_no_error_fn("_inner4")
    guarded = make_action_guard("_test4", frozenset({"get"}), inner)
    result = await guarded({}, "Error: no action", None)
    assert isinstance(result, Mismatch)


async def test_make_action_guard_does_not_register():
    """make_action_guard returns a function but does NOT register in _RULES."""
    from unity_mcp.reflect.factory import make_action_guard, _make_no_error_fn
    inner = _make_no_error_fn("_inner5")
    cmd = f"{_PFX}guard_noreg"
    make_action_guard(cmd, frozenset({"get"}), inner)
    assert cmd not in _RULES


# ── make_action_guarded_no_error_rule ─────────────────────────────────────────

async def test_make_action_guarded_no_error_rule_registers():
    from unity_mcp.reflect.factory import make_action_guarded_no_error_rule
    cmd = f"{_PFX}agnoerr"
    make_action_guarded_no_error_rule(cmd, frozenset({"get"}))
    assert cmd in _RULES


async def test_make_action_guarded_no_error_rule_read_skips():
    from unity_mcp.reflect.factory import make_action_guarded_no_error_rule
    cmd = f"{_PFX}agnoerr_read"
    make_action_guarded_no_error_rule(cmd, frozenset({"get"}))
    rule = _RULES[cmd]
    result = await rule({"action": "get"}, "Error: blah", None)
    assert result is None


async def test_make_action_guarded_no_error_rule_write_error():
    from unity_mcp.reflect.factory import make_action_guarded_no_error_rule
    cmd = f"{_PFX}agnoerr_werr"
    make_action_guarded_no_error_rule(cmd, frozenset({"get"}))
    rule = _RULES[cmd]
    result = await rule({"action": "set"}, "Error: mutation failed", None)
    assert isinstance(result, Mismatch)
