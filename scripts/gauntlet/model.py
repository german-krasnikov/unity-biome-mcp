from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Mapping


class EffectDomain(str, Enum):
    """State domains a public operation may affect."""

    PURE_READ = "pure_read"
    OBSERVER_STATE = "observer_state"
    RUNTIME_STATE = "runtime_state"
    UNITY_PERSISTENT = "unity_persistent"
    FILESYSTEM = "filesystem"
    PROCESS_CONTROL = "process_control"
    LIFECYCLE = "lifecycle"
    EXTERNAL_SERVICE = "external_service"


class Verdict(str, Enum):
    PASS = "pass"
    FAIL = "fail"
    BLOCKED = "blocked"
    ERROR = "error"


@dataclass(frozen=True, slots=True)
class Identity:
    worker_id: str
    project_path: str
    port: int
    protocol_version: str
    plugin_version: str
    server_version: str
    source_sha: str


@dataclass(frozen=True, slots=True)
class Snapshot:
    identity: Identity
    protected_hash: str
    state: Mapping[str, object] = field(default_factory=dict)


@dataclass(frozen=True, slots=True)
class ToolResult:
    is_error: bool
    text: str
    code: str | None = None
    data: Mapping[str, object] = field(default_factory=dict)


@dataclass(frozen=True, slots=True)
class Contract:
    contract_id: str
    action: str
    effects: frozenset[EffectDomain]
    arguments: Mapping[str, object] = field(default_factory=dict)
    required_project: str | None = None
    expect_error: bool = False
    forbidden_success_patterns: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if not self.contract_id:
            raise ValueError("contract_id must not be empty")
        if not self.action:
            raise ValueError("action must not be empty")
        if not self.effects:
            raise ValueError("effects must not be empty")


@dataclass(frozen=True, slots=True)
class ScenarioResult:
    contract_id: str
    verdict: Verdict
    reasons: tuple[str, ...] = ()
