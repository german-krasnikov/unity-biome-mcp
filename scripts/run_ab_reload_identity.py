#!/usr/bin/env python3
"""N3 T3/T4: live dual-worker reload identity + compile-error recovery.

Two harness-owned disposable Unity workers (A, B) prove reload identity,
new-code execution, lost-ACK safety, and compile-error recovery across
independent projects. The owner's Unity (port 9600) is never touched.

This module holds: CLI parsing, the nonce fixture installer/mutator, and the
live orchestration that wires everything to real workers. Owner-safety
guards (canonical project path, ports must differ, foreign PID on a port
file) live in gauntlet.ab_reload_owner_safety; the evidence receipt schema
(required fields, build/validate/write) lives in gauntlet.ab_reload_receipt.
Phase logic is pure and lives in gauntlet.ab_reload_identity.run_t3 (T3
slices 1+2), gauntlet.ab_reload_lost_ack.run_lost_ack (T3 slice 3), and
gauntlet.ab_reload_compile_recovery.run_t4 (T4). See
Plans/N3-T3-T4-live-reload-identity.md.

    python3 scripts/run_ab_reload_identity.py \\
        --worker-a-dir /tmp/unity-ab-reload-A --worker-b-dir /tmp/unity-ab-reload-B \\
        --port-a 9620 --port-b 9630 --unity /path/to/Unity \\
        --receipt /tmp/ab-reload-receipt.json --confirm-disposable-worker
"""

import argparse
import asyncio
import os
import re
import shutil
import sys
import uuid
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = REPO_ROOT / "scripts"
sys.path.insert(0, str(REPO_ROOT))
sys.path.insert(0, str(SCRIPTS))

import gauntlet.ab_reload_identity as t3  # noqa: E402
from gauntlet.ab_reload_compile_recovery import T4Config, T4Seams, run_t4  # noqa: E402
from gauntlet.ab_reload_live_seams import (  # noqa: E402
    live_bridge_factory,
    live_send_increment_via_proxy,
    live_sync_unity,
)
from gauntlet.ab_reload_lost_ack import LostAckConfig, LostAckSeams, run_lost_ack  # noqa: E402
from gauntlet.ab_reload_negative_controls import (  # noqa: E402
    NegativeControlsConfig,
    NegativeControlsSeams,
    run_negative_controls,
)
from gauntlet.ab_reload_owner_safety import (  # noqa: E402
    DEFAULT_PORT_A,
    DEFAULT_PORT_B,
    UNITY_VERSION,
    UTF_VERSION,
    ABReloadIdentityError,
    validate_ports,
    validate_worker_project,
)
from gauntlet.ab_reload_proxy import CounterProxy  # noqa: E402
from gauntlet.ab_reload_receipt import build_receipt, validate_receipt, write_receipt  # noqa: E402
from gauntlet.fsr_qualification import wait_for_port_diagnosed  # noqa: E402
from gauntlet.hosted_conformance import terminate_workers  # noqa: E402
from run_fsr_qualification_cell import _launch  # noqa: E402

import run_unity_tests as durable  # noqa: E402

FIXTURE_SOURCE = Path(__file__).resolve().parent / "fixtures" / "ab_reload_harness"
FIXTURE_RELATIVE = Path("Assets/UnityMCPABReloadHarness")
FIXTURE_FILES = ("AbReloadNonce.cs", "UnityMCP.Worker.ABReloadHarness.asmdef")
NONCE_FILE_NAME = "AbReloadNonce.cs"
NONCE_PATTERN = re.compile(r"^[A-Za-z0-9_-]+$")


def _validate_nonce(nonce: str) -> None:
    if not NONCE_PATTERN.fullmatch(nonce):
        raise ABReloadIdentityError(
            f"Unsafe nonce {nonce!r}: only letters, digits, '-', '_' are allowed"
        )


def _render_nonce_source(nonce: str) -> str:
    _validate_nonce(nonce)
    template = (FIXTURE_SOURCE / NONCE_FILE_NAME).read_text(encoding="utf-8")
    occurrences = template.count("NONCE_PLACEHOLDER")
    if occurrences != 1:
        raise ABReloadIdentityError(
            f"Expected exactly one NONCE_PLACEHOLDER in the nonce template, found {occurrences}"
        )
    return template.replace("NONCE_PLACEHOLDER", nonce, 1)


def install_nonce_fixture(project: Path, nonce: str) -> Path:
    """Write AbReloadNonce.cs + its asmdef under the worker's Assets/."""
    validate_worker_project(project)
    target_dir = project.resolve() / FIXTURE_RELATIVE
    rendered = _render_nonce_source(nonce)  # validate before touching disk
    target_dir.mkdir(parents=True, exist_ok=True)
    (target_dir / NONCE_FILE_NAME).write_text(rendered, encoding="utf-8")
    asmdef_source = FIXTURE_SOURCE / "UnityMCP.Worker.ABReloadHarness.asmdef"
    shutil.copy2(asmdef_source, target_dir / asmdef_source.name)
    return target_dir


def modify_nonce(project: Path, new_nonce: str) -> Path:
    """Rewrite the installed AbReloadNonce.cs with a new nonce (the
    'changed C# method' new-code proof the harness recompiles)."""
    validate_worker_project(project)
    target = project.resolve() / FIXTURE_RELATIVE / NONCE_FILE_NAME
    if not target.is_file():
        raise ABReloadIdentityError(f"Nonce fixture not installed: {target}")
    target.write_text(_render_nonce_source(new_nonce), encoding="utf-8")
    return target


def inject_compile_error(project: Path) -> Path:
    """Append an invalid top-level statement so the next recompile fails."""
    validate_worker_project(project)
    target = project.resolve() / FIXTURE_RELATIVE / NONCE_FILE_NAME
    if not target.is_file():
        raise ABReloadIdentityError(f"Nonce fixture not installed: {target}")
    with target.open("a", encoding="utf-8") as stream:
        stream.write("\nSYNTAX ERROR;\n")
    return target


def repair_compile_error(project: Path, new_nonce: str) -> Path:
    """Restore a clean, compilable AbReloadNonce.cs carrying a new nonce."""
    return modify_nonce(project, new_nonce)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="A/B live dual-worker reload identity + compile-error recovery harness"
    )
    parser.add_argument("--worker-a-dir", type=Path, required=True)
    parser.add_argument("--worker-b-dir", type=Path, required=True)
    parser.add_argument("--port-a", type=int, default=DEFAULT_PORT_A)
    parser.add_argument("--port-b", type=int, default=DEFAULT_PORT_B)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--mode", choices=("t3", "t4", "both"), default="both")
    parser.add_argument(
        "--negative-controls", action=argparse.BooleanOptionalAction, default=None,
        help="Run Task 5's live negative controls after a T3 pass. Defaults to on for --mode both, "
             "off otherwise; only takes effect when T3 actually runs (--mode t3 or both).",
    )
    parser.add_argument("--timeout-seconds", type=float, default=900.0)
    parser.add_argument("--startup-timeout-seconds", type=float, default=300.0)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--confirm-disposable-worker", action="store_true")
    return parser


async def _run_lost_ack_live(host: str, project_a: str, port_a: int) -> dict[str, object]:
    """T3 slice 3 (wiring only, not exercised by offline tests): a
    CounterProxy in front of A's real port, one execute_code increment sent
    through it via a UnityBridge, its ACK dropped."""
    proxy = CounterProxy(host, port_a)
    listener = await asyncio.start_server(proxy.handle_client, host, 0)
    proxy_port = listener.sockets[0].getsockname()[1]
    try:
        seams = LostAckSeams(
            call=durable.call,
            send_increment_via_proxy=live_send_increment_via_proxy(host, proxy_port, project_a),
            proxy=proxy,
        )
        return await run_lost_ack(LostAckConfig(port_a=port_a), seams)
    finally:
        listener.close()
        await listener.wait_closed()


async def _run_negative_controls_live(
    args: argparse.Namespace, project_b: Path, port_b: int, old_nonce_a: str, new_nonce_a: str,
) -> tuple[dict[str, object], str]:
    """Task 5 (wiring only, not exercised by offline tests): on the SAME
    still-running B, really mutate + recompile its nonce so
    check_sentinel_unchanged has a genuine live change to catch, then
    restore B to a fresh third nonce. Returns the phase result and that
    restored nonce -- T4's B sentinel must stay pinned to whatever B
    actually holds afterward, not the pre-negative-controls value."""
    mutated_nonce_b = f"B2-mutated-{uuid.uuid4().hex[:8]}"
    restored_nonce_b = f"B3-restored-{uuid.uuid4().hex[:8]}"

    async def mutate_nonce_b() -> None:
        modify_nonce(project_b, mutated_nonce_b)

    async def restore_nonce_b() -> None:
        modify_nonce(project_b, restored_nonce_b)

    config = NegativeControlsConfig(
        port_b=port_b, project_b=str(project_b), old_nonce_a=old_nonce_a, new_nonce_a=new_nonce_a,
    )
    seams = NegativeControlsSeams(
        call=durable.call, sync_unity=live_sync_unity(args.host),
        mutate_nonce_b=mutate_nonce_b, restore_nonce_b=restore_nonce_b,
    )
    result = await run_negative_controls(config, seams)
    return result, restored_nonce_b


async def _run_t4_phase_live(
    args: argparse.Namespace, project_a: Path, port_a: int, port_b: int, nonce_b: str,
) -> dict[str, object]:
    """T4 (wiring only, not exercised by offline tests): reuses whichever
    A+B are already running -- inject a compile error into A's fixture,
    confirm the failure verdict, confirm the old nonce still executes and
    B never moves, then repair and confirm the new nonce + a fresh MVID."""
    injected_nonce_a = f"A3-broken-{uuid.uuid4().hex[:8]}"
    repaired_nonce_a = f"A4-repaired-{uuid.uuid4().hex[:8]}"

    async def break_compile() -> None:
        modify_nonce(project_a, injected_nonce_a)
        inject_compile_error(project_a)

    async def repair_compile() -> None:
        repair_compile_error(project_a, repaired_nonce_a)

    config = T4Config(
        port_a=port_a, port_b=port_b, project_a=str(project_a),
        injected_nonce_a=injected_nonce_a, repaired_nonce_a=repaired_nonce_a, nonce_b=nonce_b,
    )
    seams = T4Seams(
        call=durable.call, sync_unity=live_sync_unity(args.host),
        break_compile=break_compile, repair_compile=repair_compile,
    )
    return await run_t4(config, seams)


async def _run_ab_live(args: argparse.Namespace) -> dict[str, object]:
    """Launch A+B headed once, install nonce fixtures, run the phases
    args.mode selects (t3: slices 1-3; t4: compile-error recovery; both:
    T3 then T4 on the same still-running A+B), terminate by PID once.
    Reuses launch/wait/terminate exactly as an agent would -- wiring only,
    not exercised by this task's offline tests."""
    project_a = args.worker_a_dir.resolve()
    project_b = args.worker_b_dir.resolve()
    new_nonce_a = f"A2-{uuid.uuid4().hex[:8]}"
    nonce_b = f"B-{uuid.uuid4().hex[:8]}"
    install_nonce_fixture(project_a, f"A1-{uuid.uuid4().hex[:8]}")
    install_nonce_fixture(project_b, nonce_b)

    log_dir = args.receipt.resolve().parent
    log_dir.mkdir(parents=True, exist_ok=True)
    log_a, log_b = log_dir / "worker-a.log", log_dir / "worker-b.log"
    # Both launches happen inside the try so a failed second launch can
    # never leak the first: terminate_workers() only ever sees processes
    # that actually started.
    launched = []
    result: dict[str, object] = {}
    try:
        proc_a = _launch(unity=args.unity, project=project_a, port=args.port_a, log=log_a)
        launched.append(proc_a)
        proc_b = _launch(unity=args.unity, project=project_b, port=args.port_b, log=log_b)
        launched.append(proc_b)

        wait_for_port_diagnosed(
            host=args.host, port=args.port_a, process=proc_a, log=log_a,
            timeout=args.startup_timeout_seconds, evidence_out=log_dir, os_name=os.name,
        )
        wait_for_port_diagnosed(
            host=args.host, port=args.port_b, process=proc_b, log=log_b,
            timeout=args.startup_timeout_seconds, evidence_out=log_dir, os_name=os.name,
        )

        if args.mode in ("t3", "both"):
            # The changed C# method the recompile must present as new (slice 1's action).
            modify_nonce(project_a, new_nonce_a)
            config = t3.T3Config(
                port_a=args.port_a, port_b=args.port_b,
                project_a=str(project_a), project_b=str(project_b),
                new_nonce_a=new_nonce_a, nonce_b=nonce_b,
            )
            seams = t3.T3Seams(
                call=durable.call,
                sync_unity=live_sync_unity(args.host),
                bridge_factory=live_bridge_factory(args.host),
            )
            result.update(await t3.run_t3(config, seams))
            # gauntlet.ab_reload_identity.read_port() already proves A's advertised
            # port never changed; the wire has no pid= field (read_identity's own
            # docstring), so the harness's own launched-process liveness is the
            # PID half of "a reload must never become a restart".
            if proc_a.poll() is not None:
                raise ABReloadIdentityError(
                    f"Worker A process (pid={proc_a.pid}) exited during T3 -- a reload became a restart"
                )
            result["t3_lost_ack"] = await _run_lost_ack_live(args.host, str(project_a), args.port_a)

            negative_controls_enabled = (
                args.negative_controls if args.negative_controls is not None else args.mode == "both"
            )
            if negative_controls_enabled:
                identity_reload = result["t3_identity_reload"]
                nc_result, nonce_b = await _run_negative_controls_live(
                    args, project_b, args.port_b,
                    identity_reload["old_nonce"], identity_reload["new_nonce"],
                )
                result.update(nc_result)

        if args.mode in ("t4", "both"):
            result.update(
                await _run_t4_phase_live(args, project_a, args.port_a, args.port_b, nonce_b)
            )
    finally:
        terminate_workers(launched)
    result["worker_a"] = {"pid": proc_a.pid, "port": args.port_a, "project_path": str(project_a)}
    result["worker_b"] = {"pid": proc_b.pid, "port": args.port_b, "project_path": str(project_b)}
    return result


def main(argv: list[str] | None = None) -> int:
    args = build_arg_parser().parse_args(argv)
    if not args.confirm_disposable_worker:
        raise ABReloadIdentityError("Refusing to run without --confirm-disposable-worker")
    validate_ports(args.port_a, args.port_b)
    validate_worker_project(args.worker_a_dir)
    validate_worker_project(args.worker_b_dir)
    result = asyncio.run(_run_ab_live(args))
    receipt = build_receipt(
        source_sha=os.environ.get("GIT_SHA", ""),
        unity_version=UNITY_VERSION,
        utf_version=UTF_VERSION,
        worker_a=result["worker_a"],
        worker_b=result["worker_b"],
        t3_identity_reload=result.get("t3_identity_reload", {}),
        t3_cross_identity=result.get("t3_cross_identity", {}),
        t3_lost_ack=result.get("t3_lost_ack", {}),
        t3_negative_controls=result.get("t3_negative_controls", {}),
        t4_compile_error=result.get("t4_compile_error", {}),
        t4_sentinel=result.get("t4_sentinel", {}),
        cleanup={"a_terminated": True, "b_terminated": True},
    )
    if args.mode == "both":
        # Only the full mode runs every slice (T3 + negative controls + T4),
        # so only it can satisfy validate_receipt()'s non-empty-evidence
        # check on every field.
        validate_receipt(receipt)
    write_receipt(args.receipt, receipt)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
