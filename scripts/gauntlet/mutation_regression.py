"""Pure/leaf helpers for the mutation regression lane cell driver
(scripts/run_mutation_regression_cell.py) -- provider-pin resolution, UPM
lock hashing, JUnit/JSON output parsing, and the two lane subprocess
runners. Kept separate from the CLI/orchestrator so the driver file stays
within this project's file-size budget. See
Plans/MUTATION-REGRESSION-MODULE.md section 8.
"""
import json
import os
import platform
import re
import subprocess
import sys
from pathlib import Path
from typing import NamedTuple
from xml.etree import ElementTree

SCRIPTS = Path(__file__).resolve().parents[1]
REPO_ROOT = SCRIPTS.parent
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet import provider_ref  # noqa: E402

CSHARP_ASSEMBLY = "UnityMCP.Editor.Tests.Mutation"
# Measured 1485s live for the full lane (15 tests); 1800s keeps ~20% margin.
PYTHON_LANE_TIMEOUT_SECONDS = 1800.0
CSHARP_LANE_TIMEOUT_SECONDS = 1800.0
LOCK_PATH = SCRIPTS / "fsr_qualification_lock.json"


class MutationRegressionCellError(RuntimeError):
    pass


class ResolvedProvider(NamedTuple):
    resolved_path: Path | None
    package_name: str
    requested_ref: str
    resolved_sha: str


def detect_os_name() -> str:
    system = platform.system()
    if system == "Darwin":
        return "macOS"
    if system == "Windows":
        return "Windows"
    return "Linux"


def git_head_sha() -> str:
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=REPO_ROOT, capture_output=True, text=True, check=True
    )
    return result.stdout.strip()


def resolve_provider_pin(pin_path: Path, resolved_arg: Path | None, worker_dir: Path) -> ResolvedProvider:
    """Resolve a Source Patch provider pin to an exact SHA for
    create_worker(). Passthrough when `resolved_arg` is already given.
    Otherwise resolves now (git ls-remote, via provider_ref) only when the
    pin floats on a branch, writing `worker_dir.parent / "resolved-pin.json"`
    -- `worker_dir` itself must not exist yet (create_worker() refuses a
    pre-existing destination). A pin already pinned to a 40-hex SHA needs
    no resolution call at all."""
    pin_payload = json.loads(pin_path.read_text(encoding="utf-8"))
    package_name = str(pin_payload.get("package_name", ""))
    ref = str(pin_payload.get("ref", ""))
    ref_kind = pin_payload.get("ref_kind")

    if resolved_arg is not None:
        resolved_payload = json.loads(resolved_arg.read_text(encoding="utf-8"))
        return ResolvedProvider(
            resolved_path=resolved_arg,
            package_name=package_name,
            requested_ref=str(resolved_payload.get("requested_ref", ref)),
            resolved_sha=str(resolved_payload.get("ref", "")),
        )

    if ref_kind == "branch" or not provider_ref.SHA_RE.fullmatch(ref):
        worker_dir.parent.mkdir(parents=True, exist_ok=True)
        out_path = worker_dir.parent / "resolved-pin.json"
        resolved_payload = provider_ref.resolve_provider_ref(pin_path, out_path)
        return ResolvedProvider(
            resolved_path=out_path,
            package_name=package_name,
            requested_ref=str(resolved_payload.get("requested_ref", ref)),
            resolved_sha=str(resolved_payload.get("ref", "")),
        )

    return ResolvedProvider(resolved_path=None, package_name=package_name, requested_ref=ref, resolved_sha=ref)


def lock_sha_mismatch(resolved_sha: str) -> str | None:
    """Warn-only drift check against the frozen qualification lock's
    `final_fsr_adapter_sha` -- the provider pin intentionally floats on
    master (Plans/MUTATION-REGRESSION-MODULE.md section 6), so a mismatch
    is informational only, never fatal. Returns the lock's SHA when it is
    set and differs from `resolved_sha`; None when they match, the lock
    field is absent, or the lock file itself is missing/malformed. Reads
    `LOCK_PATH` fresh on every call (module global, not a default
    parameter) so tests can monkeypatch it."""
    if not LOCK_PATH.is_file():
        return None
    try:
        payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    lock_sha = payload.get("final_fsr_adapter_sha")
    if not lock_sha or lock_sha == resolved_sha:
        return None
    return str(lock_sha)


def read_upm_lock_hash(worker_dir: Path, package_name: str) -> str | None:
    """Best-effort read of the resolved provider's hash from the worker's
    own UPM lock file, once Unity has resolved packages. Missing/malformed
    file or absent dependency entry -> None, never raises."""
    lock_path = worker_dir / "Packages" / "packages-lock.json"
    if not lock_path.is_file():
        return None
    try:
        payload = json.loads(lock_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    dependency = payload.get("dependencies", {}).get(package_name)
    return dependency.get("hash") if isinstance(dependency, dict) else None


def parse_junit_counts(path: Path) -> dict[str, int | None]:
    """Parses a pytest --junit-xml report's top-level counts (root is
    either a bare <testsuite> or <testsuites><testsuite>...). A missing
    file or malformed XML returns all-None rather than raising -- evidence
    collection must never itself fail the lane."""
    if not path.is_file():
        return {"passed": None, "failed": None, "skipped": None}
    try:
        root = ElementTree.parse(path).getroot()
    except ElementTree.ParseError:
        return {"passed": None, "failed": None, "skipped": None}
    suite = root if root.tag == "testsuite" else root.find("testsuite")
    if suite is None:
        return {"passed": None, "failed": None, "skipped": None}
    total = int(suite.get("tests", 0))
    failures = int(suite.get("failures", 0))
    errors = int(suite.get("errors", 0))
    skipped = int(suite.get("skipped", 0))
    return {"passed": total - failures - errors - skipped, "failed": failures + errors, "skipped": skipped}


def extract_json_blob(text: str) -> dict[str, object] | None:
    """Extracts the single JSON object run_unity_tests.py --json prints on
    a successful run (only present after several plain progress lines)."""
    start = text.find("{")
    if start == -1:
        return None
    try:
        obj, _ = json.JSONDecoder().raw_decode(text[start:])
    except json.JSONDecodeError:
        return None
    return obj if isinstance(obj, dict) else None


def extract_run_id(text: str) -> str | None:
    """run_unity_tests.py always prints `run_id=<id>` before validating the
    terminal snapshot -- present on both a passing and a failing run,
    unlike the --json snapshot, which is only printed on success."""
    match = re.search(r"^run_id=(.+)$", text, re.MULTILINE)
    return match.group(1).strip() if match else None


def run_python_lane(*, host: str, port: int, project: Path) -> dict[str, object]:
    """Runs the Python mutation regression lane (server/tests/mutation)
    against an already-launched worker, via the repo's own server venv."""
    server_dir = REPO_ROOT / "server"
    junit_path = project.parent / "mutation-python-junit.xml"
    env = {
        **os.environ,
        "UNITY_MCP_RUN_MUTATION_LIVE": "1",
        "UNITY_MCP_HOST": host,
        "UNITY_MCP_PORT": str(port),
        "UNITY_MCP_PROJECT_PATH": str(project),
    }
    command = [
        str(server_dir / ".venv" / "bin" / "python"), "-m", "pytest", "tests/mutation",
        "-m", "live and mutation_live", f"--timeout={int(PYTHON_LANE_TIMEOUT_SECONDS)}", "-q",
        f"--junit-xml={junit_path}",
    ]
    result = subprocess.run(
        command, cwd=server_dir, env=env, capture_output=True, text=True,
        timeout=PYTHON_LANE_TIMEOUT_SECONDS + 60.0,
    )
    return {**parse_junit_counts(junit_path), "exit_code": result.returncode}


def run_csharp_lane(*, worker_dir: Path, port: int) -> dict[str, object]:
    """Runs the C# mutation regression EditMode assembly against an
    already-launched worker via run_unity_tests.py. On any failed/invalid
    outcome, run_unity_tests.py raises before ever printing its --json
    snapshot, so only run_id (always printed) and the exit code/stderr
    tail are recoverable in that case -- passed/failed/expected_count stay
    None (documented scope decision, see A6a report)."""
    command = [
        sys.executable, str(REPO_ROOT / "run_unity_tests.py"), "EditMode",
        "--project", str(worker_dir), "--port", str(port),
        "--assembly", CSHARP_ASSEMBLY, "--minimum-tests", "1",
        "--timeout", str(CSHARP_LANE_TIMEOUT_SECONDS), "--json",
    ]
    result = subprocess.run(
        command, cwd=REPO_ROOT, capture_output=True, text=True,
        timeout=CSHARP_LANE_TIMEOUT_SECONDS + 60.0,
    )
    run_id = extract_run_id(result.stdout)
    if result.returncode == 0:
        snapshot = extract_json_blob(result.stdout) or {}
        return {
            "passed": snapshot.get("passed"),
            "failed": snapshot.get("failed"),
            "expected_count": snapshot.get("expected_count"),
            "run_id": snapshot.get("run_id", run_id),
            "exit_code": 0,
        }
    return {
        "passed": None, "failed": None, "expected_count": None,
        "run_id": run_id, "exit_code": result.returncode,
        "error": (result.stderr or result.stdout).strip()[-2000:],
    }


def build_receipt(
    *, checkout_sha: str, provider: dict[str, object], unity_version: str, port: int, mode: str,
    outcome: str, python_lane: dict[str, object] | None, csharp_lane: dict[str, object] | None,
    timeline: list[dict[str, object]], failed_phase: str | None, error: str | None = None,
) -> dict[str, object]:
    receipt: dict[str, object] = {
        "schema_version": 1,
        "checkout_sha": checkout_sha,
        "provider": provider,
        "unity_version": unity_version,
        "port": port,
        "mode": mode,
        "outcome": outcome,
        "python_lane": python_lane,
        "csharp_lane": csharp_lane,
        "timeline": timeline,
        "failed_phase": failed_phase,
    }
    if error:
        receipt["error"] = error
    return receipt


__all__ = [
    "MutationRegressionCellError",
    "ResolvedProvider",
    "CSHARP_ASSEMBLY",
    "PYTHON_LANE_TIMEOUT_SECONDS",
    "CSHARP_LANE_TIMEOUT_SECONDS",
    "LOCK_PATH",
    "detect_os_name",
    "git_head_sha",
    "resolve_provider_pin",
    "lock_sha_mismatch",
    "read_upm_lock_hash",
    "parse_junit_counts",
    "extract_json_blob",
    "extract_run_id",
    "run_python_lane",
    "run_csharp_lane",
    "build_receipt",
]
