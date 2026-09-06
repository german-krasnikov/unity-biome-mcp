#!/usr/bin/env python3
"""Mutation regression lane cell driver.

Resolves the Source Patch (FastScriptReload) provider pin, creates a
disposable Unity worker with the provider installed, launches Unity
headed, and -- in `full` mode -- runs the Python and C# mutation
regression lanes against it, writing a structured receipt.

  --mode pilot   provider-installed GUI baseline: create the worker, launch
                 headed, prove a clean compile, stop. No fixture, no lanes.

  --mode full    the above, plus installing the retained-object fixture
                 before launch and running both mutation regression lanes
                 (server/tests/mutation, then UnityMCP.Editor.Tests.Mutation).

See Plans/MUTATION-REGRESSION-MODULE.md section 8.
"""
import argparse
import asyncio
import json
import shutil
import subprocess
import sys
from datetime import UTC, datetime
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = REPO_ROOT / "scripts"
sys.path.insert(0, str(REPO_ROOT))
sys.path.insert(0, str(SCRIPTS))

import create_unity_test_worker as worker  # noqa: E402
import gauntlet.fsr_qualification as fq  # noqa: E402
import gauntlet.fsr_qualification_fixture as harness  # noqa: E402
import gauntlet.provider_ref as provider_ref  # noqa: E402
from gauntlet.hosted_conformance import terminate_workers  # noqa: E402
from gauntlet.mutation_regression import (  # noqa: E402
    MutationRegressionCellError,
    build_receipt,
    detect_os_name,
    git_head_sha,
    lock_sha_mismatch,
    read_upm_lock_hash,
    resolve_provider_pin,
)
from gauntlet.mutation_regression import run_csharp_lane as _run_csharp_lane  # noqa: E402
from gauntlet.mutation_regression import run_python_lane as _run_python_lane  # noqa: E402
from run_fsr_qualification_cell import _apply_preseed, _launch  # noqa: E402

import run_unity_tests as durable  # noqa: E402

KNOWN_ERRORS = (
    MutationRegressionCellError,
    worker.WorkerCreationError,
    provider_ref.ProviderRefError,
    fq.HostedConformanceError,
    durable.RunnerError,
    OSError,
    TimeoutError,
    subprocess.TimeoutExpired,
)


async def run_cell(
    *,
    project_root: Path,
    worker_dir: Path,
    port: int,
    unity: Path,
    provider_pin: Path,
    provider_resolved: Path | None,
    mode: str,
    receipt_path: Path,
    timeout_seconds: float = 420.0,
    keep_worker: bool = False,
) -> dict[str, object]:
    timeline: list[dict[str, object]] = []
    outcome = "FAIL"
    failed_phase: str | None = None
    error_message: str | None = None
    process: subprocess.Popen | None = None
    python_lane_result: dict[str, object] | None = None
    csharp_lane_result: dict[str, object] | None = None
    provider_info: dict[str, object] = {}
    os_name = detect_os_name()
    log = worker_dir.parent / "unity.log"

    def _mark(phase: str, status: str) -> None:
        timeline.append({"phase": phase, "utc": datetime.now(UTC).isoformat(), "status": status})

    try:
        failed_phase = "resolve_pin"
        _mark("resolve_pin", "start")
        resolved = resolve_provider_pin(provider_pin, provider_resolved, worker_dir)
        provider_info = {
            "package": resolved.package_name,
            "requested_ref": resolved.requested_ref,
            "resolved_sha": resolved.resolved_sha,
            "upm_lock_hash": None,
        }
        # Warn-only: the pin intentionally floats on master, so a drift
        # against the frozen qualification lock is informational, never
        # a phase failure.
        mismatch = lock_sha_mismatch(resolved.resolved_sha)
        if mismatch is not None:
            provider_info["lock_sha_mismatch"] = mismatch
        _mark("resolve_pin", "ok")

        failed_phase = "create_worker"
        _mark("create_worker", "start")
        worker.create_worker(
            project_root, worker_dir,
            source_patch_provider_pin=provider_pin,
            source_patch_provider_resolved=resolved.resolved_path,
        )
        _mark("create_worker", "ok")

        if mode == "full":
            failed_phase = "install_fixture"
            _mark("install_fixture", "start")
            harness.install_fixture(worker_dir)
            _mark("install_fixture", "ok")

        failed_phase = "launch"
        _mark("launch", "start")
        _apply_preseed(worker_dir, os_name=os_name)
        process = _launch(unity=unity, project=worker_dir, port=port, log=log)
        await asyncio.to_thread(
            fq.wait_for_port_diagnosed,
            host="127.0.0.1", port=port, process=process, log=log,
            timeout=timeout_seconds, evidence_out=worker_dir.parent, os_name=os_name,
        )
        compile_status = await durable.call(port, "get_compile_errors", {})
        if compile_status != "No compilation errors":
            raise MutationRegressionCellError(f"worker did not reach a clean compile: {compile_status}")
        provider_info["upm_lock_hash"] = read_upm_lock_hash(worker_dir, resolved.package_name)
        _mark("launch", "ok")

        if mode == "full":
            failed_phase = "python_lane"
            _mark("python_lane", "start")
            python_lane_result = _run_python_lane(host="127.0.0.1", port=port, project=worker_dir)
            if python_lane_result["exit_code"] != 0:
                raise MutationRegressionCellError(
                    f"python mutation lane failed (exit {python_lane_result['exit_code']})"
                )
            _mark("python_lane", "ok")

            # Stop on first lane failure -- the C# lane is never attempted
            # after a Python lane failure (documented A6a scope decision).
            failed_phase = "csharp_lane"
            _mark("csharp_lane", "start")
            csharp_lane_result = _run_csharp_lane(worker_dir=worker_dir, port=port)
            if csharp_lane_result["exit_code"] != 0:
                raise MutationRegressionCellError(
                    f"csharp mutation lane failed (exit {csharp_lane_result['exit_code']})"
                )
            _mark("csharp_lane", "ok")

        outcome = "PASS"
        failed_phase = None
    except KNOWN_ERRORS as error:
        error_message = str(error)
        outcome = "FAIL"
        raise
    finally:
        _mark("cleanup", "start")
        if process is not None:
            terminate_workers([process])
        if not keep_worker and worker_dir.exists():
            shutil.rmtree(worker_dir, ignore_errors=True)
        _mark("cleanup", "ok")
        receipt = build_receipt(
            checkout_sha=git_head_sha(),
            provider=provider_info,
            unity_version=worker.UNITY_VERSION,
            port=port,
            mode=mode,
            outcome=outcome,
            python_lane=python_lane_result,
            csharp_lane=csharp_lane_result,
            timeline=timeline,
            failed_phase=failed_phase,
            error=error_message,
        )
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return receipt


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=REPO_ROOT / "unity-test-project")
    parser.add_argument("--worker-dir", type=Path, required=True)
    parser.add_argument("--port", type=int, default=9610)
    parser.add_argument("--unity", type=Path, default=worker.DEFAULT_UNITY)
    parser.add_argument("--provider-pin", type=Path, default=SCRIPTS / "source_patch_provider_pin.json")
    parser.add_argument("--provider-resolved", type=Path, default=None)
    parser.add_argument("--mode", choices=("pilot", "full"), required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=float, default=420.0)
    parser.add_argument("--keep-worker", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        asyncio.run(
            run_cell(
                project_root=args.project_root,
                worker_dir=args.worker_dir,
                port=args.port,
                unity=args.unity,
                provider_pin=args.provider_pin,
                provider_resolved=args.provider_resolved,
                mode=args.mode,
                receipt_path=args.receipt,
                timeout_seconds=args.timeout_seconds,
                keep_worker=args.keep_worker,
            )
        )
        return 0
    except KNOWN_ERRORS as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
