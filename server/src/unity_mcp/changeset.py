"""T15: ChangeSet, ChangeOperation, ContentRef — immutable value objects. No I/O."""

import hashlib
import uuid
from dataclasses import dataclass, field
from datetime import UTC, datetime
from typing import Literal

OperationKind = Literal["create", "modify", "delete"]
TargetType = Literal["scene_object", "component", "property", "asset"]
ChangeSetStatus = Literal["open", "finalized", "reverted"]


@dataclass(frozen=True)
class ContentRef:
    hash16: str  # sha256(value_bytes).hex()[:16]

    @staticmethod
    def of(value: str | None) -> ContentRef | None:
        if value is None:
            return None
        return ContentRef(hashlib.sha256(value.encode()).hexdigest()[:16])


@dataclass(frozen=True)
class ChangeOperation:
    operation_id: str
    kind: OperationKind
    target_type: TargetType
    target_path: str
    prop: str | None
    before_ref: ContentRef | None
    after_ref: ContentRef | None
    reversible: bool
    timestamp: str  # ISO 8601 UTC

    @staticmethod
    def from_receipt(receipt: dict) -> ChangeOperation:
        return ChangeOperation(
            operation_id=str(uuid.uuid4()),
            kind=receipt.get("op", "modify"),
            target_type=receipt.get("t", "scene_object"),
            target_path=receipt.get("path", ""),
            prop=receipt.get("prop"),
            before_ref=ContentRef.of(receipt.get("before")),
            after_ref=ContentRef.of(receipt.get("after")),
            reversible=bool(receipt.get("rev", True)),
            timestamp=datetime.now(UTC).isoformat(),
        )


@dataclass
class ChangeSet:
    changeset_id: str
    session_id: str
    turn_id: int = 0
    operations: list[ChangeOperation] = field(default_factory=list)
    status: ChangeSetStatus = "open"
    created_at: str = ""
    finalized_at: str | None = None
