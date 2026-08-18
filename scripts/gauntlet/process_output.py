"""Bounded output collection for supervised subprocesses."""


import hashlib
import threading
from typing import BinaryIO

from gauntlet.process_contracts import OutputSummary, ProcessSupervisionError

_READ_CHUNK_BYTES = 64 * 1024


class OutputCollector:
    def __init__(
        self,
        stream: BinaryIO,
        limit: int,
        overflow: threading.Event,
        failure: threading.Event,
    ) -> None:
        self._stream = stream
        self._limit = limit
        self._overflow = overflow
        self._failure = failure
        self._tail = bytearray()
        self._total = 0
        self._digest = hashlib.sha256()
        self._error: BaseException | None = None
        self._thread = threading.Thread(target=self._drain, daemon=True)
        self._started = False

    def start(self) -> None:
        self._thread.start()
        self._started = True

    def discard_after_cleanup(self, timeout: float) -> None:
        if not self._started:
            self._stream.close()
            return
        self._thread.join(timeout)
        if self._thread.is_alive():
            raise ProcessSupervisionError("process output collector survived cleanup")

    def finish(self, timeout: float) -> OutputSummary:
        self._thread.join(timeout)
        if self._thread.is_alive():
            raise ProcessSupervisionError("process output collector did not stop")
        if self._error is not None:
            raise ProcessSupervisionError("process output could not be collected") from self._error
        return OutputSummary(bytes(self._tail), self._total, self._digest.hexdigest())

    def _drain(self) -> None:
        try:
            while chunk := self._stream.read(_READ_CHUNK_BYTES):
                self._total += len(chunk)
                self._digest.update(chunk)
                self._tail.extend(chunk)
                if len(self._tail) > self._limit:
                    del self._tail[: len(self._tail) - self._limit]
                if self._total > self._limit:
                    self._overflow.set()
        except BaseException as exc:  # pragma: no cover - defensive OS boundary
            self._error = exc
            self._failure.set()
        finally:
            self._stream.close()


def collector(
    stream: BinaryIO | None,
    limit: int,
    overflow: threading.Event,
    failure: threading.Event,
) -> OutputCollector:
    if stream is None:  # pragma: no cover - Popen contract
        raise ProcessSupervisionError("process output pipe is unavailable")
    return OutputCollector(stream, limit, overflow, failure)


def finish_collectors(
    stdout: OutputCollector,
    stderr: OutputCollector,
    timeout: float,
) -> tuple[OutputSummary, OutputSummary]:
    first_error: ProcessSupervisionError | None = None
    stdout_summary: OutputSummary | None = None
    stderr_summary: OutputSummary | None = None
    try:
        stdout_summary = stdout.finish(timeout)
    except ProcessSupervisionError as exc:
        first_error = exc
    try:
        stderr_summary = stderr.finish(timeout)
    except ProcessSupervisionError as exc:
        first_error = first_error or exc
    if first_error is not None:
        raise first_error
    if stdout_summary is None or stderr_summary is None:  # pragma: no cover - exhaustive guard
        raise ProcessSupervisionError("process output summary is incomplete")
    return stdout_summary, stderr_summary
