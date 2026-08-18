"""Content-addressed mutation helpers for release-gate negative tests."""


import hashlib
import json
from typing import TYPE_CHECKING

from gauntlet.attestations import build_file_reference, build_receipt
from gauntlet.evidence_schema import evidence_hash
from gauntlet_test_fixtures import write_json

if TYPE_CHECKING:
    from collections.abc import Callable
    from pathlib import Path


def mutate_evidence(path: Path, mutate: Callable[[dict[str, object]], None]) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    mutate(data)
    data.pop("evidence_hash")
    data["evidence_hash"] = evidence_hash(data)
    path.write_text(json.dumps(data), encoding="utf-8")


def rewrite_receipt(
    paths: dict[str, Path],
    path_key: str,
    kind: str,
    updates: dict[str, object],
) -> None:
    path = paths[path_key]
    raw = json.loads(path.read_text(encoding="utf-8"))
    fields = {
        key: value
        for key, value in raw.items()
        if key not in {"schema_version", "kind", "receipt_hash"}
    }
    fields.update(updates)
    write_json(path, build_receipt(kind, fields))
    reference = build_file_reference(path, paths["bundle"]).as_dict()

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        if path_key == "runtime":
            artifacts["runtime"] = reference
        elif path_key == "worker":
            artifacts["workers"] = [reference]
        else:
            cleanup = artifacts["cleanup"]
            assert isinstance(cleanup, list)
            cleanup[0 if path_key == "cleanup_process" else 1] = reference

    mutate_evidence(paths["evidence"], update)


def refresh_junit_and_runtime(paths: dict[str, Path]) -> None:
    rewrite_receipt(paths, "runtime", "runtime", {"junit_sha": _sha256(paths["junit"])})

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        artifacts["junit"] = build_file_reference(paths["junit"], paths["bundle"]).as_dict()

    mutate_evidence(paths["evidence"], update)


def refresh_journal_and_runtime(paths: dict[str, Path]) -> None:
    rewrite_receipt(paths, "runtime", "runtime", {"journal_sha": _sha256(paths["journal"])})

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        artifacts["journal"] = build_file_reference(paths["journal"], paths["bundle"]).as_dict()

    mutate_evidence(paths["evidence"], update)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()
