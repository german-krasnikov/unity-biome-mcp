"""Executor.execute's retry gate (planverify DECISION module: fixGate/fixApply).

Mock boundary: Executor.__init__(send, sampling) already takes both as injectable constructor args.
These tests pin the fixGate thresholds (0/1/5/6 failed lines, no sampling) and fixApply guards
(empty fix, invalid fix) that test_do_intent.py's existing tests do not isolate individually.
"""
from unittest.mock import AsyncMock, MagicMock


def _svc(generate_return: str = "create_object name=Cube") -> MagicMock:
    svc = MagicMock()
    svc.generate = AsyncMock(return_value=generate_return)
    return svc


async def test_not_partial_skips_retry():
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="ok: 1 ops")
    svc = _svc()
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    svc.generate.assert_not_called()
    assert result == "ok: 1 ops"


async def test_partial_marker_with_zero_failed_lines_skips_retry():
    """Legacy 'PARTIAL:' marker with no '[N] err:' lines: partial but 0 failed lines → done."""
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="PARTIAL: ok")
    svc = _svc()
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    svc.generate.assert_not_called()
    assert result == "PARTIAL: ok"


async def test_no_sampling_service_skips_retry():
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="[1] err: not found")
    ex = Executor(send, sampling=None)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    assert result == "[1] err: not found"


async def test_one_failed_line_retries():
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(side_effect=["[1] ok: created\n[2] err: not found", "ok: 1 ops"])
    svc = _svc("create_object name=Cube")
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    assert send.call_count == 2
    svc.generate.assert_called_once()
    assert result == "ok: 1 ops"


async def test_five_failed_lines_retries():
    from unity_mcp.do_intent.executor import Executor

    failed = "\n".join(f"[{i}] err: not found" for i in range(1, 6))
    send = AsyncMock(side_effect=[failed, "ok: 1 ops"])
    svc = _svc("create_object name=Cube")
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    assert send.call_count == 2
    assert result == "ok: 1 ops"


async def test_six_failed_lines_skips_retry():
    from unity_mcp.do_intent.executor import Executor

    failed = "\n".join(f"[{i}] err: not found" for i in range(1, 7))
    send = AsyncMock(return_value=failed)
    svc = _svc()
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    svc.generate.assert_not_called()
    assert result == failed


async def test_empty_fix_keeps_original():
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="[1] err: not found")
    svc = _svc("")
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    assert result == "[1] err: not found"


async def test_invalid_fix_keeps_original():
    """Fix text that validate_plan rejects (forbidden command) → keep the original batch result."""
    from unity_mcp.do_intent.executor import Executor

    send = AsyncMock(return_value="[1] err: not found")
    svc = _svc("delete_object path=/X")
    ex = Executor(send, sampling=svc)
    result = await ex.execute("create_object name=Cube")
    send.assert_called_once()
    assert result == "[1] err: not found"
