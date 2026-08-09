"""Fail-closed validation for built-player PlayTest CI evidence."""

from __future__ import annotations

from typing import TYPE_CHECKING

from gauntlet.json_io import JsonFileError, load_json_object
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from collections.abc import Sequence
    from pathlib import Path

REQUIRED_MATRICES = ("Linux", "macOS", "Windows")
_EXPECTED_FAILURE_STEPS = (
    "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
    "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
)
_DIGEST_SIZE = 64


class PlayerPlaytestGateError(ValueError):
    """Raised when player PlayTest release evidence is missing or contradictory."""


def validate_player_playtest_evidence_set(
    paths: Sequence[Path],
    *,
    expected_head_sha: str,
    required_matrices: Sequence[str] = REQUIRED_MATRICES,
) -> tuple[str, ...]:
    if not paths:
        raise PlayerPlaytestGateError("Player PlayTest evidence is required")
    required = tuple(required_matrices)
    by_matrix: dict[str, dict[str, object]] = {}
    for path in paths:
        evidence = _load_evidence(path)
        matrix = _validate_common(evidence, expected_head_sha=expected_head_sha)
        if matrix in by_matrix:
            raise PlayerPlaytestGateError(f"duplicate Player PlayTest matrix evidence: {matrix}")
        by_matrix[matrix] = evidence
    if tuple(sorted(by_matrix)) != tuple(sorted(required)):
        raise PlayerPlaytestGateError("Player PlayTest matrix set does not match release policy")
    return tuple(sorted(by_matrix))


def _load_evidence(path: Path) -> dict[str, object]:
    try:
        evidence = load_json_object(path)
    except JsonFileError as exc:
        raise PlayerPlaytestGateError(str(exc)) from exc
    if evidence.get("schema_version") != 1:
        raise PlayerPlaytestGateError("Player PlayTest evidence schema mismatch")
    supplied_hash = evidence.get("evidence_sha256")
    unhashed = dict(evidence)
    unhashed.pop("evidence_sha256", None)
    if supplied_hash != content_hash(unhashed):
        raise PlayerPlaytestGateError("Player PlayTest evidence hash mismatch")
    return evidence


def _validate_common(evidence: dict[str, object], *, expected_head_sha: str) -> str:
    if evidence.get("head_sha") != expected_head_sha.lower():
        raise PlayerPlaytestGateError("Player PlayTest head SHA mismatch")
    github = _require_object(evidence.get("github"), "Player PlayTest github identity")
    matrix = _require_text(github.get("matrix_name"), "Player PlayTest matrix")
    if github.get("runner_os") != matrix:
        raise PlayerPlaytestGateError("Player PlayTest runner OS does not match matrix")
    for key in ("run_id", "run_attempt"):
        _require_text(github.get(key), f"Player PlayTest {key}")
    _validate_player_fingerprint(_require_object(evidence.get("player"), "Player PlayTest player"))
    receipts = _require_object(evidence.get("receipts"), "Player PlayTest receipts")
    _validate_file_digest(_require_object(receipts.get("build_log"), "Player build log"))
    _validate_result_receipt(
        _require_object(receipts.get("success"), "Player PlayTest success receipt"),
        label="success",
        expected_steps=14,
        expected_failed=(),
    )
    _validate_result_receipt(
        _require_object(receipts.get("expected_failure"), "Player PlayTest failure receipt"),
        label="expected-failure",
        expected_steps=5,
        expected_failed=_EXPECTED_FAILURE_STEPS,
    )
    graphics = receipts.get("graphics")
    if graphics is not None:
        _validate_result_receipt(
            _require_object(graphics, "Player PlayTest graphics receipt"),
            label="graphics",
            expected_steps=4,
            expected_failed=(),
        )
    return matrix


def _validate_player_fingerprint(value: dict[str, object]) -> None:
    if _require_text(value.get("kind"), "Player artifact kind") not in {"file", "directory"}:
        raise PlayerPlaytestGateError("Player artifact kind is unsupported")
    _require_text(value.get("path"), "Player artifact path")
    _require_digest(value.get("sha256"), "Player artifact digest")
    _require_positive_int(value.get("file_count"), "Player artifact file count")
    _require_positive_int(value.get("size_bytes"), "Player artifact size")


def _validate_result_receipt(
    value: dict[str, object],
    *,
    label: str,
    expected_steps: int,
    expected_failed: tuple[str, ...],
) -> None:
    steps = _require_non_negative_int(value.get("steps"), f"{label} PlayTest steps")
    failed = _require_non_negative_int(value.get("failed"), f"{label} PlayTest failures")
    passed = _require_non_negative_int(value.get("passed"), f"{label} PlayTest passes")
    if steps != expected_steps:
        raise PlayerPlaytestGateError(f"{label} PlayTest step count mismatch")
    if passed + failed != steps:
        raise PlayerPlaytestGateError(f"{label} PlayTest pass/fail counters mismatch")
    failed_steps = value.get("failed_steps")
    if not isinstance(failed_steps, list) or tuple(failed_steps) != expected_failed:
        raise PlayerPlaytestGateError(f"{label} PlayTest failed-step contract mismatch")
    _validate_file_digest(value, f"{label} PlayTest")


def _validate_file_digest(value: dict[str, object], label: str = "receipt") -> None:
    _require_text(value.get("json_path", value.get("path")), f"{label} path")
    for key in ("json_sha256", "junit_sha256"):
        if key in value:
            _require_digest(value.get(key), f"{label} {key}")
    if "sha256" in value:
        _require_digest(value.get("sha256"), f"{label} digest")
    if "size_bytes" in value:
        _require_non_negative_int(value.get("size_bytes"), f"{label} size")


def _require_object(value: object, label: str) -> dict[str, object]:
    if not isinstance(value, dict):
        raise PlayerPlaytestGateError(f"{label} must be an object")
    return value


def _require_text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise PlayerPlaytestGateError(f"{label} must be a non-empty string")
    return value


def _require_digest(value: object, label: str) -> None:
    if not isinstance(value, str) or len(value) != _DIGEST_SIZE:
        raise PlayerPlaytestGateError(f"{label} must be a SHA-256 digest")
    if any(character not in "0123456789abcdef" for character in value.lower()):
        raise PlayerPlaytestGateError(f"{label} must be a SHA-256 digest")


def _require_positive_int(value: object, label: str) -> int:
    parsed = _require_non_negative_int(value, label)
    if parsed == 0:
        raise PlayerPlaytestGateError(f"{label} must be positive")
    return parsed


def _require_non_negative_int(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise PlayerPlaytestGateError(f"{label} must be a non-negative integer")
    return value
