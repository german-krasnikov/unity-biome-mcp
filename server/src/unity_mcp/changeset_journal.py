"""T15: Append-only JSONL journal per session. Dedup by operation_id."""

import json
from pathlib import Path

from .changeset import ChangeOperation, ChangeSet  # noqa: TC001


class ChangeSetJournal:
    def __init__(self, session_id: str, _dir: Path | None = None) -> None:
        from .paths import journals_dir

        base = _dir if _dir is not None else journals_dir()
        base.mkdir(parents=True, exist_ok=True)
        safe_id = Path(session_id).name
        self._path = base / f"{safe_id}.jsonl"
        self._fh = self._path.open("a", encoding="utf-8")
        self._seen_op_ids: set[str] = set()

    def write_header(self, cs: ChangeSet) -> None:
        record = {
            "_t": "cs",
            "id": cs.changeset_id,
            "sid": cs.session_id,
            "tid": cs.turn_id,
            "status": cs.status,
            "ts": cs.created_at,
        }
        self._fh.write(json.dumps(record, separators=(",", ":")) + "\n")
        self._fh.flush()

    def write_operation(self, op: ChangeOperation, changeset_id: str) -> None:
        if op.operation_id in self._seen_op_ids:
            return
        self._seen_op_ids.add(op.operation_id)
        record: dict = {
            "_t": "op",
            "csid": changeset_id,
            "id": op.operation_id,
            "kind": op.kind,
            "ttype": op.target_type,
            "path": op.target_path,
            "rev": op.reversible,
            "ts": op.timestamp,
        }
        if op.prop is not None:
            record["prop"] = op.prop
        if op.before_ref is not None:
            record["bh"] = op.before_ref.hash16
        if op.after_ref is not None:
            record["ah"] = op.after_ref.hash16
        self._fh.write(json.dumps(record, separators=(",", ":")) + "\n")
        self._fh.flush()

    def close(self) -> None:
        if not self._fh.closed:
            self._fh.close()
