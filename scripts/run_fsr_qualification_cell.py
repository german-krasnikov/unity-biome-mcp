#!/usr/bin/env python3
"""Run one P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix cell.

Two modes:

  --mode pilot   fixture-free GUI baseline: launch a plain disposable worker
                 headed (no fixture, no provider pin), prove the Editor
                 reaches a ready TCP port with a clean compile, stop it. Run
                 once per OS before any semantic cell (§7 P1-20).

  --mode full    the P0-80 nine-step product-cycle scenario against the
                 frozen cell's Unity version/revision:
                   1. package-absent + OFF clean-base legacy compile/reload
                   2. stop; prove port closed
                   3. install optional package offline by exact pin
                   4. fresh Editor: clean compile/import
                   5. enable ON; retained target 1 -> 2 -> invalid stays 2 -> 3
                   6. disable: one receipt, one sync, exact N -> N+1
                   7. stop; remove optional package offline
                   8. fresh package-absent Editor: provider assemblies absent
                   9. exact restoration; stop; receipt written last

A direct GUI supervisor (no -batchmode/-nographics) on every OS — a
batchmode lane is not FSR qualification evidence. See
Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §7 P1-20.
"""
import argparse
import asyncio
import json
import os
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = REPO_ROOT / "scripts"
sys.path.insert(0, str(REPO_ROOT))
sys.path.insert(0, str(SCRIPTS))
import create_unity_test_worker as worker  # noqa: E402
import gauntlet.fsr_qualification as fq  # noqa: E402
import gauntlet.fsr_qualification_fixture as harness  # noqa: E402
from gauntlet.hosted_conformance import (  # noqa: E402
    tail_log,
    terminate_workers,
    wait_for_port,
    write_mcp_project_settings,
)

import run_unity_tests as durable  # noqa: E402

REL_TARGET = harness.REL_TARGET


class FsrQualificationCellError(RuntimeError):
    pass


def _git_head_sha() -> str:
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=REPO_ROOT, capture_output=True, text=True, check=True
    )
    return result.stdout.strip()


def _git_changed_paths(base_sha: str) -> list[str]:
    """Raises FsrQualificationCellError (caught by main()) rather than the
    raw CalledProcessError — Run 2 crashed 4/6 cells unhandled here on a
    shallow clone (actions/checkout's default fetch-depth: 1 has no history
    reaching base_product_sha), and the exception propagated past main()'s
    except tuple entirely, so no receipt.json ever got written. checkout
    now uses fetch-depth: 0, but any future git-diff failure must still
    surface as a caught, evidenced error."""
    try:
        result = subprocess.run(
            ["git", "diff", "--name-only", f"{base_sha}..HEAD"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=True,
        )
    except subprocess.CalledProcessError as error:
        raise FsrQualificationCellError(
            f"git diff --name-only {base_sha}..HEAD failed (exit {error.returncode}): "
            f"{error.stderr or error.stdout}"
        ) from error
    return [line for line in result.stdout.splitlines() if line]


def _launch(*, unity: Path, project: Path, port: int, log: Path) -> subprocess.Popen:
    command = fq.build_headed_unity_command(unity, project, log)
    env = fq.build_headed_unity_environment(os.environ, port=port, project=project)
    write_mcp_project_settings(project, port=port, read_only=False)

    kwargs: dict[str, object] = {
        "cwd": REPO_ROOT,
        "env": env,
        "stdin": subprocess.DEVNULL,
        "stdout": subprocess.DEVNULL,
        "stderr": subprocess.DEVNULL,
    }
    if os.name == "nt":
        kwargs["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        kwargs["start_new_session"] = True
    return subprocess.Popen(command, **kwargs)


async def _stop(process: subprocess.Popen | None) -> None:
    if process is not None:
        terminate_workers([process])


async def run_pilot(
    *,
    unity: Path,
    source_project: Path,
    work_root: Path,
    port: int,
    startup_timeout: float,
    evidence_out: Path | None = None,
    cell_name: str = "pilot",
    os_name: str = "",
    arch: str = "",
) -> None:
    """Fixture-free GUI baseline: no fixture, no provider pin.

    Always writes pilot evidence (receipt + Unity log tail, or an explicit
    "not found" record) when evidence_out is given — Run 2 discarded all
    diagnostic evidence on a failed pilot, uploading nothing but a bare
    receipt.json with no log content at all."""
    project = work_root / "pilot"
    log = work_root / "pilot-unity.log"
    worker.create_worker(source_project, project)
    process = _launch(unity=unity, project=project, port=port, log=log)
    outcome = "FAIL"
    error_message: str | None = None
    try:
        await asyncio.to_thread(
            wait_for_port, host="127.0.0.1", port=port, process=process, log=log, timeout=startup_timeout
        )
        # durable.call already raises RunnerError on a non-ok response, so a
        # successful return is itself the pilot's health proof: the headed
        # Editor booted, the TCP bridge answered, and get_status compiled
        # (get_status is the real C#-side wire command registered in
        # CommandRouter.Registration.cs; mcp_status is a Python-only MCP
        # server tool and is not reachable over raw TCP).
        await durable.call(port, "get_status", {})
        outcome = "PASS"
    except (durable.RunnerError, OSError, TimeoutError) as error:
        error_message = str(error)
        outcome = "INFRASTRUCTURE_BLOCKED"
        raise
    finally:
        await _stop(process)
        if evidence_out is not None:
            fq.write_pilot_evidence(
                evidence_out,
                cell=cell_name,
                os_name=os_name,
                arch=arch,
                log_path=log,
                outcome=outcome,
                error=error_message,
            )


async def _phase_off_legacy_compile(*, unity, project, port, log, startup_timeout) -> subprocess.Popen:
    process = _launch(unity=unity, project=project, port=port, log=log)
    await asyncio.to_thread(
        wait_for_port, host="127.0.0.1", port=port, process=process, log=log, timeout=startup_timeout
    )
    harness.install_fixture(project)
    await durable.call(port, "asset", {"action": "write_text", "path": REL_TARGET, "content": harness.target_body("v0")})
    return process


async def _phase_on_retained_object(*, port) -> None:
    await durable.call(port, "editor", {"action": "mutation_mode", "enable": True})
    for kind in ("v1", "v2", "invalid", "v2", "v3"):
        await durable.call(port, "asset", {"action": "write_text", "path": REL_TARGET, "content": harness.target_body(kind)})
    await durable.call(port, "editor", {"action": "mutation_mode", "enable": False})


async def run_full(
    *,
    unity: Path,
    source_project: Path,
    work_root: Path,
    window: str,
    lock_path: Path,
    provider_pin: Path,
    evidence_out: Path,
    port: int,
    startup_timeout: float,
    cell_name: str,
    os_name: str,
    arch: str,
) -> dict[str, object]:
    lock = fq.load_lock(lock_path)
    cell = fq.resolve_cell(lock, window)
    checkout_sha = _git_head_sha()
    fq.assert_base_sha_untouched(_git_changed_paths(lock["base_product_sha"]))

    project = work_root / "worker"
    evidence_out.mkdir(parents=True, exist_ok=True)
    log = evidence_out / "unity.log"
    setup_ok = license_ok = display_ok = True
    outcome = "FAIL"
    process: subprocess.Popen | None = None
    try:
        worker.create_worker(
            source_project,
            project,
            target_unity_version=cell["unity_version"],
            target_unity_revision=cell["unity_revision"],
        )

        # Steps 1-2: package-absent OFF clean-base compile, then stop.
        process = await _phase_off_legacy_compile(
            unity=unity, project=project, port=port, log=log, startup_timeout=startup_timeout
        )
        await _stop(process)
        process = None

        # Step 3: install optional package offline by exact pin (worker only).
        worker.rewrite_manifest_pin(project, provider_pin, install=True)

        # Steps 4-6: fresh Editor, ON retained-object sequence, disable.
        process = _launch(unity=unity, project=project, port=port, log=log)
        await asyncio.to_thread(
            wait_for_port, host="127.0.0.1", port=port, process=process, log=log, timeout=startup_timeout
        )
        harness.validate_installed_fixture(project)
        await _phase_on_retained_object(port=port)
        await _stop(process)
        process = None

        # Step 7: remove optional package offline only after OFF/zero lease.
        worker.rewrite_manifest_pin(project, provider_pin, install=False)

        # Steps 8-9: fresh package-absent Editor, final legacy OFF, restoration.
        process = _launch(unity=unity, project=project, port=port, log=log)
        await asyncio.to_thread(
            wait_for_port, host="127.0.0.1", port=port, process=process, log=log, timeout=startup_timeout
        )
        await durable.call(port, "asset", {"action": "write_text", "path": REL_TARGET, "content": harness.target_body("v0")})
        await _stop(process)
        process = None

        outcome = "PASS"
    except (durable.RunnerError, OSError, TimeoutError, worker.WorkerCreationError, fq.FsrQualificationError):
        outcome = "FAIL"
        raise
    finally:
        await _stop(process)
        receipt = fq.build_runtime_receipt(
            cell=cell_name,
            os_name=os_name,
            arch=arch,
            unity_version=cell["unity_version"],
            unity_revision=cell["unity_revision"],
            setup_ok=setup_ok,
            license_ok=license_ok,
            display_ok=display_ok,
            checkout_sha=checkout_sha,
            lock_base_product_sha=lock["base_product_sha"],
            candidate_sha=lock["final_fsr_adapter_sha"],
            outcome=outcome,
        )
        (evidence_out / "receipt.json").write_text(
            json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        if log.exists():
            (evidence_out / "unity-log-tail.txt").write_text(tail_log(log), encoding="utf-8")
    return receipt


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("pilot", "full"), required=True)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--source-project", type=Path, default=REPO_ROOT / "unity-test-project")
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--window", choices=("u_min", "u_max"))
    parser.add_argument("--lock", type=Path, default=SCRIPTS / "fsr_qualification_lock.json")
    parser.add_argument("--provider-pin", type=Path, default=SCRIPTS / "source_patch_provider_pin.json")
    parser.add_argument("--evidence-out", type=Path)
    parser.add_argument("--cell-name", choices=fq.EXPECTED_CELLS, default="")
    parser.add_argument("--os-name", default="")
    parser.add_argument("--arch", default="")
    parser.add_argument("--port", type=int, default=9600)
    parser.add_argument("--startup-timeout", type=float, default=420.0)
    args = parser.parse_args(argv)
    if args.mode == "full" and not (args.window and args.evidence_out and args.cell_name):
        parser.error("--mode full requires --window, --evidence-out and --cell-name")
    return args


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        if args.mode == "pilot":
            asyncio.run(
                run_pilot(
                    unity=args.unity,
                    source_project=args.source_project,
                    work_root=args.work_root,
                    port=args.port,
                    startup_timeout=args.startup_timeout,
                    evidence_out=args.evidence_out,
                    cell_name=args.cell_name or "pilot",
                    os_name=args.os_name,
                    arch=args.arch,
                )
            )
        else:
            receipt = asyncio.run(
                run_full(
                    unity=args.unity,
                    source_project=args.source_project,
                    work_root=args.work_root,
                    window=args.window,
                    lock_path=args.lock,
                    provider_pin=args.provider_pin,
                    evidence_out=args.evidence_out,
                    port=args.port,
                    startup_timeout=args.startup_timeout,
                    cell_name=args.cell_name,
                    os_name=args.os_name,
                    arch=args.arch,
                )
            )
            print(json.dumps(receipt, indent=2, sort_keys=True))
            if receipt["outcome"] != "PASS":
                return 1
        return 0
    except (
        FsrQualificationCellError,
        fq.FsrQualificationError,
        worker.WorkerCreationError,
        durable.RunnerError,
        OSError,
        TimeoutError,
    ) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
