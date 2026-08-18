"""POSIX process-group ownership used by the trusted harness."""


import os
import signal
import subprocess
import time
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from gauntlet.process_supervisor import ProcessSpec

_POLL_SECONDS = 0.01


class PosixProcessError(RuntimeError):
    """Raised when a POSIX process group cannot be safely owned."""


def launch_posix(spec: ProcessSpec) -> subprocess.Popen[bytes]:
    """Launch a root in a new session whose process-group ID equals its PID."""
    try:
        process = subprocess.Popen(
            spec.command,
            cwd=spec.cwd,
            env=spec.environment,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            start_new_session=True,
        )
    except OSError as exc:
        raise PosixProcessError("process launch failed") from exc
    if process.pid <= 1 or process.pid == os.getpid():  # pragma: no cover - OS invariant
        process.kill()
        process.wait()
        raise PosixProcessError("process launch returned an unsafe identity")
    try:
        process_group = os.getpgid(process.pid)
        session = os.getsid(process.pid)
    except ProcessLookupError:
        return process
    if process_group != process.pid or session != process.pid:
        process.kill()
        process.wait()
        raise PosixProcessError("process launch did not create an owned session")
    return process


def cleanup_posix(
    process: subprocess.Popen[bytes],
    grace: float,
    *,
    root_was_observed_exited: bool,
) -> bool:
    """Terminate the owned process group, reap the root, and return forced flag."""
    forced = False
    if group_exists(process.pid):
        if root_was_observed_exited:
            raise PosixProcessError("refusing to signal a process group after root identity ended")
        group_was_signalled = _signal_group(process, signal.SIGTERM)
        if not _wait_group_absent(process.pid, grace, process):
            if not group_was_signalled:
                raise PosixProcessError("unverified process group survived its root")
            forced = True
            _signal_group(process, signal.SIGKILL)
            if not _wait_group_absent(process.pid, grace, process):
                raise PosixProcessError("owned process group survived forced cleanup")
    try:
        process.wait(timeout=grace)
    except subprocess.TimeoutExpired as exc:
        raise PosixProcessError("owned root process could not be reaped") from exc
    if group_exists(process.pid):
        raise PosixProcessError("owned process group still exists after cleanup")
    return forced


def group_exists(process_group: int) -> bool:
    """Return whether a process group still has a live or unreaped member."""
    try:
        os.killpg(process_group, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def _signal_group(process: subprocess.Popen[bytes], value: signal.Signals) -> bool:
    process_group = process.pid
    if process_group <= 1 or process_group == os.getpgrp():
        raise PosixProcessError("refusing to signal an unsafe process group")
    try:
        os.killpg(process_group, value)
    except ProcessLookupError:
        return False
    except PermissionError:
        transition_deadline = time.monotonic() + 0.05
        while process.poll() is None and time.monotonic() < transition_deadline:
            time.sleep(0.001)
        if process.returncode is not None:
            return False
        raise PosixProcessError("owned process group denied a signal") from None
    except OSError as exc:
        raise PosixProcessError("owned process group could not be signalled") from exc
    return True


def _wait_group_absent(
    process_group: int,
    timeout: float,
    root: subprocess.Popen[bytes],
) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        root.poll()
        if not group_exists(process_group):
            return True
        time.sleep(_POLL_SECONDS)
    root.poll()
    return not group_exists(process_group)
