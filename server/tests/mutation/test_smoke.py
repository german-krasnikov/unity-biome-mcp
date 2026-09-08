"""Mutation regression lane smoke test: minimal roundtrip through real TCP bridge."""
import pytest

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]


async def test_ping_returns_pong(mutation_bridge):
    result = await mutation_bridge.send("ping", {})
    assert result.get("ok") and result.get("data") == "pong"
