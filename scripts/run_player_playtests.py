#!/usr/bin/env python3
"""E05: fan out `.playtest` files across N built-Player processes.

Selects every glob match whose header carries `# @needs player`
(scripts/playtest_header.py), runs each through a separate Standalone Player
process (bounded by --jobs), and merges the per-file JUnit results into one
combined report. A crashed/non-zero-exit file is never silently dropped — it
surfaces as a failed testcase in the merge so CI cannot miss it.

Player CLI flags mirror unity-player-playtest.yml:148-169. OS-specific extras
(-force-glcore, -nographics) are the caller's job via --extra-arg.
"""
import argparse
import glob
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import playtest_header  # noqa: E402
from gauntlet.player_playtest_evidence import (  # noqa: E402
    PlayerPlaytestEvidenceError,
    _parse_json_receipt,
    _receipt_steps,
    _validate_junit,
)

_MERGED_SUITE_NAME = "UnityMCP.PlayerPlaytestFanOut"
_STDERR_TAIL_CHARS = 2000
_DEFAULT_PLAYER_TIMEOUT_S = 600


@dataclass
class FileRunResult:
    file: str
    exit_code: int
    json_path: str
    junit_path: str
    stderr_tail: str


@dataclass
class FileReport:
    file: str
    raw_steps: list[str] = field(default_factory=list)
    failed_steps: list[str] = field(default_factory=list)
    crashed: bool = False
    crash_message: str = ""


def select_files(pattern: str) -> list[str]:
    """Every glob match whose header declares `# @needs player` (E05 selection)."""
    selected = []
    for path in sorted(glob.glob(pattern, recursive=True)):
        text = Path(path).read_text(encoding="utf-8")
        if playtest_header.scan(text).needs_player:
            selected.append(path)
    return selected


def build_player_args(
    player: str, playtest_file: str, json_path: str, junit_path: str, extra_args: list[str],
) -> list[str]:
    """Pure argv builder — flags mirror unity-player-playtest.yml:148-169."""
    return [
        player, "-batchmode",
        "-unityMcpPlaytest", playtest_file,
        "-unityMcpPlaytestJson", json_path,
        "-unityMcpPlaytestJunit", junit_path,
        "-unityMcpPlaytestExit",
        *extra_args,
    ]


def _safe_stem(playtest_file: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]", "_", playtest_file)


def run_one_file(
    player: str, playtest_file: str, work_dir: str, extra_args: list[str],
    timeout: float = _DEFAULT_PLAYER_TIMEOUT_S,
) -> FileRunResult:
    """Module-level (picklable) worker body — required for ProcessPoolExecutor.

    A hung Player process must never block the fan-out forever: subprocess.run
    always carries a timeout, and TimeoutExpired is caught and turned into a
    crashed FileRunResult (exit_code=-1) rather than propagating out of the
    worker (which would otherwise wedge run_all's future.result()).
    """
    stem = _safe_stem(playtest_file)
    json_path = str(Path(work_dir) / f"{stem}.json")
    junit_path = str(Path(work_dir) / f"{stem}.xml")
    argv = build_player_args(player, playtest_file, json_path, junit_path, extra_args)
    try:
        proc = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8", timeout=timeout)
    except subprocess.TimeoutExpired:
        return FileRunResult(
            file=playtest_file, exit_code=-1, json_path=json_path, junit_path=junit_path,
            stderr_tail=f"TIMEOUT after {timeout}s: Player process did not exit",
        )
    stderr_tail = (proc.stderr or "")[-_STDERR_TAIL_CHARS:]
    return FileRunResult(
        file=playtest_file, exit_code=proc.returncode,
        json_path=json_path, junit_path=junit_path, stderr_tail=stderr_tail,
    )


def run_all(
    files: list[str], player: str, jobs: int, extra_args: list[str], work_dir: str,
    executor_cls=ProcessPoolExecutor, timeout: float = _DEFAULT_PLAYER_TIMEOUT_S,
) -> list[FileRunResult]:
    """Prod uses ProcessPoolExecutor (real parallelism, bounded by `jobs`);
    tests inject ThreadPoolExecutor so monkeypatched subprocess.run applies."""
    with executor_cls(max_workers=jobs) as pool:
        futures = [pool.submit(run_one_file, player, f, work_dir, extra_args, timeout) for f in files]
        return [future.result() for future in futures]


def collect_result(result: FileRunResult) -> FileReport:
    """Reuses gauntlet.player_playtest_evidence's per-receipt validators (no
    re-implemented JSON/JUnit parsing) — never returns None: a crash is always
    a FileReport with crashed=True, never a silently dropped entry."""
    try:
        json_bytes = Path(result.json_path).read_bytes()
        junit_bytes = Path(result.junit_path).read_bytes()
    except OSError as exc:
        return FileReport(
            file=result.file, crashed=True,
            crash_message=(
                f"exit={result.exit_code}: receipts missing ({exc}); "
                f"stderr: {result.stderr_tail}"
            ),
        )
    try:
        payload = _parse_json_receipt(json_bytes, Path(result.json_path).name)
        raw_steps, failed_steps = _receipt_steps(payload, result.file)
        _validate_junit(junit_bytes, raw_steps, failed_steps)
    except PlayerPlaytestEvidenceError as exc:
        return FileReport(file=result.file, crashed=True, crash_message=f"receipt validation failed: {exc}")
    if result.exit_code != 0:
        return FileReport(
            file=result.file, raw_steps=raw_steps, failed_steps=failed_steps, crashed=True,
            crash_message=f"player exited {result.exit_code} after producing receipts",
        )
    return FileReport(file=result.file, raw_steps=raw_steps, failed_steps=failed_steps)


def merge_reports(reports: list[FileReport]) -> ET.Element:
    """One combined <testsuite> — every input file contributes at least one
    testcase, even a crashed one with zero recovered steps (INV: never dropped)."""
    suite = ET.Element("testsuite", name=_MERGED_SUITE_NAME)
    total = 0
    failures = 0
    for report in reports:
        if not report.raw_steps:
            total += 1
            case = ET.SubElement(suite, "testcase", classname=report.file, name="<player process>")
            if report.crashed:
                failures += 1
                ET.SubElement(case, "failure", message=report.crash_message)
            continue
        for step in report.raw_steps:
            total += 1
            case = ET.SubElement(suite, "testcase", classname=report.file, name=step)
            if step in report.failed_steps:
                failures += 1
                ET.SubElement(case, "failure", message="step failed")
        if report.crashed:
            total += 1
            failures += 1
            case = ET.SubElement(suite, "testcase", classname=report.file, name="<player exit code>")
            ET.SubElement(case, "failure", message=report.crash_message)
    suite.set("tests", str(total))
    suite.set("failures", str(failures))
    return suite


def _parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, help="path to the built Standalone Player executable")
    parser.add_argument("--jobs", type=int, default=4, help="parallel Player processes")
    parser.add_argument("--out", default="artifacts/player-playtests-merged.xml")
    parser.add_argument(
        "--extra-arg", action="append", default=[], dest="extra_args",
        help="extra Player CLI arg, repeatable; dash-prefixed values need "
        "'=' form, e.g. --extra-arg=-force-glcore (see argparse dash-prefix rejection)",
    )
    parser.add_argument(
        "--timeout", type=float, default=_DEFAULT_PLAYER_TIMEOUT_S,
        help=f"per-file subprocess timeout in seconds (default {_DEFAULT_PLAYER_TIMEOUT_S})",
    )
    parser.add_argument("glob", help="glob pattern for .playtest files (recursive ** supported)")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)
    files = select_files(args.glob)
    if not files:
        print(f"no '# @needs player' files matched: {args.glob}", file=sys.stderr)
        return 1

    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    work_dir = str(out_path.parent)

    results = run_all(files, args.player, args.jobs, args.extra_args, work_dir, timeout=args.timeout)
    reports = [collect_result(result) for result in results]
    suite = merge_reports(reports)
    ET.ElementTree(suite).write(out_path, encoding="utf-8", xml_declaration=True)

    failed = [r for r in reports if r.crashed or r.failed_steps]
    print(f"PLAYER FANOUT: {len(files) - len(failed)}/{len(files)} files clean -> {out_path}")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
