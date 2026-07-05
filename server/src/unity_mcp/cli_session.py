"""Subprocess wrapper for one CLI backend session."""
import asyncio
import os
import socket
from dataclasses import dataclass, field

KILL_WAIT = 2.0        # seconds between SIGTERM and SIGKILL
PPID_POLL = 5.0        # orphan watchdog interval
MAX_FRAME = 10_000_000  # 10 MB


@dataclass
class BufLine:
    seq:  int
    text: str


@dataclass
class SessionMeta:
    """Tracks what was spawned so _cmd_set_mode can respawn with new mode."""
    backend:    str
    mode:       str
    model:      str | None
    mcp_port:   int
    prompt:     str
    config_dir: str | None
    extra:      dict = field(default_factory=dict)


class CliSession:
    """One CLI subprocess. Lifecycle: spawn → write → drain → kill."""

    def __init__(self, binary: str, argv: list[str],
                 env_set: dict, env_strip: list[str]):
        self._binary    = binary
        self._argv      = argv
        self._env_set   = env_set
        self._env_strip = env_strip
        self._proc: asyncio.subprocess.Process | None = None

    async def start(self) -> None:
        env = os.environ.copy()
        for k in self._env_strip:
            env.pop(k, None)
        # Prepend the login-shell PATH so node-based CLIs (codex = `env node`) and
        # their child tools resolve — Unity's inherited PATH is minimal (no node).
        from .backend_def import login_shell_path
        login_path = await login_shell_path()
        if login_path:
            env["PATH"] = login_path + os.pathsep + env.get("PATH", "")
        env.update(self._env_set)
        self._proc = await asyncio.create_subprocess_exec(
            self._binary, *self._argv,
            stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,   # captured — surfaces backend crash reason to chat
            # 16 MiB line limit: codex/CLI emit large single-line NDJSON tool results
            # (full scene hierarchy). Default 64 KiB overflows → readline ValueError.
            limit=16 * 1024 * 1024,
            env=env,
        )

    async def write_line(self, line: str) -> None:
        if not self.alive:
            raise RuntimeError(f"process dead (exit={self._proc.returncode})")
        data = (line + "\n").encode("utf-8")
        self._proc.stdin.write(data)
        try:
            await asyncio.wait_for(self._proc.stdin.drain(), timeout=5.0)
        except asyncio.TimeoutError:
            pass  # data was already written; kernel buffer full — continue

    async def read_stdout_line(self) -> str | None:
        """Read one stdout line. Returns None on EOF."""
        if self._proc is None or self._proc.stdout is None:
            return None
        try:
            line = await self._proc.stdout.readline()
            return line.decode("utf-8", errors="replace").rstrip("\n") if line else None
        except (asyncio.IncompleteReadError, ConnectionError):
            return None

    async def kill(self) -> None:
        if self._proc is None:
            return
        try:
            self._proc.terminate()
            await asyncio.wait_for(self._proc.wait(), timeout=KILL_WAIT)
        except (asyncio.TimeoutError, ProcessLookupError):
            try:
                self._proc.kill()
            except ProcessLookupError:
                pass

    def close_stdin(self) -> None:
        if self._proc and self._proc.stdin:
            self._proc.stdin.close()

    async def wait(self, timeout: float = 2.0) -> None:
        """Wait for the process to exit so returncode is populated (avoids EOF/returncode race)."""
        if self._proc is not None:
            try:
                await asyncio.wait_for(self._proc.wait(), timeout=timeout)
            except asyncio.TimeoutError:
                pass

    async def drain_stderr(self, max_bytes: int = 2048, timeout: float = 1.0) -> str:
        """Read captured stderr (backend crash reason). Empty string if none/unavailable."""
        if self._proc is None or self._proc.stderr is None:
            return ""
        try:
            data = await asyncio.wait_for(self._proc.stderr.read(max_bytes), timeout=timeout)
            return data.decode("utf-8", errors="replace").strip()
        except (asyncio.TimeoutError, Exception):
            return ""

    @property
    def alive(self) -> bool:
        return self._proc is not None and self._proc.returncode is None

    @property
    def pid(self) -> int | None:
        return self._proc.pid if self._proc else None

    @property
    def exit_code(self) -> int | None:
        return self._proc.returncode if self._proc else None


def _find_free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]
