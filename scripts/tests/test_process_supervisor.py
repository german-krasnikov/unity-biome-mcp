"""Behavioral tests for bounded POSIX process-group ownership."""


import hashlib
import os
import sys
import threading
import time
from collections.abc import Callable  # noqa: TC003
from pathlib import Path

import pytest

POSIX_ONLY = pytest.mark.skipif(
    os.name != "posix",
    reason="POSIX process-group behavior",
)

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from gauntlet.process_posix import PosixProcessError  # noqa: E402
from gauntlet.process_supervisor import (  # noqa: E402
    ProcessSpec,
    ProcessSupervisionError,
    ProcessSupervisor,
)


def _spec(
    tmp_path: Path,
    source: str,
    *,
    timeout: float = 5.0,
    output_limit: int = 64 * 1024,
) -> ProcessSpec:
    return ProcessSpec(
        command=(sys.executable, "-I", "-c", source),
        cwd=tmp_path,
        environment={"PATH": os.environ.get("PATH", ""), "PYTHONUTF8": "1"},
        timeout_seconds=timeout,
        output_limit_bytes=output_limit,
        graceful_shutdown_seconds=0.2,
    )


@POSIX_ONLY
def test_normal_exit_captures_bounded_output_and_digest(tmp_path: Path) -> None:
    result = ProcessSupervisor().run(
        _spec(
            tmp_path,
            "import sys; print('ready'); print('notice', file=sys.stderr)",
        )
    )

    assert result.completed_within_scope is True
    assert result.return_code == 0
    assert result.stdout.tail == b"ready\n"
    assert result.stderr.tail == b"notice\n"
    assert result.stdout.sha256 == hashlib.sha256(b"ready\n").hexdigest()
    assert result.cleanup.scoped_clean is True
    assert result.cleanup.release_safe is False
    assert result.cleanup.forced is False


@POSIX_ONLY
def test_timeout_terminates_owned_process_group(tmp_path: Path) -> None:
    result = ProcessSupervisor().run(
        _spec(tmp_path, "import time; time.sleep(60)", timeout=0.1)
    )

    assert result.completed_within_scope is False
    assert result.timed_out is True
    assert result.return_code is not None
    assert result.cleanup.scoped_clean is True


@POSIX_ONLY
def test_cancellation_uses_the_same_cleanup_path(tmp_path: Path) -> None:
    cancellation = threading.Event()
    timer = threading.Timer(0.1, cancellation.set)
    timer.start()
    try:
        result = ProcessSupervisor().run(
            _spec(tmp_path, "import time; time.sleep(60)"),
            cancellation=cancellation,
        )
    finally:
        timer.cancel()

    assert result.completed_within_scope is False
    assert result.cancelled is True
    assert result.cleanup.scoped_clean is True


@POSIX_ONLY
def test_output_flood_is_bounded_and_stops_the_process(tmp_path: Path) -> None:
    result = ProcessSupervisor().run(
        _spec(
            tmp_path,
            "import os; os.write(1, b'x' * 1000000); os.write(2, b'y' * 1000000)",
            output_limit=4096,
        )
    )

    assert result.completed_within_scope is False
    assert result.output_limit_exceeded is True
    assert result.stdout.total_bytes >= len(result.stdout.tail)
    assert result.stderr.total_bytes >= len(result.stderr.tail)
    assert len(result.stdout.tail) <= 4096
    assert len(result.stderr.tail) <= 4096
    assert result.cleanup.scoped_clean is True


@POSIX_ONLY
def test_sigterm_ignored_requires_forced_cleanup(tmp_path: Path) -> None:
    result = ProcessSupervisor().run(
        _spec(
            tmp_path,
            "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(60)",
            timeout=0.1,
        )
    )

    assert result.completed_within_scope is False
    assert result.timed_out is True
    assert result.cleanup.scoped_clean is True
    assert result.cleanup.forced is True


@POSIX_ONLY
def test_root_exit_with_live_grandchild_fails_without_signalling_stale_group(tmp_path: Path) -> None:
    pid_file = tmp_path / "grandchild.pid"
    source = (
        "import pathlib,subprocess,sys; "
        "child=subprocess.Popen([sys.executable,'-I','-c','import time; time.sleep(60)']); "
        f"pathlib.Path({str(pid_file)!r}).write_text(str(child.pid),encoding='ascii')"
    )

    with pytest.raises(ProcessSupervisionError, match="cleanup"):
        ProcessSupervisor().run(_spec(tmp_path, source))

    grandchild_pid = int(pid_file.read_text(encoding="ascii"))
    os.kill(grandchild_pid, 9)
    _assert_pid_gone(grandchild_pid)


def test_invalid_spec_fails_without_plausible_result(tmp_path: Path) -> None:
    with pytest.raises(ProcessSupervisionError, match="command"):
        ProcessSupervisor().run(
            ProcessSpec(
                command=(),
                cwd=tmp_path,
                environment={},
                timeout_seconds=1,
            )
        )


@POSIX_ONLY
def test_missing_executable_fails_without_plausible_result(tmp_path: Path) -> None:
    missing = tmp_path / "missing-executable"
    with pytest.raises(ProcessSupervisionError, match="launch"):
        ProcessSupervisor().run(
            ProcessSpec(
                command=(str(missing),),
                cwd=tmp_path,
                environment={},
                timeout_seconds=1,
            )
        )


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("timeout_seconds", float("nan")),
        ("timeout_seconds", float("inf")),
        ("timeout_seconds", True),
        ("graceful_shutdown_seconds", "1"),
        ("output_limit_bytes", 1.5),
    ],
)
def test_invalid_resource_limits_fail_before_launch(
    tmp_path: Path,
    field: str,
    value: object,
) -> None:
    values: dict[str, object] = {
        "command": (sys.executable, "-I", "-c", "pass"),
        "cwd": tmp_path,
        "environment": {},
        "timeout_seconds": 1.0,
        "output_limit_bytes": 1024,
        "graceful_shutdown_seconds": 0.1,
    }
    values[field] = value

    with pytest.raises(ProcessSupervisionError):
        ProcessSupervisor().run(ProcessSpec(**values))  # type: ignore[arg-type]


def test_invalid_environment_key_is_normalized_before_launch(tmp_path: Path) -> None:
    spec = ProcessSpec(
        command=(sys.executable, "-I", "-c", "pass"),
        cwd=tmp_path,
        environment={"BAD=KEY": "value"},
        timeout_seconds=1,
    )

    with pytest.raises(ProcessSupervisionError, match="environment"):
        ProcessSupervisor().run(spec)


def test_pre_cancelled_run_does_not_dispatch(tmp_path: Path) -> None:
    marker = tmp_path / "dispatched"
    cancellation = threading.Event()
    cancellation.set()

    with pytest.raises(ProcessSupervisionError, match="cancelled"):
        ProcessSupervisor().run(
            _spec(tmp_path, f"from pathlib import Path; Path({str(marker)!r}).touch()"),
            cancellation=cancellation,
        )

    assert not marker.exists()


def test_unsupported_platform_is_explicitly_fail_closed(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    monkeypatch.setattr(module.os, "name", "unsupported")

    with pytest.raises(ProcessSupervisionError, match="unsupported"):
        ProcessSupervisor().run(_spec(tmp_path, "pass"))


@POSIX_ONLY
def test_cleanup_proof_failure_cannot_return_success(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    real_cleanup: Callable[..., bool] = module.cleanup_posix

    def fail_after_cleanup(*args: object, **kwargs: object) -> bool:
        real_cleanup(*args, **kwargs)
        raise PosixProcessError("cleanup proof unavailable")

    monkeypatch.setattr(module, "cleanup_posix", fail_after_cleanup)

    with pytest.raises(ProcessSupervisionError, match="cleanup"):
        ProcessSupervisor().run(_spec(tmp_path, "pass"))


def _assert_pid_gone(pid: int) -> None:
    deadline = time.monotonic() + 2
    while time.monotonic() < deadline:
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return
        time.sleep(0.01)
    pytest.fail(f"owned descendant PID {pid} survived cleanup")
