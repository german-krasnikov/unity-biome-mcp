# Generated from dsl/data/backends.data-contract, dsl/data/backends.core-data
# source-sha256=686c7ad27fdb5a95299d6ed57eb00f5cce6fdcc1563cf7f6df29bd31f580fb43
# Edit the source, then run biome generate.

from dataclasses import dataclass
from enum import StrEnum


class SessionFormat(StrEnum):
    JSONL = "jsonl"
    CODEX = "codex"
    AGY = "agy"
    NONE = "none"


@dataclass(frozen=True, slots=True, kw_only=True)
class Backend:
    kind: str
    id: str
    display: str
    binary: str
    prefKey: str
    resume: str
    resumeCd: bool
    sessionDir: tuple[str, ...]
    sessionFormat: SessionFormat
    contextFallback: int


BACKEND_ROWS: tuple[Backend, ...] = (
    Backend(
        kind="Claude",
        id="claude",
        display="Claude",
        binary="claude",
        prefKey="UnityMCP_Chat_ClaudePath",
        resume="claude --resume {id}",
        resumeCd=True,
        sessionDir=(".claude", "projects", "{cwd}"),
        sessionFormat=SessionFormat.JSONL,
        contextFallback=200000,
    ),
    Backend(
        kind="Codex",
        id="codex",
        display="Codex",
        binary="codex",
        prefKey="UnityMCP_Chat_Path_codex",
        resume="codex resume {id}",
        resumeCd=False,
        sessionDir=(".codex", "sessions"),
        sessionFormat=SessionFormat.CODEX,
        contextFallback=1000000,
    ),
    Backend(
        kind="Antigravity",
        id="antigravity",
        display="Antigravity",
        binary="agy",
        prefKey="UnityMCP_Chat_Path_agy",
        resume="agy --conversation {id}",
        resumeCd=False,
        sessionDir=(".gemini", "antigravity-cli", "conversations"),
        sessionFormat=SessionFormat.AGY,
        contextFallback=1000000,
    ),
    Backend(
        kind="Kimi",
        id="kimi",
        display="Kimi",
        binary="kimi",
        prefKey="UnityMCP_Chat_Path_kimi",
        resume="kimi -S {id}",
        resumeCd=False,
        sessionDir=(".kimi-code", "sessions"),
        sessionFormat=SessionFormat.JSONL,
        contextFallback=128000,
    ),
    Backend(
        kind="OpenCode",
        id="opencode",
        display="OpenCode",
        binary="opencode",
        prefKey="UnityMCP_Chat_Path_opencode",
        resume="opencode -s {id}",
        resumeCd=False,
        sessionDir=(),
        sessionFormat=SessionFormat.NONE,
        contextFallback=0,
    ),
)
