from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.journal_validation import validate_terminal_journal  # noqa: E402
from gauntlet.receipts import (  # noqa: E402
    JournalError,
    ReceiptJournal,
    content_hash,
    verify_journal,
    verify_journal_bytes,
)

SCENARIO = "tests.contracts::test_identity"
RUN_MANIFEST_SHA = "f" * 64
LIFECYCLE = (
    "identity_verified",
    "scenario_started",
    "intent_recorded",
    "request_transmitted",
    "action_observed",
    "post_state_observed",
    "cleanup_observed",
    "scenario_finished",
)
EVENT_DIGEST_FIELDS = {
    "identity_verified": "identity_hash",
    "scenario_started": "precondition_hash",
    "intent_recorded": "intent_hash",
    "request_transmitted": "request_hash",
    "action_observed": "response_hash",
    "post_state_observed": "state_hash",
    "cleanup_observed": "cleanup_hash",
}


def _terminal_payload() -> dict[str, object]:
    return {
        "verdict": "pass",
        "scenario_count": 1,
        "scenario_manifest_sha": content_hash([SCENARIO]),
    }


def _validate_terminal(events: list[dict[str, object]]) -> None:
    validate_terminal_journal(
        events,
        (SCENARIO,),
        expected_profile="public-stdio",
        expected_run_manifest_sha=RUN_MANIFEST_SHA,
        expected_worker_roles=(),
    )


def test_receipt_journal_is_append_only_and_hash_chained(tmp_path: Path) -> None:
    path = tmp_path / "run.jsonl"
    journal = ReceiptJournal(path, run_id="run-123", clock=lambda: "2026-08-09T00:00:00Z")

    first = journal.append("run_started", {"profile": "public-stdio"})
    second = journal.append("action_finished", {"verdict": "PASS"})

    assert first["seq"] == 1
    assert first["prev_hash"] == "0" * 64
    assert second["seq"] == 2
    assert second["prev_hash"] == first["event_hash"]
    assert verify_journal(path) == [first, second]


def test_receipt_journal_can_resume_only_the_same_run(tmp_path: Path) -> None:
    path = tmp_path / "run.jsonl"
    ReceiptJournal(path, run_id="run-a").append("run_started", {})

    resumed = ReceiptJournal(path, run_id="run-a")
    event = resumed.append("run_finished", {"verdict": "PASS"})
    assert event["seq"] == 2

    with pytest.raises(JournalError, match="run_id"):
        ReceiptJournal(path, run_id="run-b")


def test_receipt_verifier_rejects_tampering(tmp_path: Path) -> None:
    path = tmp_path / "run.jsonl"
    journal = ReceiptJournal(path, run_id="run-123")
    journal.append(
        "run_started",
        {"profile": "public-stdio", "run_manifest_sha": RUN_MANIFEST_SHA},
    )
    journal.append("run_finished", {"verdict": "PASS"})

    lines = path.read_text(encoding="utf-8").splitlines()
    record = json.loads(lines[0])
    record["payload"]["profile"] = "tampered"
    lines[0] = json.dumps(record, sort_keys=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    with pytest.raises(JournalError, match="hash"):
        verify_journal(path)


def test_receipt_verifier_rejects_sequence_gaps(tmp_path: Path) -> None:
    path = tmp_path / "run.jsonl"
    event = ReceiptJournal(path, run_id="run-123").append("run_started", {})
    event["seq"] = 2
    path.write_text(json.dumps(event) + "\n", encoding="utf-8")

    with pytest.raises(JournalError, match="sequence"):
        verify_journal(path)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda event: event.update({"schema_version": 999}),
        lambda event: event.update({"unexpected": "accepted-before-hardening"}),
    ],
)
def test_receipt_verifier_rejects_unsupported_envelope_even_when_rehashed(
    tmp_path: Path,
    mutate: object,
) -> None:
    path = tmp_path / "unsupported-envelope.jsonl"
    ReceiptJournal(path, run_id="run-envelope").append("run_started", {})
    event = json.loads(path.read_text(encoding="utf-8"))
    assert callable(mutate)
    mutate(event)
    event_without_hash = {key: value for key, value in event.items() if key != "event_hash"}
    encoded = json.dumps(
        event_without_hash,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    event["event_hash"] = hashlib.sha256(encoded).hexdigest()
    path.write_text(json.dumps(event) + "\n", encoding="utf-8")

    with pytest.raises(JournalError, match="schema|fields"):
        verify_journal(path)


def test_terminal_journal_rejects_empty_or_truncated_payload(tmp_path: Path) -> None:
    assert verify_journal_bytes(b"") == []
    with pytest.raises(JournalError, match="empty"):
        _validate_terminal([])

    path = tmp_path / "truncated.jsonl"
    journal = ReceiptJournal(path, run_id="run-truncated")
    journal.append(
        "run_started",
        {"profile": "public-stdio", "run_manifest_sha": RUN_MANIFEST_SHA},
    )

    with pytest.raises(JournalError, match="run_finished"):
        _validate_terminal(verify_journal(path))


def test_terminal_journal_requires_every_lifecycle_event_in_order(tmp_path: Path) -> None:
    path = tmp_path / "reordered.jsonl"
    journal = ReceiptJournal(path, run_id="run-reordered")
    journal.append(
        "run_started",
        {"profile": "public-stdio", "run_manifest_sha": RUN_MANIFEST_SHA},
    )
    for event_type in (LIFECYCLE[1], LIFECYCLE[0], *LIFECYCLE[2:]):
        payload: dict[str, object] = {"contract_id": SCENARIO}
        digest_field = EVENT_DIGEST_FIELDS.get(event_type)
        if digest_field is not None:
            payload[digest_field] = "a" * 64
        if event_type == "cleanup_observed":
            payload["clean"] = True
        if event_type == "scenario_finished":
            payload["verdict"] = "pass"
        journal.append(event_type, payload)
    journal.append("run_finished", _terminal_payload())

    with pytest.raises(JournalError, match="reordered"):
        _validate_terminal(verify_journal(path))


def test_terminal_journal_rejects_events_after_terminal(tmp_path: Path) -> None:
    path = tmp_path / "post-terminal.jsonl"
    journal = ReceiptJournal(path, run_id="run-post-terminal")
    journal.append(
        "run_started",
        {"profile": "public-stdio", "run_manifest_sha": RUN_MANIFEST_SHA},
    )
    journal.append("run_finished", _terminal_payload())
    journal.append("run_finished", _terminal_payload())

    with pytest.raises(JournalError, match="after a terminal"):
        _validate_terminal(verify_journal(path))


def test_terminal_journal_requires_worker_leases_before_scenario_work(tmp_path: Path) -> None:
    path = tmp_path / "late-worker.jsonl"
    journal = ReceiptJournal(path, run_id="run-late-worker")
    journal.append(
        "run_started",
        {"profile": "public-stdio", "run_manifest_sha": RUN_MANIFEST_SHA},
    )
    for event_type in LIFECYCLE:
        payload: dict[str, object] = {"contract_id": SCENARIO}
        digest_field = EVENT_DIGEST_FIELDS.get(event_type)
        if digest_field is not None:
            payload[digest_field] = "a" * 64
        if event_type == "cleanup_observed":
            payload["clean"] = True
        if event_type == "scenario_finished":
            payload["verdict"] = "pass"
        journal.append(event_type, payload)
    journal.append(
        "worker_leased",
        {"role": "worker-a", "worker_id": "worker-epoch-1", "lease_hash": "b" * 64},
    )
    journal.append("run_finished", _terminal_payload())

    with pytest.raises(JournalError, match="precede scenario"):
        validate_terminal_journal(
            verify_journal(path),
            (SCENARIO,),
            expected_profile="public-stdio",
            expected_run_manifest_sha=RUN_MANIFEST_SHA,
            expected_worker_roles=("worker-a",),
        )
