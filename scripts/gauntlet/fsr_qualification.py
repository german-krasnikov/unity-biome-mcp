"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: lock loading,
runtime receipts, and the headed (non-batchmode) Unity launch primitives.

Reuses `wait_for_port`, `terminate_workers`, `tail_log` and
`write_mcp_project_settings` from `gauntlet.hosted_conformance` — those are
launch-mode-agnostic and already proven on all three target OSes by the
`hosted-disposable-unity` conformance job. What differs here is the launch
itself: `build_headed_unity_command`/`build_headed_unity_environment` omit
`-batchmode`/`-nographics` and `UNITY_MCP_ENABLE_BATCHMODE`, because a
batchmode lane is explicitly not FSR qualification evidence (§7 P1-20) — the
plan requires "a direct GUI supervisor on every OS."

See Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §7 P1-20.
"""
import json
from collections.abc import Mapping, Sequence  # noqa: TC003
from pathlib import Path  # noqa: TC003

from gauntlet.hosted_conformance import tail_log

REQUIRED_LOCK_KEYS = ("base_product_sha", "final_fsr_adapter_sha", "cells")
REQUIRED_CELL_WINDOWS = ("u_min", "u_max")
REQUIRED_CELL_FIELDS = ("unity_version", "unity_revision", "utf_version")
VALID_OUTCOMES = frozenset({"PASS", "FAIL", "INFRASTRUCTURE_BLOCKED"})
GUARDED_BASE_PREFIXES = ("unity-plugin/", "server/src/")
EXPECTED_CELLS = (
    "min-macos-arm64",
    "min-windows-x64",
    "min-linux-x64",
    "max-macos-arm64",
    "max-windows-x64",
    "max-linux-x64",
)


class FsrQualificationError(RuntimeError):
    pass


def load_lock(path: Path) -> dict[str, object]:
    """Load and schema-validate the tracked
    scripts/fsr_qualification_lock.json (or an equivalent test fixture)."""
    if not path.is_file():
        raise FsrQualificationError(f"FSR qualification lock not found: {path}")
    payload = json.loads(path.read_text(encoding="utf-8"))
    missing = [key for key in REQUIRED_LOCK_KEYS if key not in payload]
    if missing:
        raise FsrQualificationError(
            f"FSR qualification lock {path} missing key(s): {', '.join(missing)}"
        )
    cells = payload["cells"]
    if not isinstance(cells, dict):
        raise FsrQualificationError(f"FSR qualification lock {path}: 'cells' is not an object")
    for window in REQUIRED_CELL_WINDOWS:
        if window not in cells:
            raise FsrQualificationError(
                f"FSR qualification lock {path} missing cell window: {window}"
            )
        cell = cells[window]
        missing_fields = [field for field in REQUIRED_CELL_FIELDS if field not in cell]
        if missing_fields:
            raise FsrQualificationError(
                f"FSR qualification lock {path} cell {window} missing "
                f"field(s): {', '.join(missing_fields)}"
            )
    return payload


def resolve_cell(lock: Mapping[str, object], window: str) -> dict[str, str]:
    """Return the frozen unity_version/unity_revision/utf_version for one
    matrix window ('u_min' or 'u_max')."""
    cells = lock["cells"]
    if window not in cells:
        raise FsrQualificationError(
            f"Unknown FSR qualification window: {window!r} (expected one of "
            f"{REQUIRED_CELL_WINDOWS})"
        )
    return dict(cells[window])


def build_runtime_receipt(
    *,
    cell: str,
    os_name: str,
    arch: str,
    unity_version: str,
    unity_revision: str,
    setup_ok: bool,
    license_ok: bool,
    display_ok: bool,
    checkout_sha: str,
    lock_base_product_sha: str,
    candidate_sha: str,
    outcome: str,
    error: str | None = None,
) -> dict[str, object]:
    """Assemble one cell's terminal evidence receipt. `outcome` must be one
    of PASS/FAIL/INFRASTRUCTURE_BLOCKED — a missing secret or setup/license
    failure is always a failed INFRASTRUCTURE_BLOCKED cell, never a green
    skip (§7 P1-20). `error`, when given, is the real Python exception
    message — Run 3 reached real semantic failures with no error recorded
    anywhere but the GH Actions job log, which is only retrievable once the
    whole workflow run has completed."""
    if outcome not in VALID_OUTCOMES:
        raise FsrQualificationError(
            f"Invalid receipt outcome {outcome!r}; expected one of {sorted(VALID_OUTCOMES)}"
        )
    receipt: dict[str, object] = {
        "cell": cell,
        "os": os_name,
        "arch": arch,
        "unity_version": unity_version,
        "unity_revision": unity_revision,
        "setup_ok": setup_ok,
        "license_ok": license_ok,
        "display_ok": display_ok,
        "checkout_sha": checkout_sha,
        "lock_base_product_sha": lock_base_product_sha,
        "candidate_sha": candidate_sha,
        "outcome": outcome,
    }
    if error:
        receipt["error"] = error
    return receipt


def assert_base_sha_untouched(
    changed_paths: Sequence[str], *, guarded_prefixes: Sequence[str] = GUARDED_BASE_PREFIXES
) -> None:
    """Aggregate-job guard: the workflow's own CI commits, staged on top of
    the frozen `lock.base_product_sha`, must never touch product code —
    only CI/lock/script infrastructure. Takes a plain path list so it stays
    hermetically testable; the real `git diff --name-only <base>..HEAD` call
    is a thin, untested wrapper around this."""
    violations = [
        path
        for path in changed_paths
        if any(path.startswith(prefix) for prefix in guarded_prefixes)
    ]
    if violations:
        raise FsrQualificationError(
            "FSR qualification commits on top of base_product_sha touched "
            f"guarded product path(s): {', '.join(violations)}"
        )


def validate_receipt_set(
    receipts: Sequence[Mapping[str, object]], lock: Mapping[str, object]
) -> None:
    """Aggregate-job guard: DoD requires exactly 6/6 unique PASS receipts
    bound to one SHA set (§7 P1-20). Any missing cell, duplicate, non-PASS
    outcome, or SHA drift fails the aggregate — this never averages or
    partially-passes the matrix."""
    cells_seen = [receipt.get("cell") for receipt in receipts]
    if sorted(cells_seen) != sorted(EXPECTED_CELLS):
        raise FsrQualificationError(
            f"Expected exactly the 6 cells {sorted(EXPECTED_CELLS)}, got {sorted(cells_seen)}"
        )
    if len(set(cells_seen)) != len(cells_seen):
        raise FsrQualificationError(f"Duplicate cell receipts: {cells_seen}")

    for receipt in receipts:
        if receipt.get("outcome") != "PASS":
            raise FsrQualificationError(
                f"Cell {receipt.get('cell')} is not PASS: {receipt.get('outcome')}"
            )
        if receipt.get("lock_base_product_sha") != lock["base_product_sha"]:
            raise FsrQualificationError(
                f"Cell {receipt.get('cell')} lock_base_product_sha does not match the lock"
            )
        if receipt.get("candidate_sha") != lock["final_fsr_adapter_sha"]:
            raise FsrQualificationError(
                f"Cell {receipt.get('cell')} candidate_sha does not match the lock"
            )

    checkout_shas = {receipt.get("checkout_sha") for receipt in receipts}
    if len(checkout_shas) != 1:
        raise FsrQualificationError(
            f"Cells ran against different checkout SHAs: {sorted(checkout_shas)}"
        )


def write_pilot_evidence(
    evidence_out: Path,
    *,
    cell: str,
    os_name: str,
    arch: str,
    log_path: Path,
    outcome: str,
    error: str | None,
) -> None:
    """The fixture-free GUI pilot previously discarded all diagnostic
    evidence on failure — a failed pilot cell uploaded nothing but a bare
    receipt.json with no log content at all. Always write a receipt +
    whatever Unity log tail exists (or an explicit "not found" record) so a
    future failure is diagnosable from the uploaded artifact alone."""
    if outcome not in VALID_OUTCOMES:
        raise FsrQualificationError(
            f"Invalid pilot evidence outcome {outcome!r}; expected one of {sorted(VALID_OUTCOMES)}"
        )
    evidence_out.mkdir(parents=True, exist_ok=True)
    receipt: dict[str, object] = {
        "cell": cell,
        "os": os_name,
        "arch": arch,
        "outcome": outcome,
    }
    if error:
        receipt["error"] = error
    (evidence_out / "pilot-receipt.json").write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    (evidence_out / "pilot-unity-log-tail.txt").write_text(tail_log(log_path), encoding="utf-8")


def build_headed_unity_command(
    unity: Path, project: Path, log: Path, *, force_d3d11: bool = False
) -> list[str]:
    """No -batchmode/-nographics/-quit: a direct GUI supervisor, not a
    batchmode lane (§7 P1-20).

    force_d3d11 is a Windows-only diagnostic: min-windows-x64's Editor.log
    never reached even the earliest engine banner line across 3 consecutive
    matrix runs, unlike every Linux/macOS cell, which always logs within
    the first second. GPU-less Windows CI VMs are a documented source of
    Unity Editor startup hangs during graphics-backend auto-detection;
    -force-d3d11 is a standard Unity Editor argument that skips it. This is
    an evidence-motivated experiment, not a confirmed fix."""
    command = [
        str(unity),
        "-projectPath",
        str(project.resolve()),
        "-logFile",
        str(log.resolve()),
    ]
    if force_d3d11:
        command.append("-force-d3d11")
    return command


def build_headed_unity_environment(
    base: Mapping[str, str], *, port: int, project: Path
) -> dict[str, str]:
    """No UNITY_MCP_ENABLE_BATCHMODE — this worker boots headed, driven by a
    real TCP client, not the batchmode conformance harness."""
    env = dict(base)
    env["UNITY_MCP_PORT"] = str(port)
    env["UNITY_MCP_PROJECT_PATH"] = str(project.resolve())
    return env


__all__ = [
    "FsrQualificationError",
    "GUARDED_BASE_PREFIXES",
    "VALID_OUTCOMES",
    "EXPECTED_CELLS",
    "load_lock",
    "resolve_cell",
    "build_runtime_receipt",
    "write_pilot_evidence",
    "assert_base_sha_untouched",
    "validate_receipt_set",
    "build_headed_unity_command",
    "build_headed_unity_environment",
]
