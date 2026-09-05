"""test_timeline.py — top-N slowest-fixture duration reporter (A08).

Parses NUnit3 XML test-run artifacts written by TestRunStore (see
unity-plugin/Editor/TestRuns/TestRunStore.cs:56-78) and reports which
fixtures consumed the most wall-clock time, summed from <test-case> duration
attributes. No Unity/TCP dependency — reads artifacts already on disk.

parse_nunit_case_durations() is the single XML walker (A09 reuses it per the
plan instead of hand-rolling a second one); parse_nunit_durations() is a
fixture-level view built on top of it.
"""

import argparse
import json
import statistics
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import NamedTuple

DEFAULT_TOP_N = 20
EXIT_INPUT_ERROR = 2  # missing/malformed input; never a bare traceback
REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_PROJECT = REPO_ROOT / "unity-test-project"  # mirrors run_unity_tests.py:30


class CaseDuration(NamedTuple):
    fixture: str
    name: str
    duration_s: float


def parse_nunit_case_durations(xml_text: str) -> list[CaseDuration]:
    """Parse every <test-case> element into (fixture, name, duration_s).

    fixture = the NUnit `classname` attribute (the TestFixture); name = the
    full test name (`fullname`). This is the one XML walker other tooling
    (A09's collect_test_results.py) should import and reuse.
    """
    root = ET.fromstring(xml_text)
    return [
        CaseDuration(
            fixture=tc.get("classname", ""),
            name=tc.get("fullname", ""),
            duration_s=float(tc.get("duration") or 0.0),
        )
        for tc in root.iter("test-case")
    ]


def parse_nunit_durations(xml_text: str) -> list[tuple[str, float]]:
    """Fixture-level view: sum each fixture's <test-case> durations."""
    totals: dict[str, float] = {}
    for case in parse_nunit_case_durations(xml_text):
        totals[case.fixture] = totals.get(case.fixture, 0.0) + case.duration_s
    return list(totals.items())


def top_n(rows: list[tuple[str, float]], n: int = DEFAULT_TOP_N) -> list[tuple[str, float]]:
    """Sort rows descending by duration, truncated to the top n."""
    return sorted(rows, key=lambda row: row[1], reverse=True)[:n]


def median_base_setup_ms(values: list[float]) -> float:
    """Median of per-fixture base-setup overhead (ms).

    Pure function over synthetic/caller-supplied data — the real field lands
    with plan item A31; this keeps the statistic itself testable now.
    """
    return statistics.median(values)


def _is_full_unfiltered_editmode(summary: dict) -> bool:
    """True if a run's summary.json describes a full, unfiltered EditMode run.

    Fields per unity-plugin/Editor/TestRuns/TestRunProtocol.cs:276-278
    (TestRunSummary.mode/group/filter).
    """
    return (
        summary.get("mode") == "EditMode"
        and not summary.get("filter")
        and not summary.get("group")
    )


def _resolve_latest_full_run_xml(project: Path) -> Path:
    """Find the newest full-EditMode run under <project>/Library/UnityMCP/TestRuns/runs
    (unity-plugin/Editor/TestRuns/TestRunStore.cs:37-78) and return its utf-results.xml path.

    Ties on mtime (a same-second CI write is plausible) are broken by the run
    directory name, lexically largest wins -- deterministic regardless of the
    order the filesystem's iterdir() happens to yield.
    """
    runs_root = project / "Library" / "UnityMCP" / "TestRuns" / "runs"
    candidates: list[tuple[float, str, Path]] = []
    for run_dir in runs_root.iterdir() if runs_root.is_dir() else []:
        summary_path = run_dir / "summary.json"
        if not summary_path.is_file():
            continue
        try:
            summary = json.loads(summary_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if not _is_full_unfiltered_editmode(summary):
            continue
        candidates.append((summary_path.stat().st_mtime, run_dir.name, run_dir))
    if not candidates:
        raise FileNotFoundError(f"no full unfiltered EditMode run found under {runs_root}")
    _, _, newest_run_dir = max(candidates, key=lambda candidate: candidate[:2])
    return newest_run_dir / "utf-results.xml"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Report the top-N slowest test fixtures from an NUnit XML test-run artifact."
    )
    parser.add_argument("--nunit-xml", type=Path, help="Path to a utf-results.xml artifact")
    parser.add_argument(
        "--latest-full", action="store_true",
        help="Resolve the newest full unfiltered EditMode run under --project",
    )
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--top", type=int, default=DEFAULT_TOP_N)
    parser.add_argument(
        "--by", choices=("fixture", "case"), default="fixture",
        help="Aggregate by fixture (classname, default) or list raw test cases",
    )
    args = parser.parse_args(argv)

    try:
        if args.nunit_xml:
            xml_path = args.nunit_xml
        elif args.latest_full:
            xml_path = _resolve_latest_full_run_xml(args.project)
        else:
            parser.error("one of --nunit-xml or --latest-full is required")
            return EXIT_INPUT_ERROR  # unreachable: parser.error() exits

        xml_text = xml_path.read_text(encoding="utf-8")
        if args.by == "case":
            rows = [(case.name, case.duration_s) for case in parse_nunit_case_durations(xml_text)]
            label = "test_case"
        else:
            rows = parse_nunit_durations(xml_text)
            label = "fixture"
    except (OSError, ET.ParseError) as error:
        # Missing/malformed input is a clean one-line stderr message, never a
        # bare traceback (missing file -> OSError incl. FileNotFoundError;
        # --latest-full with nothing matching -> FileNotFoundError; malformed
        # XML -> ET.ParseError).
        # Use getattr for .filename (OSError) to avoid repr()-doubled backslashes
        # on Windows that break cross-platform test assertions.
        path_hint = getattr(error, "filename", None)
        print(f"error: {path_hint or error}", file=sys.stderr)
        return EXIT_INPUT_ERROR

    print(f"{'duration_s':>12}  {label}")
    for name, duration in top_n(rows, args.top):
        print(f"{duration:12.3f}  {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
