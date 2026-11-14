import asyncio

import pytest

from tests.live.relay_test_helpers import _read_relay_port, _stop_relay


class _Stdout:
    def __init__(self, lines):
        self._lines = iter(lines)

    async def readline(self):
        return next(self._lines, b"")


class _Process:
    def __init__(self, lines=()):
        self.stdout = _Stdout(lines)
        self.returncode = None
        self.terminated = 0
        self.killed = 0
        self.waited = 0

    def terminate(self):
        self.terminated += 1

    def kill(self):
        self.killed += 1
        self.returncode = -9

    async def wait(self):
        self.waited += 1
        self.returncode = self.returncode or 0
        return self.returncode


@pytest.mark.asyncio
async def test_read_relay_port_uses_python_310_compatible_deadline():
    proc = _Process([b"starting\n", b"relay_port:54321\n"])

    assert await _read_relay_port(proc, timeout=0.1) == 54321


@pytest.mark.asyncio
async def test_read_relay_port_rejects_eof_and_invalid_port():
    with pytest.raises(EOFError):
        await _read_relay_port(_Process(), timeout=0.1)
    with pytest.raises(ValueError, match="invalid relay port"):
        await _read_relay_port(_Process([b"relay_port:70000\n"]), timeout=0.1)


@pytest.mark.asyncio
async def test_stop_relay_terminates_and_reaps_process():
    proc = _Process()

    await _stop_relay(proc, timeout=0.1)

    assert (proc.terminated, proc.killed, proc.waited) == (1, 0, 1)


@pytest.mark.asyncio
async def test_stop_relay_kills_and_reaps_after_timeout():
    proc = _Process()
    wait_calls = 0

    async def wait():
        nonlocal wait_calls
        wait_calls += 1
        if wait_calls == 1:
            await asyncio.sleep(10)
        return proc.returncode

    proc.wait = wait

    await _stop_relay(proc, timeout=0.001)

    assert (proc.terminated, proc.killed, wait_calls) == (1, 1, 2)
