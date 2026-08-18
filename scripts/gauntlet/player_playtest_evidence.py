"""Evidence producer for built-player PlayTest CI receipts."""


import hashlib
import re
import stat
import xml.etree.ElementTree as ET
from typing import TYPE_CHECKING

from gauntlet.artifact_contracts import ArtifactError, read_stable_artifact
from gauntlet.json_io import JsonFileError, parse_json_object
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from collections.abc import Iterable
    from pathlib import Path

_SHA_PATTERN = re.compile(r"^[0-9a-f]+$")
_MAX_RECEIPT_BYTES = 16 * 1024 * 1024
_MAX_PLAYER_BYTES = 2 * 1024 * 1024 * 1024
_SUCCESS_STEPS = 14
_GRAPHICS_STEPS = 4
_EXPECTED_FAILURE_STEPS = (
    "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
    "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
)


class PlayerPlaytestEvidenceError(ValueError):
    """Raised when player PlayTest evidence cannot be proven."""


def build_player_playtest_evidence(
    *,
    head_sha: str,
    run_id: str,
    run_attempt: str,
    runner_os: str,
    matrix_name: str,
    player_path: Path,
    artifacts_dir: Path,
) -> dict[str, object]:
    """Build one self-contained evidence object from already-produced receipts."""

    _validate_head_sha(head_sha)
    github = {
        "run_id": _require_text(run_id, "run ID"),
        "run_attempt": _require_text(run_attempt, "run attempt"),
        "runner_os": _require_text(runner_os, "runner OS"),
        "matrix_name": _require_text(matrix_name, "matrix name"),
    }
    player = _fingerprint_player_path(player_path)
    receipts = {
        "build_log": _fingerprint_required_file(artifacts_dir / "player-build.log"),
        "success": _validate_receipt_pair(
            artifacts_dir,
            "player-playtest",
            expected_steps=_SUCCESS_STEPS,
            expected_failed_steps=(),
            label="success",
        ),
        "expected_failure": _validate_receipt_pair(
            artifacts_dir,
            "player-playtest-failure",
            expected_steps=None,
            expected_failed_steps=_EXPECTED_FAILURE_STEPS,
            label="expected-failure",
        ),
        "graphics": _optional_graphics_receipt(artifacts_dir),
    }
    payload: dict[str, object] = {
        "schema_version": 1,
        "head_sha": head_sha.lower(),
        "github": github,
        "player": player,
        "receipts": receipts,
    }
    payload["evidence_sha256"] = content_hash(payload)
    return payload


def _optional_graphics_receipt(artifacts_dir: Path) -> dict[str, object] | None:
    json_path = artifacts_dir / "player-playtest-graphics.json"
    junit_path = artifacts_dir / "player-playtest-graphics.xml"
    if not json_path.exists() and not junit_path.exists():
        return None
    return _validate_receipt_pair(
        artifacts_dir,
        "player-playtest-graphics",
        expected_steps=_GRAPHICS_STEPS,
        expected_failed_steps=(),
        label="graphics",
    )


def _validate_receipt_pair(
    artifacts_dir: Path,
    stem: str,
    *,
    expected_steps: int | None,
    expected_failed_steps: Iterable[str],
    label: str,
) -> dict[str, object]:
    json_path = artifacts_dir / f"{stem}.json"
    junit_path = artifacts_dir / f"{stem}.xml"
    json_bytes, json_sha = _read_required_receipt(json_path)
    junit_bytes, junit_sha = _read_required_receipt(junit_path)
    payload = _parse_json_receipt(json_bytes, json_path.name)
    raw_steps, failed_steps = _receipt_steps(payload, label)
    expected_failed = tuple(expected_failed_steps)
    if expected_steps is not None and len(raw_steps) != expected_steps:
        raise PlayerPlaytestEvidenceError(f"{label} PlayTest step count mismatch")
    if tuple(failed_steps) != expected_failed:
        raise PlayerPlaytestEvidenceError(f"{label} PlayTest failed-step contract mismatch")
    if payload["failed"] != len(failed_steps) or payload["passed"] != len(raw_steps) - len(failed_steps):
        raise PlayerPlaytestEvidenceError(f"{label} PlayTest pass/fail counters mismatch")
    _validate_junit(junit_bytes, raw_steps, failed_steps)
    return {
        "json_path": json_path.name,
        "json_sha256": json_sha,
        "junit_path": junit_path.name,
        "junit_sha256": junit_sha,
        "passed": payload["passed"],
        "failed": payload["failed"],
        "steps": len(raw_steps),
        "failed_steps": failed_steps,
    }


def _parse_json_receipt(payload: bytes, source: str) -> dict[str, object]:
    if payload.startswith(b"\xef\xbb\xbf"):
        raise PlayerPlaytestEvidenceError(f"{source}: JSON receipt must be UTF-8 without BOM")
    try:
        data = parse_json_object(payload, source=source)
    except JsonFileError as exc:
        raise PlayerPlaytestEvidenceError(str(exc)) from exc
    if data.get("schema_version") != 1:
        raise PlayerPlaytestEvidenceError(f"{source}: unsupported receipt schema")
    return data


def _receipt_steps(payload: dict[str, object], label: str) -> tuple[list[str], list[str]]:
    steps = payload.get("steps")
    if not isinstance(steps, list) or not steps:
        raise PlayerPlaytestEvidenceError(f"{label} PlayTest emitted no steps")
    raw_steps: list[str] = []
    failed_steps: list[str] = []
    for step in steps:
        if not isinstance(step, dict):
            raise PlayerPlaytestEvidenceError(f"{label} PlayTest step is not an object")
        raw = step.get("raw")
        passed = step.get("passed")
        if not isinstance(raw, str) or not raw:
            raise PlayerPlaytestEvidenceError(f"{label} PlayTest step raw text is missing")
        if not isinstance(passed, bool):
            raise PlayerPlaytestEvidenceError(f"{label} PlayTest step outcome is missing")
        raw_steps.append(raw)
        if not passed:
            failed_steps.append(raw)
    passed_count = payload.get("passed")
    failed_count = payload.get("failed")
    if type(passed_count) is not int or type(failed_count) is not int:
        raise PlayerPlaytestEvidenceError(f"{label} PlayTest counters must be integers")
    return raw_steps, failed_steps


def _validate_junit(payload: bytes, raw_steps: list[str], failed_steps: list[str]) -> None:
    try:
        root = ET.fromstring(payload)
    except ET.ParseError as exc:
        raise PlayerPlaytestEvidenceError("player JUnit is not valid XML") from exc
    if _local_name(root.tag) != "testsuite" or root.get("name") != "UnityMCP.PlayerPlaytest":
        raise PlayerPlaytestEvidenceError("player JUnit suite identity mismatch")
    cases = [child for child in root if _local_name(child.tag) == "testcase"]
    if _count(root.get("tests"), "player JUnit tests") != len(raw_steps) or len(cases) != len(raw_steps):
        raise PlayerPlaytestEvidenceError("player JUnit test count mismatch")
    if _count(root.get("failures"), "player JUnit failures") != len(failed_steps):
        raise PlayerPlaytestEvidenceError("player JUnit failure count mismatch")
    junit_steps = [case.get("name") for case in cases]
    if junit_steps != raw_steps:
        raise PlayerPlaytestEvidenceError("player JUnit step order does not match JSON")
    junit_failed = [case.get("name") for case in cases if any(_local_name(child.tag) == "failure" for child in case)]
    if junit_failed != failed_steps:
        raise PlayerPlaytestEvidenceError("player JUnit failed steps do not match JSON")


def _fingerprint_player_path(path: Path) -> dict[str, object]:
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise PlayerPlaytestEvidenceError(f"player path is not accessible: {path}") from exc
    if stat.S_ISREG(metadata.st_mode):
        size, digest = _hash_stable_file(path, max_bytes=_MAX_PLAYER_BYTES)
        return {"path": str(path), "kind": "file", "sha256": digest, "file_count": 1, "size_bytes": size}
    if stat.S_ISDIR(metadata.st_mode):
        return _fingerprint_directory(path)
    raise PlayerPlaytestEvidenceError(f"player path is not a file or directory: {path}")


def _fingerprint_directory(root: Path) -> dict[str, object]:
    records = []
    total_size = 0
    for child in sorted(root.rglob("*")):
        metadata = child.lstat()
        if stat.S_ISDIR(metadata.st_mode):
            continue
        if not stat.S_ISREG(metadata.st_mode):
            raise PlayerPlaytestEvidenceError(f"player tree contains a non-regular file: {child}")
        size, digest = _hash_stable_file(child, max_bytes=_MAX_PLAYER_BYTES)
        total_size += size
        records.append({"path": child.relative_to(root).as_posix(), "size_bytes": size, "sha256": digest})
    if not records:
        raise PlayerPlaytestEvidenceError("player directory contains no files")
    return {
        "path": str(root),
        "kind": "directory",
        "sha256": content_hash({"files": records}),
        "file_count": len(records),
        "size_bytes": total_size,
    }


def _fingerprint_required_file(path: Path) -> dict[str, object]:
    size, digest = _hash_stable_file(path, max_bytes=_MAX_RECEIPT_BYTES)
    return {"path": path.name, "sha256": digest, "size_bytes": size}


def _read_required_receipt(path: Path) -> tuple[bytes, str]:
    payload = _read_stable_bytes(path, max_bytes=_MAX_RECEIPT_BYTES)
    return payload, hashlib.sha256(payload).hexdigest()


def _hash_stable_file(path: Path, *, max_bytes: int) -> tuple[int, str]:
    payload = _read_stable_bytes(path, max_bytes=max_bytes)
    return len(payload), hashlib.sha256(payload).hexdigest()


def _read_stable_bytes(path: Path, *, max_bytes: int) -> bytes:
    try:
        return read_stable_artifact(path, max_bytes)
    except ArtifactError as exc:
        raise PlayerPlaytestEvidenceError(str(exc).replace("artifact", "receipt file", 1)) from exc


def _validate_head_sha(value: str) -> None:
    if len(value) not in {40, 64} or not _SHA_PATTERN.fullmatch(value.lower()):
        raise PlayerPlaytestEvidenceError("head SHA must contain 40 or 64 hexadecimal characters")


def _require_text(value: str, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise PlayerPlaytestEvidenceError(f"{label} must be a non-empty string")
    return value


def _count(value: str | None, label: str) -> int:
    if value is None or not value.isdigit():
        raise PlayerPlaytestEvidenceError(f"{label} must be a non-negative integer")
    return int(value)


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]
