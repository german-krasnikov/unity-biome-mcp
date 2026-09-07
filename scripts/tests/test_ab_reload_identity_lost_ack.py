"""Offline tests for gauntlet.ab_reload_lost_ack.run_lost_ack (N3 T3 slice 3:
lost-ACK against a non-idempotent owned counter) and
gauntlet.ab_reload_proxy.CounterProxy's real drop-the-ACK wire behavior. No
Unity; the CounterProxy test uses a real in-process asyncio TCP pair, no
Unity peer. See Plans/N3-T3-T4-live-reload-identity.md Part 1.
"""
import asyncio
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import gauntlet.ab_reload_lost_ack as lost_ack  # noqa: E402
from fault_proxy import read_frame, write_frame  # noqa: E402
from gauntlet.ab_reload_proxy import CounterProxy  # noqa: E402

from unity_mcp.errors import UncertainDeliveryError  # noqa: E402

PORT_A = 9620


class ScriptedCounterCall:
    """Fake call(port, cmd, args) seam: pops one scripted response per call,
    in call order (read_counter is called exactly twice: before, after)."""

    def __init__(self, responses: list[str]) -> None:
        self._responses = list(responses)
        self.log: list[tuple] = []

    async def __call__(self, port: int, cmd: str, args: dict) -> str:
        self.log.append((port, cmd, args.get("code", "")))
        if not self._responses:
            raise AssertionError("no scripted counter response left")
        return self._responses.pop(0)


class FakeProxy:
    def __init__(self, forwarded: int = 1) -> None:
        self.forwarded = forwarded


def raising_send(exc: BaseException):
    async def send() -> None:
        raise exc
    return send


async def non_raising_send() -> None:
    return None


def make_seams(*, counters: list[str], forwarded: int = 1, send=None) -> lost_ack.LostAckSeams:
    send = send or raising_send(UncertainDeliveryError(cmd="execute_code", op_id="op-1", delivery="ACCEPTED"))
    return lost_ack.LostAckSeams(
        call=ScriptedCounterCall(counters), send_increment_via_proxy=send, proxy=FakeProxy(forwarded),
    )


# --- run_lost_ack: happy path + negative controls ---------------------------


@pytest.mark.asyncio
async def test_run_lost_ack_happy_path_exactly_one_effect() -> None:
    seams = make_seams(counters=["0", "1"])
    result = await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)
    assert result["counter_before"] == 0
    assert result["counter_after"] == 1
    assert result["forwarded"] == 1
    assert result["uncertain_delivery_raised"] is True


@pytest.mark.asyncio
async def test_run_lost_ack_raises_when_no_exception_raised() -> None:
    seams = make_seams(counters=["0", "1"], send=non_raising_send)
    with pytest.raises(lost_ack.ABReloadIdentityError, match="did not raise UncertainDeliveryError"):
        await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)


@pytest.mark.asyncio
async def test_run_lost_ack_raises_when_wrong_exception_type() -> None:
    seams = make_seams(counters=["0", "1"], send=raising_send(ConnectionError("boom")))
    with pytest.raises(lost_ack.ABReloadIdentityError, match="not UncertainDeliveryError"):
        await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)


@pytest.mark.asyncio
async def test_run_lost_ack_raises_on_duplicate_forward() -> None:
    seams = make_seams(counters=["0", "2"], forwarded=2)
    with pytest.raises(lost_ack.ABReloadIdentityError, match="duplicate forward"):
        await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)


@pytest.mark.asyncio
async def test_run_lost_ack_raises_when_effect_not_applied() -> None:
    seams = make_seams(counters=["0", "0"])
    with pytest.raises(lost_ack.ABReloadIdentityError, match="effect not applied"):
        await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)


@pytest.mark.asyncio
async def test_run_lost_ack_raises_on_duplicate_increment() -> None:
    seams = make_seams(counters=["0", "2"], forwarded=1)
    with pytest.raises(lost_ack.ABReloadIdentityError, match="duplicate increment"):
        await lost_ack.run_lost_ack(lost_ack.LostAckConfig(port_a=PORT_A), seams)


def test_effect_not_applied_and_duplicate_messages_are_distinct() -> None:
    with pytest.raises(lost_ack.ABReloadIdentityError, match="effect not applied") as lost:
        lost_ack.check_effect_applied_exactly_once(counter_before=0, counter_after=0, forwarded=1)
    with pytest.raises(lost_ack.ABReloadIdentityError, match="duplicate increment") as dup:
        lost_ack.check_effect_applied_exactly_once(counter_before=0, counter_after=2, forwarded=1)
    assert str(lost.value) != str(dup.value)


def test_check_effect_applied_exactly_once_passes_on_single_effect() -> None:
    lost_ack.check_effect_applied_exactly_once(counter_before=0, counter_after=1, forwarded=1)  # no raise


# --- CounterProxy: real TCP drop-the-ACK behavior ----------------------------


async def _fake_unity_upstream(reader: asyncio.StreamReader, writer: asyncio.StreamWriter, effects: list[int]) -> None:
    """Minimal upstream Unity stand-in: execute_code really increments and
    replies -- the CounterProxy in front is what drops the reply."""
    request = await read_frame(reader)
    payload = json.loads(request)
    effects[0] += 1
    response = json.dumps({"id": payload["id"], "ok": True, "data": str(effects[0])}).encode("utf-8")
    write_frame(writer, response)
    await writer.drain()
    writer.close()


@pytest.mark.asyncio
async def test_counter_proxy_forwards_effect_and_drops_the_ack() -> None:
    effects = [0]
    upstream = await asyncio.start_server(lambda r, w: _fake_unity_upstream(r, w, effects), "127.0.0.1", 0)
    proxy = CounterProxy("127.0.0.1", upstream.sockets[0].getsockname()[1])
    listener = await asyncio.start_server(proxy.handle_client, "127.0.0.1", 0)
    try:
        reader, writer = await asyncio.open_connection("127.0.0.1", listener.sockets[0].getsockname()[1])
        request = json.dumps({"id": "1", "cmd": "execute_code", "args": {"code": "Counter++;"}}).encode("utf-8")
        write_frame(writer, request)
        await writer.drain()
        with pytest.raises(asyncio.IncompleteReadError):
            await read_frame(reader)  # the ACK never arrives -- the proxy closed instead
        writer.close()
    finally:
        listener.close()
        await listener.wait_closed()
        upstream.close()
        await upstream.wait_closed()
    assert proxy.forwarded == 1
    assert proxy.dropped_ack == 1
    assert effects[0] == 1, "the real effect happened exactly once upstream, ACK or not"
