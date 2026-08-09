"""Tests for content-addressed runtime, worker, and cleanup evidence."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from gauntlet.attestations import (
    AttestationError,
    build_file_reference,
    build_receipt,
    parse_file_reference,
    parse_receipt_bytes,
    read_verified_file,
)


def _receipt_bytes(value: dict[str, object]) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def test_file_reference_is_bundle_relative_and_content_addressed(tmp_path: Path) -> None:
    bundle = tmp_path / "bundle"
    evidence = bundle / "profile" / "result.json"
    evidence.parent.mkdir(parents=True)
    evidence.write_bytes(b"result")

    reference = build_file_reference(evidence, bundle)
    parsed = parse_file_reference(reference.as_dict())

    assert parsed.relative_path == "profile/result.json"
    assert read_verified_file(parsed, bundle) == b"result"


def test_verified_file_rejects_substitution_and_escape(tmp_path: Path) -> None:
    bundle = tmp_path / "bundle"
    bundle.mkdir()
    evidence = bundle / "result.json"
    evidence.write_bytes(b"before")
    reference = build_file_reference(evidence, bundle)
    evidence.write_bytes(b"after")

    with pytest.raises(AttestationError, match="size|digest"):
        read_verified_file(reference, bundle)
    with pytest.raises(AttestationError, match="normalized"):
        parse_file_reference({"path": "../outside", "sha256": "a" * 64, "size_bytes": 1})
    with pytest.raises(AttestationError, match="normalized"):
        parse_file_reference({"path": "nested//result", "sha256": "a" * 64, "size_bytes": 1})


def test_verified_file_rejects_declared_oversize_before_open(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    bundle = tmp_path / "bundle"
    bundle.mkdir()
    evidence = bundle / "large.bin"
    evidence.write_bytes(b"12345")
    reference = build_file_reference(evidence, bundle)
    opened = False
    original_open = Path.open

    def observed_open(path: Path, *args: object, **kwargs: object) -> object:
        nonlocal opened
        if path == evidence:
            opened = True
        return original_open(path, *args, **kwargs)

    monkeypatch.setattr(Path, "open", observed_open)
    with pytest.raises(AttestationError, match="size limit"):
        read_verified_file(reference, bundle, max_bytes=4)
    assert opened is False


def test_runtime_receipt_round_trip_is_strict() -> None:
    receipt = build_receipt(
        "runtime",
        {
            "profile": "public-stdio-linux",
            "run_id": "run-runtime",
            "run_manifest_sha": "0" * 64,
            "driver": "public_stdio",
            "head_sha": "a" * 40,
            "os": "linux",
            "python": "3.10",
            "unity": None,
            "plugin_scope": "none",
            "consumed_artifacts": {"python_wheel": "b" * 64},
            "junit_sha": "c" * 64,
            "journal_sha": "d" * 64,
            "exit_code": 0,
        },
    )

    assert parse_receipt_bytes(_receipt_bytes(receipt), "runtime") == receipt


def test_worker_and_cleanup_receipts_require_authoritative_clean_state() -> None:
    worker = build_receipt(
        "worker_identity",
        {
            "profile": "unity-dual",
            "run_id": "run-unity-dual",
            "run_manifest_sha": "0" * 64,
            "role": "worker-a",
            "worker_id": "worker-a-epoch-1",
            "project_id": "c" * 64,
            "port": 9500,
            "os": "macos",
            "unity": "6000.0.65f1",
            "plugin_scope": "exact",
            "loaded_artifacts": {"unity_upm": "d" * 64},
            "clean_before": True,
        },
    )
    cleanup = build_receipt(
        "cleanup",
        {
            "profile": "unity-dual",
            "run_id": "run-unity-dual",
            "run_manifest_sha": "0" * 64,
            "obligation": "worker-a",
            "clean": True,
            "details_hash": "e" * 64,
        },
    )

    assert parse_receipt_bytes(_receipt_bytes(worker), "worker_identity") == worker
    assert parse_receipt_bytes(_receipt_bytes(cleanup), "cleanup") == cleanup

    with pytest.raises(AttestationError, match="clean"):
        build_receipt(
            "cleanup",
            {
                "profile": "unity-dual",
                "run_id": "run-unity-dual",
                "run_manifest_sha": "0" * 64,
                "obligation": "worker-a",
                "clean": False,
                "details_hash": "e" * 64,
            },
        )


def test_receipt_rejects_tamper_wrong_kind_and_unknown_fields() -> None:
    receipt = build_receipt(
        "cleanup",
        {
            "profile": "public-stdio",
            "run_id": "run-stdio",
            "run_manifest_sha": "0" * 64,
            "obligation": "stdio-process",
            "clean": True,
            "details_hash": "f" * 64,
        },
    )
    tampered = dict(receipt)
    tampered["obligation"] = "other"

    with pytest.raises(AttestationError, match="hash"):
        parse_receipt_bytes(_receipt_bytes(tampered), "cleanup")
    with pytest.raises(AttestationError, match="expected runtime"):
        parse_receipt_bytes(_receipt_bytes(receipt), "runtime")
    with pytest.raises(AttestationError, match="schema"):
        parse_receipt_bytes(
            _receipt_bytes({**receipt, "unexpected": True}),
            "cleanup",
        )


def test_receipt_rejects_duplicate_json_keys_before_semantic_validation() -> None:
    with pytest.raises(AttestationError, match="duplicate key"):
        parse_receipt_bytes(
            b'{"kind":"cleanup","kind":"runtime"}',
            "cleanup",
        )
