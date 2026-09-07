"""N3 T3/T4: evidence receipt schema for the A/B live dual-worker reload
identity harness. Required fields, per-slice content requirements, and the
build/validate/write helpers scripts/run_ab_reload_identity.py wires to live
phase results. See Plans/N3-T3-T4-live-reload-identity.md.
"""

import json
import os
import uuid
from datetime import UTC, datetime
from pathlib import Path  # noqa: TC003

from gauntlet.ab_reload_owner_safety import ABReloadIdentityError

REQUIRED_RECEIPT_FIELDS = (
    "source_sha",
    "unity_version",
    "utf_version",
    "worker_a",
    "worker_b",
    "t3_identity_reload",
    "t3_cross_identity",
    "t3_lost_ack",
    "t3_negative_controls",
    "t4_compile_error",
    "t4_sentinel",
    "cleanup",
)
_MISSING = object()

# Content check (not just presence): every slice field must be a non-empty
# dict carrying its actual evidence keys, so a never-run slice's {}
# placeholder can never be mistaken for a completed receipt. Keys mirror
# exactly what run_t3()/run_lost_ack()/run_t4() return (gauntlet.*).
_SLICE_REQUIRED_KEYS: dict[str, tuple[str, ...]] = {
    "t3_identity_reload": (
        "old_nonce", "new_nonce", "mvid_before", "mvid_after", "port_before", "port_after",
        "sync_verdict", "sentinel_nonce", "sentinel_nonce_before", "sentinel_nonce_after",
        "sentinel_mvid_before", "sentinel_mvid_after",
    ),
    "t3_cross_identity": ("reject_foreign", "accept_correct", "counter_before", "counter_after"),
    "t3_lost_ack": ("counter_before", "counter_after", "forwarded", "uncertain_delivery_raised"),
    "t3_negative_controls": ("b_mutated_red", "old_code_not_new"),
    "t4_compile_error": (
        "old_nonce", "injected_nonce", "mvid_before", "break_verdict", "nonce_during_break",
        "mvid_during_break", "repaired_nonce", "repair_verdict", "nonce_after_repair", "mvid_after_repair",
    ),
    "t4_sentinel": (
        "nonce_b_before", "nonce_b_during_break", "nonce_b_after_repair",
        "mvid_b_before", "mvid_b_during_break", "mvid_b_after_repair",
    ),
}


def _atomic_write_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def build_receipt(**fields: object) -> dict[str, object]:
    missing = [name for name in REQUIRED_RECEIPT_FIELDS if fields.get(name, _MISSING) is _MISSING]
    if missing:
        raise ABReloadIdentityError(f"Receipt missing required field(s): {', '.join(missing)}")
    receipt: dict[str, object] = {"schema_version": 1}
    receipt.update({name: fields[name] for name in REQUIRED_RECEIPT_FIELDS})
    receipt["timestamp"] = datetime.now(UTC).isoformat()
    return receipt


def validate_receipt(receipt: dict[str, object]) -> None:
    """Fail-closed content check for a full (T3+T4) receipt: every slice
    field must actually be a non-empty dict carrying its required evidence
    keys. build_receipt() only proves the field exists; a never-run slice's
    {} placeholder passes that check but must not pass this one."""
    for field, required_keys in _SLICE_REQUIRED_KEYS.items():
        value = receipt.get(field, _MISSING)
        if value is _MISSING:
            raise ABReloadIdentityError(f"Receipt missing required field(s): {field}")
        if not isinstance(value, dict) or not value:
            raise ABReloadIdentityError(f"Receipt field {field!r} must be a non-empty dict, got {value!r}")
        missing_keys = [key for key in required_keys if key not in value]
        if missing_keys:
            raise ABReloadIdentityError(
                f"Receipt field {field!r} missing required key(s): {', '.join(missing_keys)}"
            )


def write_receipt(path: Path, receipt: dict[str, object]) -> None:
    _atomic_write_json(path, receipt)
