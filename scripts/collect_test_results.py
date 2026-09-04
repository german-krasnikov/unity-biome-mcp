"""collect_test_results.py — aggregate test results from multiple sources into one JSON.

Each CI workflow calls this with --add to append a suite, then --finalize to write the JSON.
Format:
  {"suites": [{"name": "...", "passed": N, "failed": N, "skipped": N, "total": N, "platform": "..."}]}

Usage:
  # From pytest junitxml:
  python scripts/collect_test_results.py --add-pytest reports/junit.xml --suite "Python 3.14" --platform linux --out docs/quality/data/tests.json
  # From Unity test results XML:
  python scripts/collect_test_results.py --add-nunit reports/editmode.xml --suite "C# EditMode (Linux)" --platform linux --out docs/quality/data/tests.json
  # Manual entry:
  python scripts/collect_test_results.py --add-manual --suite "Python 3.14" --passed 4886 --failed 0 --skipped 3 --platform linux --out docs/quality/data/tests.json
"""

import argparse
import json
import pathlib
import xml.etree.ElementTree as ET

from test_timeline import parse_nunit_case_durations


def parse_pytest_junit(path: pathlib.Path) -> dict:
    """Parse pytest junitxml → {passed, failed, skipped, total, duration}."""
    tree = ET.parse(path)
    root = tree.getroot()
    ts = root.find("testsuite") if root.tag != "testsuite" else root
    if ts is None:
        return {"passed": 0, "failed": 0, "skipped": 0, "total": 0, "duration": 0.0}
    tests = int(ts.get("tests", 0))
    failures = int(ts.get("failures", 0))
    errors = int(ts.get("errors", 0))
    skipped = int(ts.get("skipped", 0))
    return {
        "passed": tests - failures - errors - skipped,
        "failed": failures + errors,
        "skipped": skipped,
        "total": tests,
        "duration": float(ts.get("time", 0.0)),
    }


def parse_nunit(path: pathlib.Path) -> dict:
    """Parse NUnit XML → {passed, failed, skipped, total, duration}.

    duration sums each <test-case>'s `duration` attribute via
    test_timeline.parse_nunit_case_durations -- the single reused XML walker,
    not a second hand-rolled one.
    """
    xml_text = path.read_text(encoding="utf-8")
    root = ET.fromstring(xml_text)
    duration = sum(case.duration_s for case in parse_nunit_case_durations(xml_text))
    return {
        "passed": int(root.get("passed", 0)),
        "failed": int(root.get("failed", 0)),
        "skipped": int(root.get("skipped", 0)) + int(root.get("inconclusive", 0)),
        "total": int(root.get("total", 0)),
        "duration": duration,
    }


def load_results(path: pathlib.Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {"suites": []}


def save_results(path: pathlib.Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--add-pytest", type=pathlib.Path)
    parser.add_argument("--add-nunit", type=pathlib.Path)
    parser.add_argument("--add-manual", action="store_true")
    parser.add_argument("--suite", required=True)
    parser.add_argument("--platform", default="")
    parser.add_argument("--out", type=pathlib.Path, required=True)
    parser.add_argument("--passed", type=int, default=0)
    parser.add_argument("--failed", type=int, default=0)
    parser.add_argument("--skipped", type=int, default=0)
    args = parser.parse_args()

    results = load_results(args.out)

    if args.add_pytest:
        stats = parse_pytest_junit(args.add_pytest)
    elif args.add_nunit:
        stats = parse_nunit(args.add_nunit)
    elif args.add_manual:
        stats = {
            "passed": args.passed,
            "failed": args.failed,
            "skipped": args.skipped,
            "total": args.passed + args.failed + args.skipped,
        }
    else:
        parser.error("One of --add-pytest, --add-nunit, or --add-manual required")
        return

    suite = {
        "name": args.suite,
        "platform": args.platform,
        **stats,
    }

    existing = [s for s in results["suites"] if s.get("name") != args.suite]
    existing.append(suite)
    results["suites"] = existing

    save_results(args.out, results)
    print(f"{args.suite}: {stats['passed']} passed, {stats['failed']} failed, {stats['skipped']} skipped")


if __name__ == "__main__":
    main()
