"""Adversarial process escapes and post-launch fault injection."""

from __future__ import annotations

import os
import sys
import time
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from gauntlet.process_inventory import ProcessIdentity  # noqa: E402
from gauntlet.process_supervisor import (  # noqa: E402
    CleanupSummary,
    ProcessSpec,
    ProcessSupervisionError,
    ProcessSupervisor,
)

pytestmark = pytest.mark.skipif(os.name != "posix", reason="POSIX process-group behavior")


def _spec(tmp_path: Path, source: str) -> ProcessSpec:
    return ProcessSpec(
        command=(sys.executable, "-I", "-c", source),
        cwd=tmp_path,
        environment={"PATH": os.environ.get("PATH", ""), "PYTHONUTF8": "1"},
        timeout_seconds=5,
        graceful_shutdown_seconds=0.2,
    )


def test_collector_start_failure_still_cleans_launched_process(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    launched: list[int] = []
    start_calls = 0
    real_launch = module.launch_posix
    real_start = module._OutputCollector.start

    def capture_launch(spec: ProcessSpec) -> object:
        process = real_launch(spec)
        launched.append(process.pid)
        return process

    def fail_second_start(collector: object) -> None:
        nonlocal start_calls
        start_calls += 1
        if start_calls == 2:
            raise RuntimeError("synthetic collector failure")
        real_start(collector)

    monkeypatch.setattr(module, "launch_posix", capture_launch)
    monkeypatch.setattr(module._OutputCollector, "start", fail_second_start)

    with pytest.raises(ProcessSupervisionError, match="initialization"):
        ProcessSupervisor().run(_spec(tmp_path, "import time; time.sleep(60)"))

    assert len(launched) == 1
    _assert_pid_gone(launched[0])


def test_detached_session_descendant_cannot_return_clean(tmp_path: Path) -> None:
    source = (
        "import subprocess,sys; "
        "child=subprocess.Popen([sys.executable,'-I','-c','import time; time.sleep(60)'],"
        "start_new_session=True,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,"
        "stderr=subprocess.DEVNULL); print(child.pid,flush=True)"
    )

    result = ProcessSupervisor().run(_spec(tmp_path, source))
    escaped_pid = int(result.stdout.tail.strip())
    try:
        assert result.completed_within_scope is False
        assert result.cleanup.scoped_clean is False
        assert escaped_pid in {identity.pid for identity in result.cleanup.tagged_residual_processes}
    finally:
        os.kill(escaped_pid, 9)
        _assert_pid_gone(escaped_pid)


def test_sanitized_environment_escape_is_never_release_safe(tmp_path: Path) -> None:
    source = (
        "import os,subprocess,sys; "
        "child=subprocess.Popen([sys.executable,'-I','-c','import time; time.sleep(60)'],"
        "start_new_session=True,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,"
        "stderr=subprocess.DEVNULL,env={'PATH':os.environ.get('PATH','')}); "
        "print(child.pid,flush=True)"
    )

    result = ProcessSupervisor().run(_spec(tmp_path, source))
    escaped_pid = int(result.stdout.tail.strip())
    try:
        assert result.cleanup.release_safe is False
        assert result.cleanup.detached_descendants_proven is False
    finally:
        os.kill(escaped_pid, 9)
        _assert_pid_gone(escaped_pid)


def test_collector_read_failure_wakes_runner_and_cleans_process_group(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    real_drain = module._OutputCollector._drain
    real_cleanup = module._cleanup_group
    calls = 0
    cleanup_root_states: list[bool] = []

    def fail_first_read(collector: object) -> None:
        nonlocal calls
        calls += 1
        if calls == 1:
            collector._error = OSError("synthetic read failure")
            collector._failure.set()
            collector._stream.close()
            return
        real_drain(collector)

    def capture_cleanup(
        process: object,
        grace: float,
        root_exited: bool,
        descendants: bool,
        baseline: object,
        lease_token: str,
    ) -> CleanupSummary:
        cleanup_root_states.append(root_exited)
        return real_cleanup(process, grace, root_exited, descendants, baseline, lease_token)

    monkeypatch.setattr(module._OutputCollector, "_drain", fail_first_read)
    monkeypatch.setattr(module, "_cleanup_group", capture_cleanup)
    started = time.monotonic()

    with pytest.raises(ProcessSupervisionError, match="output"):
        ProcessSupervisor().run(_spec(tmp_path, "import time; time.sleep(60)"))

    assert time.monotonic() - started < 2
    assert cleanup_root_states == [False, True]


def test_observed_root_exit_is_forwarded_separately_from_group_probe(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    observed: list[tuple[bool, bool]] = []

    def capture_cleanup(
        process: object,
        grace: float,
        root_exited: bool,
        descendants: bool,
        baseline: object,
        lease_token: str,
    ) -> CleanupSummary:
        observed.append((root_exited, descendants))
        return CleanupSummary(True, False, descendants, (), "test_scope", False)

    monkeypatch.setattr(module, "group_exists", lambda _: False)
    monkeypatch.setattr(module, "_cleanup_group", capture_cleanup)

    result = ProcessSupervisor().run(_spec(tmp_path, "pass"))

    assert result.completed_within_scope is True
    assert observed == [(True, False)]


def test_residual_oracle_ignores_transient_identity(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    baseline = frozenset({ProcessIdentity(1, "old")})
    transient = ProcessIdentity(2, "short")
    observations = iter((baseline | {transient}, baseline, baseline, baseline, baseline))
    monkeypatch.setattr(module, "capture_process_inventory", lambda: next(observations))
    monkeypatch.setattr(module.time, "sleep", lambda _: None)

    monkeypatch.setattr(module, "processes_with_environment", lambda values, *_: frozenset(values))

    assert module._persistent_residuals(baseline, "lease") == ()


def test_residual_oracle_keeps_persistent_identity(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    baseline = frozenset({ProcessIdentity(1, "old")})
    leaked = ProcessIdentity(2, "persistent")
    monkeypatch.setattr(module, "capture_process_inventory", lambda: baseline | {leaked})
    monkeypatch.setattr(module, "processes_with_environment", lambda values, *_: frozenset(values))
    monkeypatch.setattr(module.time, "sleep", lambda _: None)

    assert module._persistent_residuals(baseline, "lease") == (leaked,)


def test_residual_oracle_tracks_daemon_handoff(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    import gauntlet.process_supervisor as module

    baseline = frozenset({ProcessIdentity(1, "old")})
    first = ProcessIdentity(2, "first")
    replacement = ProcessIdentity(3, "replacement")
    observations = iter(
        (
            baseline | {first},
            baseline | {replacement},
            baseline | {replacement},
            baseline | {replacement},
            baseline | {replacement},
        )
    )
    monkeypatch.setattr(module, "capture_process_inventory", lambda: next(observations))
    monkeypatch.setattr(module, "processes_with_environment", lambda values, *_: frozenset(values))
    monkeypatch.setattr(module.time, "sleep", lambda _: None)

    assert module._persistent_residuals(baseline, "lease") == (replacement,)


def _assert_pid_gone(pid: int) -> None:
    deadline = time.monotonic() + 2
    while time.monotonic() < deadline:
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return
        time.sleep(0.01)
    pytest.fail(f"owned process PID {pid} survived test cleanup")
