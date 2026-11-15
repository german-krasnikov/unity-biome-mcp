"""Tests for AcpAgentAdapter — type-safety and mode switching."""
import pytest
from unittest.mock import MagicMock

from unity_mcp.adapters.acp import AcpAgentAdapter
from unity_mcp.cli_session import SessionMeta


def _make_adapter() -> AcpAgentAdapter:
    backend = MagicMock()
    broker = MagicMock()
    return AcpAgentAdapter(backend, broker)


def _make_meta(**overrides) -> SessionMeta:
    defaults = dict(
        backend="claude", mode="ask", model=None,
        mcp_port=9500, prompt="hello", config_dir=None,
    )
    return SessionMeta(**{**defaults, **overrides})


@pytest.mark.asyncio
async def test_acp_set_mode_correct_type():
    """set_mode must pass a narrowed SessionMeta (not Optional) to start."""
    adapter = _make_adapter()
    adapter._meta = _make_meta(mode="ask")

    start_calls: list[SessionMeta] = []

    async def fake_start(m: SessionMeta) -> None:
        start_calls.append(m)

    adapter.start = fake_start  # type: ignore[method-assign]
    await adapter.set_mode("agent")

    assert len(start_calls) == 1
    assert isinstance(start_calls[0], SessionMeta)
    assert start_calls[0].mode == "agent"


@pytest.mark.asyncio
async def test_acp_set_mode_noop_when_no_meta():
    """set_mode is a no-op when _meta is None."""
    adapter = _make_adapter()
    adapter._meta = None

    start_calls = []

    async def fake_start(m: SessionMeta) -> None:
        start_calls.append(m)

    adapter.start = fake_start  # type: ignore[method-assign]
    await adapter.set_mode("agent")

    assert start_calls == []
