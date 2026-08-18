"""Bounded process-group ownership with fail-closed residual detection."""


import os
import secrets
import threading
import time
from collections import Counter
from dataclasses import replace
from typing import TYPE_CHECKING

from gauntlet.process_contracts import (
    CleanupSummary,
    ProcessResult,
    ProcessSpec,
    ProcessSupervisionError,
    validate_process_spec,
)
from gauntlet.process_inventory import (
    ProcessIdentity,
    ProcessInventoryError,
    capture_process_inventory,
    processes_with_environment,
)
from gauntlet.process_output import (
    OutputCollector as _OutputCollector,
)
from gauntlet.process_output import (
    collector as _collector,
)
from gauntlet.process_output import (
    finish_collectors as _finish_collectors,
)
from gauntlet.process_posix import PosixProcessError, cleanup_posix, group_exists, launch_posix

if TYPE_CHECKING:
    import subprocess

_POLL_SECONDS = 0.01
_RESIDUAL_SAMPLE_SECONDS = 0.05
_RESIDUAL_SAMPLE_COUNT = 5
_RESIDUAL_MIN_OBSERVATIONS = 3
_LEASE_ENVIRONMENT_KEY = "UNITY_MCP_GAUNTLET_LEASE"


class ProcessSupervisor:
    """Run one command while owning its complete POSIX process group."""

    def run(
        self,
        spec: ProcessSpec,
        *,
        cancellation: threading.Event | None = None,
    ) -> ProcessResult:
        normalized = validate_process_spec(spec)
        if cancellation is not None and cancellation.is_set():
            raise ProcessSupervisionError("process launch was cancelled before dispatch")
        if os.name != "posix":
            raise ProcessSupervisionError("whole-tree process ownership is unsupported on this platform")
        try:
            baseline = capture_process_inventory()
        except ProcessInventoryError as exc:
            raise ProcessSupervisionError("process baseline cannot be captured") from exc
        if _LEASE_ENVIRONMENT_KEY in normalized.environment:
            raise ProcessSupervisionError("process environment contains the reserved lease key")
        lease_token = secrets.token_hex(16)
        normalized = replace(
            normalized,
            environment={**normalized.environment, _LEASE_ENVIRONMENT_KEY: lease_token},
        )
        process: subprocess.Popen[bytes] | None = None
        collectors: list[_OutputCollector] = []
        try:
            process = launch_posix(normalized)
            overflow = threading.Event()
            failure = threading.Event()
            stdout = _collector(process.stdout, normalized.output_limit_bytes, overflow, failure)
            collectors.append(stdout)
            stderr = _collector(process.stderr, normalized.output_limit_bytes, overflow, failure)
            collectors.append(stderr)
            stdout.start()
            stderr.start()
        except BaseException as exc:
            if process is None:
                if isinstance(exc, PosixProcessError):
                    raise ProcessSupervisionError(str(exc)) from exc
                raise
            try:
                _cleanup_group(
                    process,
                    normalized.graceful_shutdown_seconds,
                    False,
                    False,
                    baseline,
                    lease_token,
                )
                for collector in collectors:
                    collector.discard_after_cleanup(1.0)
            except ProcessSupervisionError as cleanup_exc:
                raise ProcessSupervisionError("post-launch failure left unproven cleanup") from cleanup_exc
            raise ProcessSupervisionError("process launch initialization failed") from exc
        return self._wait(
            process,
            normalized,
            stdout,
            stderr,
            overflow,
            failure,
            cancellation,
            baseline,
            lease_token,
        )

    def _wait(
        self,
        process: subprocess.Popen[bytes],
        spec: ProcessSpec,
        stdout: _OutputCollector,
        stderr: _OutputCollector,
        overflow: threading.Event,
        failure: threading.Event,
        cancellation: threading.Event | None,
        baseline: frozenset[ProcessIdentity],
        lease_token: str,
    ) -> ProcessResult:
        deadline = time.monotonic() + spec.timeout_seconds
        timed_out = False
        cancelled = False
        root_exited = False
        descendants = False
        try:
            while process.poll() is None:
                cancelled = cancellation is not None and cancellation.is_set()
                timed_out = time.monotonic() >= deadline
                if cancelled or timed_out or overflow.is_set() or failure.is_set():
                    break
                time.sleep(_POLL_SECONDS)
            if time.monotonic() >= deadline:
                timed_out = True
            root_exited = process.poll() is not None
            descendants = root_exited and group_exists(process.pid)
            cleanup = _cleanup_group(
                process,
                spec.graceful_shutdown_seconds,
                root_exited,
                descendants,
                baseline,
                lease_token,
            )
            output_wait = max(spec.graceful_shutdown_seconds, 1.0)
            stdout_summary, stderr_summary = _finish_collectors(stdout, stderr, output_wait)
        except BaseException as exc:
            try:
                retry_root_exited = root_exited or process.poll() is not None
                _cleanup_group(
                    process,
                    spec.graceful_shutdown_seconds,
                    retry_root_exited,
                    descendants,
                    baseline,
                    lease_token,
                )
            except ProcessSupervisionError as cleanup_exc:
                raise ProcessSupervisionError("process failed and cleanup could not be proven") from cleanup_exc
            if isinstance(exc, ProcessSupervisionError):
                raise
            raise
        return ProcessResult(
            return_code=process.returncode,
            timed_out=timed_out,
            cancelled=cancelled,
            output_limit_exceeded=overflow.is_set(),
            stdout=stdout_summary,
            stderr=stderr_summary,
            cleanup=cleanup,
        )


def _cleanup_group(
    process: subprocess.Popen[bytes],
    grace: float,
    root_was_observed_exited: bool,
    descendants_after_root_exit: bool,
    baseline: frozenset[ProcessIdentity],
    lease_token: str,
) -> CleanupSummary:
    try:
        forced = cleanup_posix(
            process,
            grace,
            root_was_observed_exited=root_was_observed_exited,
        )
    except PosixProcessError as exc:
        raise ProcessSupervisionError(str(exc)) from exc
    try:
        residual = _persistent_residuals(baseline, lease_token)
    except ProcessInventoryError as exc:
        raise ProcessSupervisionError("residual process inventory cannot be captured") from exc
    return CleanupSummary(
        process_group_absent=True,
        forced=forced,
        descendants_after_root_exit=descendants_after_root_exit,
        tagged_residual_processes=residual,
        containment_scope="posix_process_group+inherited_environment_marker",
        detached_descendants_proven=False,
    )


def _persistent_residuals(
    baseline: frozenset[ProcessIdentity],
    lease_token: str,
) -> tuple[ProcessIdentity, ...]:
    observations: Counter[ProcessIdentity] = Counter()
    for index in range(_RESIDUAL_SAMPLE_COUNT):
        if index:
            time.sleep(_RESIDUAL_SAMPLE_SECONDS)
        candidates = set(capture_process_inventory() - baseline)
        observations.update(processes_with_environment(candidates, _LEASE_ENVIRONMENT_KEY, lease_token))
    return tuple(
        sorted(identity for identity, count in observations.items() if count >= _RESIDUAL_MIN_OBSERVATIONS)
    )
