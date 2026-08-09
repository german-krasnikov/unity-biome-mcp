"""Typed contracts and validation for supervised process execution."""

from __future__ import annotations

import math
import stat
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from gauntlet.process_inventory import ProcessIdentity


class ProcessSupervisionError(RuntimeError):
    """Raised when launch or cleanup cannot be proven safe."""


@dataclass(frozen=True, slots=True)
class ProcessSpec:
    command: tuple[str, ...]
    cwd: Path
    environment: Mapping[str, str]
    timeout_seconds: float
    output_limit_bytes: int = 1024 * 1024
    graceful_shutdown_seconds: float = 2.0


@dataclass(frozen=True, slots=True)
class OutputSummary:
    tail: bytes
    total_bytes: int
    sha256: str


@dataclass(frozen=True, slots=True)
class CleanupSummary:
    process_group_absent: bool
    forced: bool
    descendants_after_root_exit: bool
    tagged_residual_processes: tuple[ProcessIdentity, ...]
    containment_scope: str
    detached_descendants_proven: bool

    @property
    def scoped_clean(self) -> bool:
        return self.process_group_absent and not self.tagged_residual_processes

    @property
    def release_safe(self) -> bool:
        return self.scoped_clean and self.detached_descendants_proven


@dataclass(frozen=True, slots=True)
class ProcessResult:
    return_code: int | None
    timed_out: bool
    cancelled: bool
    output_limit_exceeded: bool
    stdout: OutputSummary
    stderr: OutputSummary
    cleanup: CleanupSummary

    @property
    def completed_within_scope(self) -> bool:
        return (
            self.return_code == 0
            and not self.timed_out
            and not self.cancelled
            and not self.output_limit_exceeded
            and not self.cleanup.descendants_after_root_exit
            and self.cleanup.scoped_clean
        )


def validate_process_spec(spec: ProcessSpec) -> ProcessSpec:
    """Snapshot and validate every caller-controlled launch field."""
    if not isinstance(spec.command, tuple) or not spec.command or any(
        not isinstance(value, str) or not value or "\0" in value for value in spec.command
    ):
        raise ProcessSupervisionError("process command must contain non-empty strings")
    environment = _environment(spec.environment)
    _time_limits(spec.timeout_seconds, spec.graceful_shutdown_seconds)
    if (
        isinstance(spec.output_limit_bytes, bool)
        or not isinstance(spec.output_limit_bytes, int)
        or spec.output_limit_bytes <= 0
    ):
        raise ProcessSupervisionError("process output limit must be positive")
    if not isinstance(spec.cwd, Path):
        raise ProcessSupervisionError("process cwd must be a path")
    try:
        metadata = spec.cwd.lstat()
        cwd = spec.cwd.resolve(strict=True)
    except OSError as exc:
        raise ProcessSupervisionError("process cwd is not accessible") from exc
    if not stat.S_ISDIR(metadata.st_mode) or spec.cwd.is_symlink():
        raise ProcessSupervisionError("process cwd must be a real directory")
    return ProcessSpec(
        command=spec.command,
        cwd=cwd,
        environment=environment,
        timeout_seconds=float(spec.timeout_seconds),
        output_limit_bytes=spec.output_limit_bytes,
        graceful_shutdown_seconds=float(spec.graceful_shutdown_seconds),
    )


def _environment(value: Mapping[str, str]) -> dict[str, str]:
    if not isinstance(value, Mapping):
        raise ProcessSupervisionError("process environment must contain valid strings")
    try:
        environment = dict(value)
    except (TypeError, ValueError) as exc:
        raise ProcessSupervisionError("process environment cannot be captured") from exc
    if any(
        not isinstance(key, str)
        or not key
        or "=" in key
        or not isinstance(item, str)
        or "\0" in key + item
        for key, item in environment.items()
    ):
        raise ProcessSupervisionError("process environment must contain valid strings")
    return environment


def _time_limits(timeout: object, grace: object) -> None:
    if any(isinstance(value, bool) or not isinstance(value, (int, float)) for value in (timeout, grace)):
        raise ProcessSupervisionError("process time limits must be finite numbers")
    if not math.isfinite(timeout) or not math.isfinite(grace) or timeout <= 0 or grace <= 0:
        raise ProcessSupervisionError("process time limits must be positive")
