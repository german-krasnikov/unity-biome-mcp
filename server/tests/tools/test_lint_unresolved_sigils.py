"""Tests for lint_playtest warning/error contract for unresolved $sigils.

MCP-LINT-014: unresolved sigils must produce warnings, not exceptions.
No live Unity — _send is mocked at module level.
"""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mod():
    import unity_mcp.tools.runtime as m
    return m


@pytest.fixture(autouse=True)
def _patch_runtime(monkeypatch, mod):
    """Bind _send and _args so lint_playtest can be called without a server."""
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


async def test_unresolved_sigil_in_assert_is_warning_not_error(mod, _patch_runtime):
    """ASSERT $UNDEFINED → Unity linter returns WARN, not an exception.

    The Python layer must propagate the warning string without raising.
    """
    _patch_runtime.return_value = "WARN: unresolved sigil '$UNDEFINED'"
    result = await mod.lint_playtest(script="ASSERT $UNDEFINED|Health == 10")
    assert "WARN" in result
    assert not result.startswith("err:")


async def test_unresolved_sigil_in_include_path_is_warning(mod, _patch_runtime):
    """INCLUDE $MISSING_FILE → parse completes with a warning about the unresolved sigil.

    Lint must not crash when a sigil appears in an INCLUDE path.
    """
    _patch_runtime.return_value = "WARN: unresolved sigil '$MISSING_FILE' in INCLUDE path"
    result = await mod.lint_playtest(script="INCLUDE $MISSING_FILE\nASSERT /Player|Health == 10")
    assert "WARN" in result
    assert "$MISSING_FILE" in result


async def test_defined_sigil_produces_no_warning(mod, _patch_runtime):
    """VAL $hp /Player|Health followed by ASSERT $hp == 10 → no WARN in result.

    A correctly defined alias must pass lint cleanly.
    """
    _patch_runtime.return_value = "ok: 0 warnings"
    result = await mod.lint_playtest(script="VAL $hp /Player|Health\nASSERT $hp == 10")
    assert "WARN" not in result
