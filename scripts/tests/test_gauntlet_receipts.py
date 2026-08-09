from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.receipts import JournalError, ReceiptJournal, verify_journal  # noqa: E402


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
    journal.append("run_started", {"profile": "public-stdio"})
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
