"""Semantic and terminal validation for hash-chained Gauntlet journals."""


from dataclasses import dataclass
from datetime import UTC, datetime
from typing import Any

from gauntlet.receipts import JournalError, content_hash

_SCENARIO_EVENT_FIELDS = {
    "identity_verified": {"contract_id", "identity_hash"},
    "scenario_started": {"contract_id", "precondition_hash"},
    "intent_recorded": {"contract_id", "intent_hash"},
    "request_transmitted": {"contract_id", "request_hash"},
    "action_observed": {"contract_id", "response_hash"},
    "post_state_observed": {"contract_id", "state_hash"},
    "cleanup_observed": {"contract_id", "clean", "cleanup_hash"},
    "scenario_finished": {"contract_id", "verdict"},
}


@dataclass(frozen=True, slots=True)
class JournalSummary:
    run_id: str
    profile: str
    finished_at: str
    worker_ids: tuple[str, ...]


def validate_terminal_journal(
    events: list[dict[str, Any]],
    expected_scenarios: tuple[str, ...],
    *,
    expected_profile: str,
    expected_run_manifest_sha: str,
    expected_worker_roles: tuple[str, ...],
) -> JournalSummary:
    """Require a complete PASS lifecycle for every expected scenario."""
    if not events:
        raise JournalError("journal is empty")
    if events[0].get("event_type") != "run_started":
        raise JournalError("journal must start with run_started")
    start_payload = _payload(events[0])
    expected_start = {
        "profile": expected_profile,
        "run_manifest_sha": expected_run_manifest_sha,
    }
    if start_payload != expected_start:
        raise JournalError("run_started identity does not match release inputs")
    _require_digest(start_payload["run_manifest_sha"], "run manifest")
    if events[-1].get("event_type") != "run_finished":
        raise JournalError("journal must end with run_finished")
    expected_terminal = {
        "verdict": "pass",
        "scenario_count": len(expected_scenarios),
        "scenario_manifest_sha": content_hash(sorted(expected_scenarios)),
    }
    if _payload(events[-1]) != expected_terminal:
        raise JournalError("run_finished summary does not match release inputs")
    if any(event.get("event_type") == "run_finished" for event in events[:-1]):
        raise JournalError("journal contains events after a terminal run event")

    _validate_timestamps(events)
    required_order = tuple(_SCENARIO_EVENT_FIELDS)
    observed: dict[str, list[str]] = {}
    workers: dict[str, str] = {}
    for event in events[1:-1]:
        event_type = event.get("event_type")
        if event_type == "worker_leased":
            if observed:
                raise JournalError("worker leases must precede scenario events")
            _record_worker(_payload(event), workers)
            continue
        if event_type not in required_order:
            raise JournalError(f"unsupported release journal event: {event_type}")
        payload = _payload(event)
        if set(payload) != _SCENARIO_EVENT_FIELDS[str(event_type)]:
            raise JournalError(f"{event_type} payload schema is invalid")
        scenario_id = payload.get("contract_id")
        if not isinstance(scenario_id, str) or not scenario_id:
            raise JournalError("scenario event is missing contract_id")
        observed.setdefault(scenario_id, []).append(str(event_type))
        digest_field = next((key for key in payload if key.endswith("_hash")), None)
        if digest_field is not None:
            _require_digest(payload[digest_field], str(event_type))
        if event_type == "cleanup_observed" and payload.get("clean") is not True:
            raise JournalError(f"scenario cleanup is not clean: {scenario_id}")
        if event_type == "scenario_finished" and payload.get("verdict") != "pass":
            raise JournalError(f"scenario did not finish with pass: {scenario_id}")

    if set(observed) != set(expected_scenarios):
        raise JournalError("journal scenario set does not match policy")
    if tuple(sorted(workers)) != tuple(sorted(expected_worker_roles)):
        raise JournalError("journal worker lease roles do not match policy")
    for scenario_id, event_types in observed.items():
        if tuple(event_types) != required_order:
            raise JournalError(f"scenario lifecycle is incomplete or reordered: {scenario_id}")
    return JournalSummary(
        run_id=str(events[0]["run_id"]),
        profile=expected_profile,
        finished_at=str(events[-1]["timestamp"]),
        worker_ids=tuple(sorted(workers.values())),
    )


def _record_worker(payload: dict[str, Any], workers: dict[str, str]) -> None:
    if set(payload) != {"role", "worker_id", "lease_hash"}:
        raise JournalError("worker_leased payload schema is invalid")
    role = payload.get("role")
    worker_id = payload.get("worker_id")
    if not isinstance(role, str) or not role or not isinstance(worker_id, str) or not worker_id:
        raise JournalError("worker lease identity is invalid")
    _require_digest(payload.get("lease_hash"), "worker lease")
    if role in workers:
        raise JournalError(f"duplicate worker lease role: {role}")
    workers[role] = worker_id


def _payload(event: dict[str, Any]) -> dict[str, Any]:
    payload = event.get("payload")
    if not isinstance(payload, dict):
        raise JournalError("journal event payload must be an object")
    return payload


def _validate_timestamps(events: list[dict[str, Any]]) -> None:
    previous: datetime | None = None
    for event in events:
        value = event.get("timestamp")
        if not isinstance(value, str):
            raise JournalError("journal timestamp must be an RFC3339 string")
        try:
            parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        except ValueError as exc:
            raise JournalError("journal timestamp must be RFC3339") from exc
        if parsed.tzinfo is None:
            raise JournalError("journal timestamp must include a timezone")
        current = parsed.astimezone(UTC)
        if previous is not None and current < previous:
            raise JournalError("journal timestamps are not monotonic")
        previous = current


def _require_digest(value: object, label: str) -> None:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value.lower())
    ):
        raise JournalError(f"{label} digest is invalid")
