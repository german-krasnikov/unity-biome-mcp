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
import gauntlet.editor_prefs_preseed as preseed  # noqa: E402
import gauntlet.fsr_qualification as fq  # noqa: E402
import gauntlet.fsr_qualification_fixture as harness  # noqa: E402
from gauntlet.hosted_conformance import (  # noqa: E402
    HostedConformanceError,
    tail_log,
    terminate_workers,
    write_mcp_project_settings,
)

import run_unity_tests as durable  # noqa: E402

REL_TARGET = harness.REL_TARGET

# Run 8 (33396935103): the old sequence ("v1", "v2", "invalid", "v2", "v3")
# repeated "v2" right after "invalid" is correctly rejected pre-effect —
# since the rejection leaves the file still at "v2", writing "v2" again is
# a genuine no-op that any correct body-only classifier legitimately
# refuses as "no-body-change" ("rejected the replacement body; no effect").
# Never a classifier/BOM/JSON-unescape bug — present since f5c5b746, the
# very first commit that created the matrix. Each step must differ from
# its predecessor.
ON_MODE_KIND_SEQUENCE = ("v1", "v2", "invalid", "v3")


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
    command = fq.build_headed_unity_command(unity, project, log, force_d3d11=os.name == "nt")
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


def _apply_preseed(
    project: Path, *, os_name: str, evidence_out: Path | None = None, label: str = ""
) -> dict[str, object]:
    """Defense-in-depth (Run 4 root cause): FSR's first-run modal dialog
    blocks ProcessInitializeOnLoadAttributes on a headed Editor before the
    MCP listener ever starts. Called before EVERY Unity launch, not just
    the cell's first (Run 5: min-linux-x64 still hung even with preseed
    applied once at cell start — an intermediate Unity process exit can
    overwrite the underlying prefs store, losing the preseeded values
    before the actually-vulnerable launch, steps 4-6 with the package
    installed, ever runs). Never lets a preseed failure abort the cell —
    this is a second, independent layer, not the primary fix (the fork
    owner's adapter guard is).

    When evidence_out is given, also captures the real on-disk/registry
    prefs snapshot right after this write (Run 6: min-linux-x64 reported
    applied=true yet FSR's auto-refresh check still did not return early —
    capturing the actual content lets a future run compare against what
    Unity itself writes, instead of guessing the format again)."""
    try:
        receipt = preseed.preseed_editor_prefs(project, os_name=os_name)
    except preseed.EditorPrefsPreseedError as preseed_error:
        receipt = {"mechanism": os_name, "applied": False, "error": str(preseed_error)}
    if evidence_out is not None:
        # Run 8 (33396935103): the receipt only carries company_name on a
        # successful Linux write — falls back to flat-only snapshot
        # (unchanged prior behavior) on any other OS or a failed preseed.
        snapshot = preseed.read_prefs_snapshot(
            os_name,
            company_name=receipt.get("company_name"),
            product_name=receipt.get("product_name"),
        )
        if snapshot is not None:
            evidence_out.mkdir(parents=True, exist_ok=True)
            suffix = f"-{label}" if label else ""
            (evidence_out / f"prefs-after-preseed{suffix}.txt").write_text(
                snapshot, encoding="utf-8"
            )
    return receipt


def _write_final_prefs_snapshot(evidence_out: Path | None, *, os_name: str, project: Path | None = None) -> None:
    """Post-mortem: captures the real prefs content AFTER the cell ends
    (success or failure) — Run 6's ask: compare this against the
    after-preseed snapshots to see whether Unity itself rewrote/ignored
    what was preseeded. Best-effort company/product resolution (Run 8:
    33396935103) — never fails this diagnostic-only capture."""
    if evidence_out is None:
        return
    company_name = product_name = None
    if project is not None:
        try:
            company_name = preseed.resolve_company_name(project)
            product_name = preseed.resolve_product_name(project)
        except preseed.EditorPrefsPreseedError:
            pass
    snapshot = preseed.read_prefs_snapshot(os_name, company_name=company_name, product_name=product_name)
    if snapshot is None:
        return
    evidence_out.mkdir(parents=True, exist_ok=True)
    (evidence_out / "prefs-final.txt").write_text(snapshot, encoding="utf-8")


LINUX_LICENSE_FILE_REL = Path(".local") / "share" / "unity3d" / "Unity" / "Unity_lic.ulf"

# P1-30: structural off-mode evidence — mirrors
# scripts/fixtures/fsr_qualification/Editor/CycleInstrumentation.cs
# exactly (same denylist fragments, same domain-loads.jsonl path/shape) so
# a fixture change and a driver change can never silently drift apart
# without a test catching it.
DOMAIN_LOAD_DENYLIST_FRAGMENTS = (
    "roslyn", "codeanalysis", "harmony", "cecil", "monomod", "fastscriptreload",
)
DOMAIN_LOADS_REL = (
    Path("Library") / "UnityMCP" / "FsrQualificationCell" / "fsr-qualification" / "domain-loads.jsonl"
)


async def _call_retrying_reload_race(
    port: int, command: str, args: dict[str, object],
    *, retries: int = 10, retry_delay: float = 2.0,
) -> str:
    """Wraps durable.call, retrying on durable.TransportUncertain (a
    domain reload's "going_away" announcement racing the response) — this
    codebase's established convention (run_unity_tests.py's own retry
    semantics; "timeout/reload disconnect is nonterminal") is that this
    race is expected, not a hard failure, for any command that either
    directly triggers a reload (disabling ON mode, a legacy .cs write) or
    queries state immediately after one.

    Run 13 (33410330964): the very first live oracle query after
    triggering the disable-reload raced it and failed both required
    cells. Run 14 (33411347829) recurred identically on min-linux-x64
    after _query_oracle alone got a retry — on-mode-write-diagnostics.json
    again showed the full ON-mode sequence already complete and no
    off-mode-evidence.json at all, meaning the SAME race can equally hit
    the disable call and the legacy writes themselves, not only the
    oracle query that follows them. Every reload-adjacent durable.call in
    the off-mode phases goes through this one wrapper now, not just
    _query_oracle's own. A genuine RunnerError (a real compile/command
    failure) still fails fast, never silently retried away."""
    last_error: durable.TransportUncertain | None = None
    for _attempt in range(retries):
        try:
            return await durable.call(port, command, args)
        except durable.TransportUncertain as error:
            last_error = error
            await asyncio.sleep(retry_delay)
    raise last_error


async def _query_oracle(
    port: int, *, retries: int = 10, retry_delay: float = 2.0
) -> dict[str, str]:
    """One execute_code round trip to
    SourcePatchHarness.CycleInstrumentation.QueryOracle() — retained-object
    identity, current Compute() value, domain epoch/stamp, isCompiling, and
    the independent compile-started counter, all consistent as of one
    moment. Parses its "key=value|key=value..." contract; a malformed pair
    (no "=") is skipped, never raised on — evidence collection must never
    itself become a new source of cell failure. See
    _call_retrying_reload_race's docstring for why this retries."""
    raw = await _call_retrying_reload_race(
        port, "execute_code",
        {"code": "return SourcePatchHarness.CycleInstrumentation.QueryOracle();"},
        retries=retries, retry_delay=retry_delay,
    )
    result: dict[str, str] = {}
    for pair in raw.split("|"):
        if "=" not in pair:
            continue
        key, _, value = pair.partition("=")
        result[key] = value
    return result


async def _wait_for_oracle_settle(
    port: int, *, timeout: float = 60.0, poll_interval: float = 0.5
) -> dict[str, str]:
    """Polls _query_oracle until compiling=false — used right after a
    reload-triggering write/mode-change so the evidence captured is the
    settled post-reload state, not a mid-compile snapshot. Raises rather
    than silently returning a still-compiling snapshot: DoD language is
    explicit that no cycle evidence may be "skipped/uncertain" (§6 P0-80)."""
    oracle = await _query_oracle(port)
    deadline = asyncio.get_event_loop().time() + timeout
    while oracle.get("compiling") == "true":
        if asyncio.get_event_loop().time() >= deadline:
            raise FsrQualificationCellError(
                f"Unity did not settle (still compiling) within {timeout}s"
            )
        await asyncio.sleep(poll_interval)
        oracle = await _query_oracle(port)
    return oracle


def _read_domain_loads(project: Path) -> list[dict[str, object]]:
    """Reads CycleInstrumentation's domain-loads.jsonl — one JSON record
    per domain load (cold start and every Domain Reload within the
    process), already written into the worker's own Library/ folder during
    every real run but never previously read back by the driver (P1-20
    reviewer gap #2). Missing file -> []; a malformed line is skipped, not
    raised on, since a partial write mid-flush is a real possibility this
    evidence-reader must tolerate, not amplify into a cell failure."""
    path = project / DOMAIN_LOADS_REL
    if not path.is_file():
        return []
    records: list[dict[str, object]] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            records.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return records


def _manifest_matches_pre_pin(project: Path, pre_pin_manifest: str) -> bool:
    """Step 7 (§6 P0-80): "remove optional package offline only after
    Off/zero lease" — proves the manifest is restored exactly byte-for-
    byte, not merely "rewrite_manifest_pin raised no exception"."""
    manifest_path = project / "Packages" / "manifest.json"
    if not manifest_path.is_file():
        return False
    return manifest_path.read_text(encoding="utf-8") == pre_pin_manifest


def _int_or_none(value: object) -> int | None:
    try:
        return int(value)  # type: ignore[arg-type]
    except (TypeError, ValueError):
        return None


async def _phase_off_disable_evidence(*, port: int, project: Path) -> dict[str, object]:
    """Step 6 (§6 P0-80): "disable: one receipt, one sync, same PID/
    project, exact N -> N+1, clean compile and v3 behavior from normally
    compiled source." P1-20 reviewer gap #2 (GO_WITH_GAPS): this evidence
    previously existed only as unstructured unity.log text, never
    validated independently by the receipt itself.

    Queries CycleInstrumentation.QueryOracle() immediately before
    disabling ON mode (epoch/compile-count baseline), disables (the one
    legacy compile/reload this step requires), waits for that reload to
    settle, then queries again — the delta between the two oracle
    snapshots is the proof, not a log grep. same_pid comes from every
    domain-loads.jsonl record's pid across the whole launch (not just the
    two endpoints), so a transient respawn in between can't hide. On any
    check failing, raises with whatever evidence was collected attached as
    .off_mode_evidence — the same pattern _phase_on_retained_object uses
    for .byte_diagnostics."""
    evidence: dict[str, object] = {}

    before = await _query_oracle(port)
    evidence["epoch_before"] = before.get("epoch")
    evidence["compile_started_count_before"] = before.get("compileCount")

    evidence["disable_result"] = await _call_retrying_reload_race(
        port, "editor", {"action": "mutation_mode", "enable": False}
    )

    after = await _wait_for_oracle_settle(port)
    evidence["epoch_after"] = after.get("epoch")
    evidence["compile_started_count_after"] = after.get("compileCount")
    evidence["compute_after_disable"] = after.get("compute")

    epoch_before_i = _int_or_none(evidence["epoch_before"])
    epoch_after_i = _int_or_none(evidence["epoch_after"])
    epoch_delta = (
        epoch_after_i - epoch_before_i if epoch_before_i is not None and epoch_after_i is not None else None
    )
    evidence["epoch_delta"] = epoch_delta
    evidence["epoch_delta_is_one"] = epoch_delta == 1

    count_before_i = _int_or_none(evidence["compile_started_count_before"])
    count_after_i = _int_or_none(evidence["compile_started_count_after"])
    count_delta = (
        count_after_i - count_before_i if count_before_i is not None and count_after_i is not None else None
    )
    evidence["compile_started_count_delta"] = count_delta
    evidence["compile_started_count_delta_is_one"] = count_delta == 1

    evidence["compute_after_disable_is_3"] = evidence["compute_after_disable"] == "3"

    domain_loads = _read_domain_loads(project)
    pids = {record.get("pid") for record in domain_loads if "pid" in record}
    evidence["same_pid"] = len(pids) <= 1
    evidence["editor_pid"] = domain_loads[-1].get("pid") if domain_loads else None

    checks = (
        "epoch_delta_is_one", "compile_started_count_delta_is_one",
        "compute_after_disable_is_3", "same_pid",
    )
    failed = [name for name in checks if not evidence.get(name)]
    if failed:
        error = FsrQualificationCellError(f"step 6 off-mode evidence check(s) failed: {failed}")
        error.off_mode_evidence = evidence
        raise error
    return evidence


async def _phase_final_restore(
    *, port: int, project: Path, target_path: Path
) -> dict[str, object]:
    """Steps 8-9 (§6 P0-80): "8. fresh package-absent Editor: provider
    assemblies absent, base compile clean, another legacy `.cs`
    write/reload; 9. exact source/meta/mtime/project settings
    restoration." Two writes within the same fresh launch: v4 first
    (proves legacy compile still works cleanly with the package
    physically gone, and CycleInstrumentation's own denylist scan of
    AppDomain assemblies confirms none of the provider's names are
    loaded), then v0 (restores the pristine baseline byte-for-byte,
    verified by sha256, not merely "no exception raised"). On any check
    failing, raises with whatever evidence was collected attached as
    .off_mode_evidence."""
    evidence: dict[str, object] = {}

    evidence["legacy_write_result"] = await _call_retrying_reload_race(
        port, "asset", {"action": "write_text", "path": REL_TARGET, "content": harness.target_body("v4")}
    )
    oracle_v4 = await _wait_for_oracle_settle(port)
    evidence["compute_after_legacy_write"] = oracle_v4.get("compute")
    evidence["compute_after_legacy_write_is_4"] = oracle_v4.get("compute") == "4"

    domain_loads = _read_domain_loads(project)
    last_assemblies = domain_loads[-1].get("assemblies", []) if domain_loads else []
    found = [
        assembly.get("name", "") for assembly in last_assemblies
        if any(fragment in assembly.get("name", "").lower() for fragment in DOMAIN_LOAD_DENYLIST_FRAGMENTS)
    ]
    evidence["assembly_needles_found"] = found
    evidence["assembly_needles_absent"] = not found

    evidence["restore_write_result"] = await _call_retrying_reload_race(
        port, "asset", {"action": "write_text", "path": REL_TARGET, "content": harness.target_body("v0")}
    )
    await _wait_for_oracle_settle(port)
    restored_bytes = target_path.read_bytes() if target_path.is_file() else b""
    pristine_bytes = harness.target_body("v0").encode("utf-8")
    evidence["restored_sha256"] = fq.byte_diagnostic(restored_bytes)["sha256"]
    evidence["pristine_sha256"] = fq.byte_diagnostic(pristine_bytes)["sha256"]
    evidence["restore_sha_matches"] = evidence["restored_sha256"] == evidence["pristine_sha256"]

    checks = ("compute_after_legacy_write_is_4", "assembly_needles_absent", "restore_sha_matches")
    failed = [name for name in checks if not evidence.get(name)]
    if failed:
        error = FsrQualificationCellError(f"steps 8-9 restore evidence check(s) failed: {failed}")
        error.off_mode_evidence = evidence
        raise error
    return evidence




def _license_file_diagnostic(*, os_name: str, label: str) -> dict[str, object] | None:
    """Run 8 (33396935103): min-linux-x64's full run crashed on "No valid
    Unity Editor license found" right after an (now fixed) adaptive-preseed
    bug overwrote Unity's real license file (find_candidate_prefs_paths'
    old "contains \'unity\'" filter matched it; now restricted to the
    exact basename "prefs"). Captures this file's state at each launch
    boundary so any future recurrence — from this or any other cause — is
    immediately visible in evidence, never silently re-diagnosed from a
    crashed Editor's log alone. Linux-only: this exact path is not where
    macOS/Windows store their license."""
    if os_name != "Linux":
        return None
    path = Path.home() / LINUX_LICENSE_FILE_REL
    if not path.is_file():
        return {"label": label, "path": str(path), "exists": False}
    try:
        data = path.read_bytes()
    except OSError as error:
        return {"label": label, "path": str(path), "exists": True, "error": str(error)}
    return {"label": label, "path": str(path), "exists": True, **fq.byte_diagnostic(data)}


def _apply_adaptive_preseed(
    evidence_out: Path | None, *, os_name: str, project: Path
) -> dict[str, object] | None:
    """Run 7 (b): after pilot's discovery revealed what Unity actually
    touched, extend preseed to those candidate paths too — in addition to,
    never instead of, the already-known ~/.config/unity3d/prefs path.
    None when there is no pilot discovery report to read yet (e.g. pilot
    itself never ran) or on Windows (discovery is POSIX-only)."""
    if os_name == "Windows" or evidence_out is None:
        return None
    report_path = evidence_out / "pilot" / "prefs-discovery.txt"
    if not report_path.is_file():
        return None
    report = report_path.read_text(encoding="utf-8")
    product_name = preseed.resolve_product_name(project)
    return preseed.adaptive_preseed_from_discovery(report, product_name=product_name)


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
    unity_version: str | None = None,
    unity_revision: str | None = None,
) -> None:
    """Fixture-free GUI baseline: no fixture, no provider pin.

    unity_version/unity_revision (when both given) rewrite the worker's
    declared Unity version to match the launching Editor — Run 3 showed
    max-linux-x64/max-macos-arm64 pilots failing because this was omitted:
    the worker always defaulted to U_MIN (6000.0.65f1) while the U_MAX
    (6000.5.10f1) Editor launched headed against it, which risks Unity's
    interactive "project created with an earlier version, continue
    anyway?" dialog — it blocks indefinitely outside batchmode.

    Always writes pilot evidence (receipt + Unity log tail, or an explicit
    "not found" record) when evidence_out is given — Run 2 discarded all
    diagnostic evidence on a failed pilot, uploading nothing but a bare
    receipt.json with no log content at all."""
    project = work_root / "pilot"
    log = work_root / "pilot-unity.log"
    diagnostics_dir = evidence_out or work_root
    worker.create_worker(
        source_project,
        project,
        target_unity_version=unity_version,
        target_unity_revision=unity_revision,
    )
    # Defense-in-depth (Run 4 root cause): FSR's first-run modal dialog
    # blocks ProcessInitializeOnLoadAttributes on a headed Editor before the
    # MCP listener ever starts. Never lets a preseed failure abort the
    # cell — this is a second, independent layer, not the primary fix.
    preseed_receipt = _apply_preseed(
        project, os_name=os_name, evidence_out=evidence_out, label="pilot"
    )
    # Run 7: the tracked ~/.config/unity3d/prefs file is proven stable/
    # untouched across a whole cell run, yet Unity's behavior still implies
    # a non-default kAutoRefreshMode was read from somewhere — `find
    # -newer` reveals what Unity actually touches. POSIX-only (Windows has
    # no ~/.config convention); never fails the pilot on its own.
    discovery_marker = work_root / "pilot-discovery-marker"
    if os_name != "Windows":
        preseed.create_discovery_marker(discovery_marker)
    process = _launch(unity=unity, project=project, port=port, log=log)
    outcome = "FAIL"
    error_message: str | None = None
    try:
        await asyncio.to_thread(
            fq.wait_for_port_diagnosed,
            host="127.0.0.1", port=port, process=process, log=log,
            timeout=startup_timeout, evidence_out=diagnostics_dir, os_name=os_name,
        )
        # durable.call already raises RunnerError on a non-ok response, so a
        # successful return is itself the pilot's health proof: the headed
        # Editor booted, the TCP bridge answered, and get_status compiled
        # (get_status is the real C#-side wire command registered in
        # CommandRouter.Registration.cs; mcp_status is a Python-only MCP
        # server tool and is not reachable over raw TCP).
        await durable.call(port, "get_status", {})
        outcome = "PASS"
    except (durable.RunnerError, HostedConformanceError, OSError, TimeoutError) as error:
        error_message = str(error)
        outcome = "INFRASTRUCTURE_BLOCKED"
        raise
    finally:
        await _stop(process)
        _write_final_prefs_snapshot(evidence_out, os_name=os_name, project=project)
        if os_name != "Windows" and evidence_out is not None:
            report = preseed.discover_touched_config_files(marker=discovery_marker)
            evidence_out.mkdir(parents=True, exist_ok=True)
            (evidence_out / "prefs-discovery.txt").write_text(report, encoding="utf-8")
        if evidence_out is not None:
            fq.write_pilot_evidence(
                evidence_out,
                cell=cell_name,
                os_name=os_name,
                arch=arch,
                log_path=log,
                outcome=outcome,
                error=error_message,
                preseed=preseed_receipt,
            )


async def _phase_off_legacy_compile(
    *, unity, project, port, log, startup_timeout, evidence_out, os_name
) -> subprocess.Popen:
    """The fixture must already be on disk (see run_full, install_fixture
    called before this phase's launch) — this phase only launches and
    waits for the port. Run 6 (33390881487) showed why: install_fixture
    used to run AFTER Unity was already started, then an immediate
    redundant asset/write_text re-wrote the same v0 content through the
    legacy route — which adds a UTF-8 BOM (AssetDatabaseHelper.WriteText
    uses File.WriteAllText with .NET's BOM-including Encoding.UTF8, while
    SourcePatchModePolicy.TryApplyWrite's own newBytes never gets one) —
    bypassing Unity's normal startup asset-import for no reason. Installing
    before launch lets Unity's own startup AssetDatabase scan import it
    naturally, matching the proven local P0-80 shape."""
    process = _launch(unity=unity, project=project, port=port, log=log)
    await asyncio.to_thread(
        fq.wait_for_port_diagnosed,
        host="127.0.0.1", port=port, process=process, log=log,
        timeout=startup_timeout, evidence_out=evidence_out, os_name=os_name,
    )
    return process


async def _phase_on_retained_object(*, port, target_path: Path) -> list[dict[str, object]]:
    """Returns one byte-diagnostic entry per attempted write (Run 7: the
    classifier proved correct for the exact intended content when tested
    directly offline — ADMITTED cleanly — so the discrepancy must be in
    what actually reaches it live; capturing sha256 + edge bytes of the
    real before/after content lets a live run be compared against that
    offline proof). On any unexpected failure, whatever diagnostics were
    already collected are attached to the raised error as
    .byte_diagnostics so they are never lost.

    While mutation is ON, .cs writes must go through source_patch_write,
    never asset/write_text — the C# side explicitly rejects a legacy write
    on a .cs path in that state ("source patch active — legacy .cs write
    rejected pre-effect", Run 5: 33387852561). This is the same routing
    server/src/unity_mcp/tools/asset.py itself performs."""
    diagnostics: list[dict[str, object]] = []
    await durable.call(port, "editor", {"action": "mutation_mode", "enable": True})
    for kind in ON_MODE_KIND_SEQUENCE:
        content = harness.target_body(kind)
        before_bytes = target_path.read_bytes() if target_path.is_file() else b""
        entry: dict[str, object] = {
            "kind": kind,
            "before": fq.byte_diagnostic(before_bytes),
            "after": fq.byte_diagnostic(content.encode("utf-8")),
        }
        rejection: durable.RunnerError | None = None
        try:
            await durable.call(port, "source_patch_write", {"path": REL_TARGET, "content": content})
        except durable.RunnerError as error:
            rejection = error

        # The scenario deliberately proves an invalid (non body-only)
        # mutation is rejected pre-effect, not that it succeeds
        # ("1 -> 2 -> invalid stays 2 -> 3", §6 P0-80) — a rejection here
        # is the correct, expected outcome (Run 6: 33390881487 first
        # exposed this; the driver previously let this specific
        # RunnerError abort the whole cell).
        if kind == "invalid":
            if rejection is None:
                entry["result"] = "applied(unexpected)"
                diagnostics.append(entry)
                raise FsrQualificationCellError(
                    "source_patch_write unexpectedly accepted the invalid (non body-only) mutation"
                )
            if "rejected" not in str(rejection).lower():
                entry["result"] = f"rejected(unexpected-reason): {rejection}"
                diagnostics.append(entry)
                rejection.byte_diagnostics = diagnostics
                raise rejection
            entry["result"] = "rejected(expected)"
        elif rejection is not None:
            entry["result"] = f"rejected: {rejection}"
            diagnostics.append(entry)
            rejection.byte_diagnostics = diagnostics
            raise rejection
        else:
            entry["result"] = "applied"
        diagnostics.append(entry)
    await durable.call(port, "editor", {"action": "mutation_mode", "enable": False})
    return diagnostics


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
    error_message: str | None = None
    process: subprocess.Popen | None = None
    preseed_attempts: list[dict[str, object]] = []
    on_mode_diagnostics: list[dict[str, object]] | None = None
    license_diagnostics: list[dict[str, object]] = []
    off_mode_evidence: dict[str, object] = {}
    try:
        print(f"PHASE: create worker ({cell['unity_version']})", flush=True)
        worker.create_worker(
            source_project,
            project,
            target_unity_version=cell["unity_version"],
            target_unity_revision=cell["unity_revision"],
        )
        # Before Unity ever launches, so its own startup AssetDatabase scan
        # imports the file naturally — see _phase_off_legacy_compile's
        # docstring for the BOM-introducing bug (Run 6: 33390881487) this
        # ordering avoids.
        harness.install_fixture(project)

        print("PHASE: steps 1-2 package-absent OFF compile", flush=True)
        preseed_attempts.append(
            _apply_preseed(project, os_name=os_name, evidence_out=evidence_out, label="steps1-2")
        )
        diag = _license_file_diagnostic(os_name=os_name, label="before-steps1-2")
        if diag is not None:
            license_diagnostics.append(diag)
        process = await _phase_off_legacy_compile(
            unity=unity, project=project, port=port, log=log, startup_timeout=startup_timeout,
            evidence_out=evidence_out, os_name=os_name,
        )
        await _stop(process)
        process = None

        print("PHASE: step 3 install optional package offline", flush=True)
        pre_pin_manifest = (project / "Packages" / "manifest.json").read_text(encoding="utf-8")
        worker.rewrite_manifest_pin(project, provider_pin, install=True)

        print("PHASE: steps 4-6 fresh Editor with package, ON retained-object", flush=True)
        preseed_attempts.append(
            _apply_preseed(project, os_name=os_name, evidence_out=evidence_out, label="steps4-6")
        )
        adaptive_result = _apply_adaptive_preseed(evidence_out, os_name=os_name, project=project)
        if adaptive_result is not None:
            preseed_attempts.append({"mechanism": "adaptive", **adaptive_result})
        diag = _license_file_diagnostic(os_name=os_name, label="before-steps4-6")
        if diag is not None:
            license_diagnostics.append(diag)
        process = _launch(unity=unity, project=project, port=port, log=log)
        await asyncio.to_thread(
            fq.wait_for_port_diagnosed,
            host="127.0.0.1", port=port, process=process, log=log,
            timeout=startup_timeout, evidence_out=evidence_out, os_name=os_name,
        )
        harness.validate_installed_fixture(project)
        on_mode_diagnostics = await _phase_on_retained_object(
            port=port, target_path=project / REL_TARGET
        )
        off_mode_evidence["step6_disable"] = await _phase_off_disable_evidence(
            port=port, project=project
        )
        await _stop(process)
        process = None

        print("PHASE: step 7 remove optional package offline", flush=True)
        worker.rewrite_manifest_pin(project, provider_pin, install=False)
        manifest_matches_pre_pin = _manifest_matches_pre_pin(project, pre_pin_manifest)
        off_mode_evidence["step7_manifest_restore"] = {
            "manifest_matches_pre_pin": manifest_matches_pre_pin
        }
        if not manifest_matches_pre_pin:
            raise FsrQualificationCellError(
                "step 7: restored manifest.json does not match its pre-pin content"
            )

        print("PHASE: steps 8-9 fresh package-absent Editor, final OFF", flush=True)
        preseed_attempts.append(
            _apply_preseed(project, os_name=os_name, evidence_out=evidence_out, label="steps8-9")
        )
        diag = _license_file_diagnostic(os_name=os_name, label="before-steps8-9")
        if diag is not None:
            license_diagnostics.append(diag)
        process = _launch(unity=unity, project=project, port=port, log=log)
        await asyncio.to_thread(
            fq.wait_for_port_diagnosed,
            host="127.0.0.1", port=port, process=process, log=log,
            timeout=startup_timeout, evidence_out=evidence_out, os_name=os_name,
        )
        off_mode_evidence["step8_9_final_restore"] = await _phase_final_restore(
            port=port, project=project, target_path=project / REL_TARGET
        )
        await _stop(process)
        process = None

        outcome = "PASS"
    except (
        durable.RunnerError,
        HostedConformanceError,
        OSError,
        TimeoutError,
        worker.WorkerCreationError,
        fq.FsrQualificationError,
    ) as error:
        error_message = str(error)
        outcome = "FAIL"
        on_mode_diagnostics = getattr(error, "byte_diagnostics", on_mode_diagnostics)
        # _phase_off_disable_evidence and _phase_final_restore are the only
        # two off-mode phases that attach partial evidence to a raised
        # error, they run sequentially, and each only ever fills its own
        # step key — so "whichever key is still unset" unambiguously
        # identifies which phase was in flight when it failed.
        partial_off_mode_evidence = getattr(error, "off_mode_evidence", None)
        if partial_off_mode_evidence is not None:
            if "step6_disable" not in off_mode_evidence:
                off_mode_evidence["step6_disable"] = partial_off_mode_evidence
            elif "step8_9_final_restore" not in off_mode_evidence:
                off_mode_evidence["step8_9_final_restore"] = partial_off_mode_evidence
        raise
    finally:
        await _stop(process)
        _write_final_prefs_snapshot(evidence_out, os_name=os_name, project=project)
        if on_mode_diagnostics is not None:
            (evidence_out / "on-mode-write-diagnostics.json").write_text(
                json.dumps(on_mode_diagnostics, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
        if license_diagnostics:
            (evidence_out / "license-file-diagnostics.json").write_text(
                json.dumps(license_diagnostics, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
        if off_mode_evidence:
            (evidence_out / "off-mode-evidence.json").write_text(
                json.dumps(off_mode_evidence, indent=2, sort_keys=True) + "\n", encoding="utf-8"
            )
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
            error=error_message,
            preseed={"attempts": preseed_attempts} if preseed_attempts else None,
            off_mode_evidence=off_mode_evidence if off_mode_evidence else None,
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
            unity_version = unity_revision = None
            if args.window:
                pilot_cell = fq.resolve_cell(fq.load_lock(args.lock), args.window)
                unity_version = pilot_cell["unity_version"]
                unity_revision = pilot_cell["unity_revision"]
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
                    unity_version=unity_version,
                    unity_revision=unity_revision,
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
