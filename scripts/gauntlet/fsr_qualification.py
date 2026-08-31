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
import hashlib
import json
import socket
import subprocess
import time
from collections.abc import Mapping, Sequence  # noqa: TC003
from datetime import UTC, datetime
from pathlib import Path  # noqa: TC003

from gauntlet.hosted_conformance import HostedConformanceError, tail_log

REQUIRED_LOCK_KEYS = ("base_product_sha", "final_fsr_adapter_sha", "cells")
REQUIRED_CELL_WINDOWS = ("u_min", "u_max")
REQUIRED_CELL_FIELDS = ("unity_version", "unity_revision", "utf_version")
VALID_OUTCOMES = frozenset({"PASS", "FAIL", "INFRASTRUCTURE_BLOCKED"})
GUARDED_BASE_PREFIXES = ("unity-plugin/", "server/src/")
# Narrowed after run 5 (coordinator decision, Plans/HotReload/V2/FSR-MVP-CLEAN
# /04-PARETO-COMPLETION-HANDOFF.md §1.1): u_max shelved to P2-07. The
# qualifying window is u_min on macOS/Linux only; Windows is kept as a
# documented, non-blocking INFRASTRUCTURE_BLOCKED limitation — present with
# an honest receipt is required, PASS is not (never a green skip: an
# absent or invalid-outcome Windows receipt still fails the aggregate).
REQUIRED_PASS_CELLS = ("min-macos-arm64", "min-linux-x64")
DOCUMENTED_BLOCKED_CELLS = ("min-windows-x64",)
EXPECTED_CELLS = REQUIRED_PASS_CELLS + DOCUMENTED_BLOCKED_CELLS


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
    preseed: dict[str, object] | None = None,
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
    if preseed is not None:
        receipt["preseed"] = preseed
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
    """Aggregate-job guard, narrowed after run 5: exactly 2/2 unique PASS
    receipts for REQUIRED_PASS_CELLS, bound to one SHA set, plus exactly
    one present, honestly-labeled receipt for DOCUMENTED_BLOCKED_CELLS
    (Windows) — its outcome does not have to be PASS, but it must exist
    and carry a real VALID_OUTCOMES value. A missing or duplicate cell
    (either kind), a non-PASS required cell, an invalid-outcome blocked
    cell, or SHA drift on a required cell fails the aggregate — this never
    averages or partially-passes the matrix, and Windows is never a green
    skip by simply being absent."""
    cells_seen = [receipt.get("cell") for receipt in receipts]
    if sorted(cells_seen) != sorted(EXPECTED_CELLS):
        raise FsrQualificationError(
            f"Expected exactly the cells {sorted(EXPECTED_CELLS)}, got {sorted(cells_seen)}"
        )
    if len(set(cells_seen)) != len(cells_seen):
        raise FsrQualificationError(f"Duplicate cell receipts: {cells_seen}")

    by_cell = {receipt.get("cell"): receipt for receipt in receipts}

    for cell in REQUIRED_PASS_CELLS:
        receipt = by_cell[cell]
        if receipt.get("outcome") != "PASS":
            raise FsrQualificationError(f"Required cell {cell} is not PASS: {receipt.get('outcome')}")
        if receipt.get("lock_base_product_sha") != lock["base_product_sha"]:
            raise FsrQualificationError(f"Cell {cell} lock_base_product_sha does not match the lock")
        if receipt.get("candidate_sha") != lock["final_fsr_adapter_sha"]:
            raise FsrQualificationError(f"Cell {cell} candidate_sha does not match the lock")

    for cell in DOCUMENTED_BLOCKED_CELLS:
        receipt = by_cell[cell]
        outcome = receipt.get("outcome")
        if outcome not in VALID_OUTCOMES:
            raise FsrQualificationError(
                f"Documented-blocked cell {cell} has no honest outcome: {outcome!r}"
            )

    checkout_shas = {by_cell[cell].get("checkout_sha") for cell in REQUIRED_PASS_CELLS}
    if len(checkout_shas) != 1:
        raise FsrQualificationError(
            f"Required-pass cells ran against different checkout SHAs: {sorted(checkout_shas)}"
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
    preseed: dict[str, object] | None = None,
) -> None:
    """The fixture-free GUI pilot previously discarded all diagnostic
    evidence on failure — a failed pilot cell uploaded nothing but a bare
    receipt.json with no log content at all. Always write a receipt +
    whatever Unity log tail exists (or an explicit "not found" record) so a
    future failure is diagnosable from the uploaded artifact alone.

    preseed, when given, is the offline EditorPrefs preseed receipt
    (mechanism/keys/applied/error) — embedded so the evidence bundle is
    honest about what environment mitigations were actually in place."""
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
    if preseed is not None:
        receipt["preseed"] = preseed
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


def classify_dialog_evidence(log_text: str) -> str:
    """Returns "shown" (a DisplayDialogComplex stack frame is present — the
    dialog definitely appeared) or "inconclusive" — never a false
    "suppressed". Run 6 correction (33390881487): the earlier
    detect_dialog_suppressed treated the ABSENCE of DisplayDialogComplex as
    proof of suppression, but a killed process's truncated log looks
    IDENTICAL whether the dialog blocked it or something else entirely
    did — anything that would have logged after a real block is equally
    absent either way. "FSR: asset auto refresh enabled..." also prints
    unconditionally, before FSR's own StopShowing gate
    (FastScriptReloadWelcomeScreen.cs L984 vs the DisplayDialogComplex call
    at L986), so its presence alone says nothing either. Positive proof of
    suppression requires independent evidence the phase actually
    completed (e.g. wait_for_port_diagnosed succeeding), not log-text
    alone."""
    if "DisplayDialogComplex" in log_text:
        return "shown"
    return "inconclusive"


def byte_diagnostic(data: bytes) -> dict[str, object]:
    """sha256 + size + first/last 32 hex bytes of one content buffer. Run 7:
    min-macos-arm64's classifier rejection could not be explained by the
    (fixed, disproven) BOM theory, nor by reproducing the exact intended
    content through the real classifier locally (ADMITTED cleanly, offline)
    — capturing this for the actual before (on disk) and after (about to
    be sent) content of every ON-mode write lets a live run's real bytes be
    compared against what is proven to work."""
    return {
        "sha256": hashlib.sha256(data).hexdigest(),
        "size": len(data),
        "first32_hex": data[:32].hex(),
        "last32_hex": data[-32:].hex(),
    }


def default_editor_log_path(*, os_name: str, home: Path) -> Path:
    """The OS-default Editor.log location Unity falls back to — checked in
    addition to our explicit -logFile path, in case that argument itself
    silently failed (Run 4: Windows produced zero content at the -logFile
    path across 4 consecutive matrix runs)."""
    if os_name == "Windows":
        return home / "AppData" / "Local" / "Unity" / "Editor" / "Editor.log"
    if os_name == "macOS":
        return home / "Library" / "Logs" / "Unity" / "Editor.log"
    return home / ".config" / "unity3d" / "Editor.log"


def _list_unity_processes(*, os_name: str) -> str:
    """Best-effort process snapshot; never raises — a missing/failing tool
    must not break diagnostics collection, only degrade it."""
    try:
        if os_name == "Windows":
            command = ["tasklist", "/FI", "IMAGENAME eq Unity.exe"]
        else:
            command = ["ps", "-A", "-o", "pid,comm"]
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        return result.stdout
    except (OSError, subprocess.SubprocessError) as error:
        return f"<process list unavailable: {error}>"


def capture_wait_diagnostics(
    *, process: subprocess.Popen, log: Path, os_name: str, home: Path
) -> dict[str, object]:
    """One diagnostic snapshot: process liveness (poll()), -logFile
    presence/size, the OS-default Editor.log fallback location, and a
    Unity process list."""
    default_log = default_editor_log_path(os_name=os_name, home=home)
    return {
        "utc": datetime.now(UTC).isoformat(),
        "poll": process.poll(),
        "log_path": str(log),
        "log_exists": log.is_file(),
        "log_size": log.stat().st_size if log.is_file() else None,
        "default_log_path": str(default_log),
        "default_log_exists": default_log.is_file(),
        "default_log_size": default_log.stat().st_size if default_log.is_file() else None,
        "unity_processes": _list_unity_processes(os_name=os_name),
    }


def _append_diagnostics(path: Path, snapshot: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(snapshot) + "\n")


def wait_for_port_diagnosed(
    *,
    host: str,
    port: int,
    process: subprocess.Popen,
    log: Path,
    timeout: float,
    evidence_out: Path,
    os_name: str,
    home: Path | None = None,
    poll_interval: float = 30.0,
) -> None:
    """Same contract as gauntlet.hosted_conformance.wait_for_port (raises
    HostedConformanceError on early exit or timeout), plus a periodic
    diagnostic snapshot every poll_interval seconds to
    evidence_out/wait-diagnostics.jsonl and one final snapshot on timeout —
    Run 4 (Windows, 4 consecutive matrix runs) produced a timeout with zero
    other evidence at all. A separate function from the shared, already-
    proven wait_for_port so this never risks the batchmode conformance
    lanes that depend on it."""
    home = home or Path.home()
    diagnostics_path = evidence_out / "wait-diagnostics.jsonl"
    deadline = time.monotonic() + timeout
    next_snapshot = time.monotonic()
    last_error: OSError | None = None
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise HostedConformanceError(
                f"Unity worker for port {port} exited early with {process.returncode}.\n"
                + tail_log(log)
            )
        if time.monotonic() >= next_snapshot:
            _append_diagnostics(
                diagnostics_path,
                capture_wait_diagnostics(process=process, log=log, os_name=os_name, home=home),
            )
            next_snapshot = time.monotonic() + poll_interval
        try:
            with socket.create_connection((host, port), timeout=1.0):
                return
        except OSError as exc:
            last_error = exc
            time.sleep(1.0)
    _append_diagnostics(
        diagnostics_path,
        capture_wait_diagnostics(process=process, log=log, os_name=os_name, home=home),
    )
    raise HostedConformanceError(
        f"Timed out waiting for Unity MCP port {host}:{port}: {last_error}\n" + tail_log(log)
    )


__all__ = [
    "FsrQualificationError",
    "HostedConformanceError",
    "GUARDED_BASE_PREFIXES",
    "VALID_OUTCOMES",
    "EXPECTED_CELLS",
    "REQUIRED_PASS_CELLS",
    "DOCUMENTED_BLOCKED_CELLS",
    "load_lock",
    "resolve_cell",
    "build_runtime_receipt",
    "write_pilot_evidence",
    "assert_base_sha_untouched",
    "validate_receipt_set",
    "build_headed_unity_command",
    "build_headed_unity_environment",
    "byte_diagnostic",
    "default_editor_log_path",
    "classify_dialog_evidence",
    "capture_wait_diagnostics",
    "wait_for_port_diagnosed",
]
