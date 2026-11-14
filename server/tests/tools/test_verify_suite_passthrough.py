"""Tests for verify_after_change → run_playtest_suite param passthrough.

Ensures auto_play, restart_between, and suite_timeout are forwarded correctly.
"""
import pytest
from unittest.mock import AsyncMock, call, patch

_AWAIT_COMPILE = "unity_mcp.tools.code_intel.await_compile"
_GET_ERRORS = "unity_mcp.tools.console.get_compile_errors"
_RUN_SUITE = "unity_mcp.tools.runtime.run_playtest_suite"

_SUITE_PASS = "SUITE: 2/2 passed (1.0s)"


def _patch_compile_clean():
    return patch(_AWAIT_COMPILE, new=AsyncMock(return_value="compile clean"))


def _patch_errors_clean():
    return patch(_GET_ERRORS, new=AsyncMock(return_value=""))


async def _call_verify(**kwargs):
    from unity_mcp.tools.verify import verify_after_change
    return await verify_after_change(**kwargs)


@pytest.mark.asyncio
async def test_verify_passes_auto_play_to_suite():
    with (
        _patch_compile_clean(),
        _patch_errors_clean(),
        patch(_RUN_SUITE, new=AsyncMock(return_value=_SUITE_PASS)) as mock_suite,
    ):
        result = await _call_verify(playtests="Playtests/*.playtest", auto_play=True)

    assert "PASS" in result
    _kw = mock_suite.call_args.kwargs
    assert _kw.get("auto_play") is True


@pytest.mark.asyncio
async def test_verify_passes_restart_between_to_suite():
    with (
        _patch_compile_clean(),
        _patch_errors_clean(),
        patch(_RUN_SUITE, new=AsyncMock(return_value=_SUITE_PASS)) as mock_suite,
    ):
        result = await _call_verify(playtests="Playtests/*.playtest", restart_between=True)

    assert "PASS" in result
    _kw = mock_suite.call_args.kwargs
    assert _kw.get("restart_between") is True


@pytest.mark.asyncio
async def test_verify_passes_suite_timeout_to_suite():
    with (
        _patch_compile_clean(),
        _patch_errors_clean(),
        patch(_RUN_SUITE, new=AsyncMock(return_value=_SUITE_PASS)) as mock_suite,
    ):
        result = await _call_verify(playtests="Playtests/*.playtest", suite_timeout=600.0)

    assert "PASS" in result
    _kw = mock_suite.call_args.kwargs
    assert _kw.get("suite_timeout") == 600.0


@pytest.mark.asyncio
async def test_verify_default_params_unchanged():
    """Without explicit params, suite gets default values (backward compat)."""
    with (
        _patch_compile_clean(),
        _patch_errors_clean(),
        patch(_RUN_SUITE, new=AsyncMock(return_value=_SUITE_PASS)) as mock_suite,
    ):
        result = await _call_verify(playtests="Playtests/*.playtest")

    assert "PASS" in result
    _kw = mock_suite.call_args.kwargs
    assert _kw.get("auto_play") is False
    assert _kw.get("restart_between") is False
    assert _kw.get("suite_timeout") == 300.0


@pytest.mark.asyncio
async def test_verify_auto_play_independent_of_restart_between():
    """auto_play and restart_between are independent — setting one doesn't set the other."""
    with (
        _patch_compile_clean(),
        _patch_errors_clean(),
        patch(_RUN_SUITE, new=AsyncMock(return_value=_SUITE_PASS)) as mock_suite,
    ):
        await _call_verify(playtests="Playtests/*.playtest", auto_play=True, restart_between=False)

    _kw = mock_suite.call_args.kwargs
    assert _kw.get("auto_play") is True
    assert _kw.get("restart_between") is False
