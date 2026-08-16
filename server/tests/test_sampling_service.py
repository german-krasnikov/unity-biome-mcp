"""Tests for SamplingService (claude CLI)."""
import asyncio
import os
import tempfile
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


def _mock_proc(stdout_text="PASS: looks good"):
    proc = MagicMock()
    proc.communicate = AsyncMock(return_value=(stdout_text.encode(), b""))
    proc.returncode = 0
    return proc


# ── Enabled / disabled ──────────────────────────────────────────────────────

def test_disabled_by_default(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_VISUAL_VERIFY", raising=False)
    from unity_mcp.sampling import SamplingService
    assert SamplingService().enabled is False


def test_enabled_via_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService
    assert SamplingService().enabled is True


async def test_disabled_returns_none(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_VISUAL_VERIFY", raising=False)
    from unity_mcp.sampling import SamplingService
    svc = SamplingService()
    assert await svc.verify_visual("prompt") is None
    assert await svc.summarize("text") is None


@pytest.mark.parametrize("backend", ["codex", "gemini"])
async def test_unsupported_sampling_backend_fails_closed_before_spawn(monkeypatch, backend):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.llm_config import apply_config, reset
    from unity_mcp.sampling import SamplingService

    apply_config({
        "do_intent": {
            "model": "unsupported-model",
            "max_turns": 1,
            "timeout": 15.0,
            "backend": backend,
        }
    })
    try:
        with patch(
            "unity_mcp.sampling.asyncio.create_subprocess_exec",
            new_callable=AsyncMock,
        ) as mock_exec:
            result = await SamplingService().generate("prompt")
    finally:
        reset()

    assert result is None
    mock_exec.assert_not_called()


# ── verify_visual ───────────────────────────────────────────────────────────

async def test_verify_visual_calls_claude_cli(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    mock_proc = _mock_proc("PASS: position correct")
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=mock_proc)) as mock_exec:
        result = await SamplingService().verify_visual("check position")

    assert result == "PASS: position correct"
    mock_exec.assert_called_once()
    args = mock_exec.call_args[0]
    assert "claude" in args[0] or args[0].endswith("claude")
    assert "-p" in args
    assert "--model" in args
    assert "haiku" in args


async def test_verify_visual_with_screenshot(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
        f.write(b"\x89PNG\r\n\x1a\n" + b"\x00" * 20)
        tmp_path = f.name

    try:
        mock_proc = _mock_proc("PASS: visible")
        with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=mock_proc)) as mock_exec:
            result = await SamplingService().verify_visual("check", screenshot_path=tmp_path)

        assert result == "PASS: visible"
        args = mock_exec.call_args[0]
        # path must be embedded in the -p prompt, not as a positional arg
        prompt_idx = list(args).index("-p") + 1
        assert tmp_path in args[prompt_idx]
        assert "--tools" in args
        assert "Read" in args
        assert "--max-turns" in args
        turns_idx = list(args).index("--max-turns") + 1
        assert args[turns_idx] == "2"
    finally:
        os.unlink(tmp_path)


async def test_verify_visual_bad_screenshot_ignored(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    mock_proc = _mock_proc("PASS: text only")
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=mock_proc)) as mock_exec:
        result = await SamplingService().verify_visual("check", screenshot_path="/no/such/file.png")

    assert result == "PASS: text only"
    args = mock_exec.call_args[0]
    # bad path → no Read tool flags; max-turns comes from profile (visual_verify default=2)
    assert "--tools" not in args
    assert "--max-turns" in args


async def test_verify_visual_graceful_on_error(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(side_effect=FileNotFoundError("claude not found"))):
        result = await SamplingService().verify_visual("prompt")

    assert result is None


async def test_verify_visual_timeout(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    proc = MagicMock()
    proc.communicate = AsyncMock(side_effect=asyncio.TimeoutError)
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        result = await SamplingService().verify_visual("prompt")

    assert result is None


# ── summarize ───────────────────────────────────────────────────────────────

async def test_summarize_calls_claude(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    mock_proc = _mock_proc("short summary")
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=mock_proc)) as mock_exec:
        result = await SamplingService().summarize("long text", "Make short")

    assert result == "short summary"
    args = mock_exec.call_args[0]
    assert "-p" in args


# ── Middleware integration ──────────────────────────────────────────────────

async def test_middleware_skips_reads():
    from unity_mcp.middleware import Middleware
    from unity_mcp.sampling import SamplingService
    mock = MagicMock(spec=SamplingService)
    mock.verify_visual = AsyncMock(return_value="PASS")
    mock.enabled = True
    mw = Middleware()
    mw.sampling = mock
    result = await mw.maybe_verify_visual("get_component", {}, "data")
    assert result == "data"
    mock.verify_visual.assert_not_called()


async def test_middleware_skips_high_confidence():
    from unity_mcp.middleware import Middleware
    from unity_mcp.sampling import SamplingService
    mock = MagicMock(spec=SamplingService)
    mock.verify_visual = AsyncMock(return_value="PASS")
    mock.enabled = True
    mw = Middleware()
    mw.sampling = mock
    mw.confidence = 1.0
    result = await mw.maybe_verify_visual("set_property", {}, "ok")
    assert result == "ok"
    mock.verify_visual.assert_not_called()


async def test_middleware_triggers_on_low_confidence():
    from unity_mcp.middleware import Middleware
    from unity_mcp.sampling import SamplingService
    mock = MagicMock(spec=SamplingService)
    mock.verify_visual = AsyncMock(return_value="PASS: correct")
    mock.enabled = True
    mw = Middleware()
    mw.sampling = mock
    mw.confidence = 0.3
    result = await mw.maybe_verify_visual("set_property", {"path": "/Obj"}, "ok")
    mock.verify_visual.assert_called_once()
    assert "[VERIFY:" in result


# ── Fix 1: zombie process killed on timeout ──────────────────────────────────

async def test_sampling_zombie_killed_on_timeout(monkeypatch):
    """When wait_for times out, proc.kill() MUST be called to prevent zombie."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService
    import unity_mcp.sampling as sampling_mod

    proc = MagicMock()
    proc.returncode = None
    async def _hang():
        await asyncio.sleep(9999)
        return b"", b""
    proc.communicate = _hang
    proc.kill = MagicMock()
    proc.wait = AsyncMock(return_value=0)

    real_wait_for = asyncio.wait_for
    async def fast_wait_for(coro, timeout):
        if timeout == 15.0:
            coro.close()  # avoid "never awaited" warning
            raise asyncio.TimeoutError()
        return await real_wait_for(coro, 0.1)
    monkeypatch.setattr(sampling_mod.asyncio, "wait_for", fast_wait_for)

    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        result = await SamplingService().verify_visual("prompt")

    assert result is None
    proc.kill.assert_called_once()


async def test_run_helper_returns_none_on_generic_exception(monkeypatch):
    """Generic exceptions (e.g. OSError) → return None, kill if still running."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    proc = MagicMock()
    proc.returncode = None
    proc.communicate = AsyncMock(side_effect=OSError("pipe broken"))
    proc.kill = MagicMock()

    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        result = await SamplingService().generate("prompt")

    assert result is None
    proc.kill.assert_called_once()


async def test_run_passes_stdin_devnull(monkeypatch):
    """CLI must not block on stdin EOF — pass DEVNULL."""
    captured = {}

    async def fake_exec(*args, **kwargs):
        captured.update(kwargs)
        raise RuntimeError("stop")

    monkeypatch.setattr("unity_mcp.sampling.asyncio.create_subprocess_exec", fake_exec)

    from unity_mcp.sampling import SamplingService
    result = await SamplingService()._run(["echo"], 1.0)
    assert result is None
    assert captured.get("stdin") == asyncio.subprocess.DEVNULL


async def test_semaphore_serializes_concurrent_calls(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    monkeypatch.setenv("UNITY_MCP_VISUAL_CONCURRENCY", "2")
    from unity_mcp.sampling import SamplingService
    SamplingService._semaphore = None  # force re-init

    active = [0]; peak = [0]

    class MockProc:
        returncode = 0
        async def communicate(self):
            active[0] += 1
            peak[0] = max(peak[0], active[0])
            await asyncio.sleep(0.05)
            active[0] -= 1
            return (b"OK", b"")

    async def fake_exec(*args, **kwargs):
        return MockProc()

    monkeypatch.setattr("unity_mcp.sampling.asyncio.create_subprocess_exec", fake_exec)

    await asyncio.gather(*[SamplingService()._run(["x"], 1.0) for _ in range(5)])
    assert peak[0] <= 2, f"Concurrency limit violated: peak={peak[0]}"


async def test_cancellederror_kills_proc_and_propagates(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")

    proc_killed = [False]

    class MockProc:
        returncode = None
        async def communicate(self):
            await asyncio.sleep(99)  # hang
            return (b"", b"")
        def kill(self):
            proc_killed[0] = True
            self.returncode = -9
        async def wait(self):
            return 0

    async def fake_exec(*args, **kwargs):
        return MockProc()

    monkeypatch.setattr("unity_mcp.sampling.asyncio.create_subprocess_exec", fake_exec)

    from unity_mcp.sampling import SamplingService
    SamplingService._semaphore = None  # fresh semaphore
    task = asyncio.create_task(SamplingService()._run(["x"], 60.0))
    await asyncio.sleep(0.05)  # let _run get to communicate
    task.cancel()

    with pytest.raises(asyncio.CancelledError):
        await task
    assert proc_killed[0], "proc.kill() not called on cancellation"

