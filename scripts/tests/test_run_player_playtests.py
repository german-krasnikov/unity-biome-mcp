"""E05: scripts/run_player_playtests.py — N-process Player playtest fan-out.

Every subprocess dispatch is mocked here (`subprocess.run` is monkeypatched to
write canned receipt files instead of touching a real Player build). The
concurrency primitive (ProcessPoolExecutor) is swapped for ThreadPoolExecutor
under test via the injectable `executor_cls` param — same-process threads let
monkeypatch reach the "worker" code; production still uses real OS processes.
"""
import json
import sys
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "scripts"))

import run_player_playtests as rpp  # noqa: E402


def _write_json(path: Path, steps: list[str], failed: set[str]) -> None:
    payload = {
        "schema_version": 1,
        "passed": len(steps) - len(failed),
        "failed": len(failed),
        "duration_seconds": 0.1,
        "steps": [
            {"raw": raw, "passed": raw not in failed, "message": "ok"}
            for raw in steps
        ],
    }
    path.write_text(json.dumps(payload), encoding="utf-8")


def _write_junit(path: Path, steps: list[str], failed: set[str]) -> None:
    root = ET.Element(
        "testsuite",
        {"name": "UnityMCP.PlayerPlaytest", "tests": str(len(steps)), "failures": str(len(failed))},
    )
    for raw in steps:
        case = ET.SubElement(root, "testcase", {"name": raw})
        if raw in failed:
            ET.SubElement(case, "failure", {"message": "failed"})
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


def _write_receipt_pair(json_path: Path, junit_path: Path, steps: list[str], failed: set[str]) -> None:
    _write_json(json_path, steps, failed)
    _write_junit(junit_path, steps, failed)


# ===========================================================================
# Group A: file selection honors `# @needs player`
# ===========================================================================

def test_select_files_against_real_streaming_assets_glob_matches_e06_fixtures():
    """E06: pins exactly which shipped fixtures the CI fan-out step selects —
    the 3 previously CI-uncovered Player fixtures (bounds/multi_move/reset),
    not the 3 already individually scripted in unity-player-playtest.yml
    (smoke/expected_failure/graphics_smoke stay untagged to avoid redundant
    Player invocations of files already validated with precise assertions)."""
    pattern = str(REPO_ROOT / "unity-test-project/Assets/StreamingAssets/Playtests/*.playtest")

    selected = {Path(p).name for p in rpp.select_files(pattern)}

    assert selected == {
        "player_ci_bounds.playtest",
        "player_ci_multi_move.playtest",
        "player_ci_reset.playtest",
    }


def test_select_files_filters_by_needs_player(tmp_path):
    (tmp_path / "a.playtest").write_text("# @needs player\nLOG a\n", encoding="utf-8")
    (tmp_path / "b.playtest").write_text("# @needs editmode\nLOG b\n", encoding="utf-8")
    (tmp_path / "c.playtest").write_text("LOG c\n", encoding="utf-8")

    selected = rpp.select_files(str(tmp_path / "*.playtest"))

    assert selected == [str(tmp_path / "a.playtest")]


# ===========================================================================
# Group B: pure argv builder
# ===========================================================================

def test_build_player_args_shape():
    argv = rpp.build_player_args(
        "/path/Player", "Assets/x.playtest", "out/x.json", "out/x.xml", ["-force-glcore"],
    )

    assert argv == [
        "/path/Player", "-batchmode",
        "-unityMcpPlaytest", "Assets/x.playtest",
        "-unityMcpPlaytestJson", "out/x.json",
        "-unityMcpPlaytestJunit", "out/x.xml",
        "-unityMcpPlaytestExit",
        "-force-glcore",
    ]


# ===========================================================================
# Group C: N files -> N subprocess dispatches, bounded by --jobs
# ===========================================================================

def test_run_all_dispatches_one_subprocess_per_file(tmp_path, monkeypatch):
    calls = []

    class _FakeCompleted:
        returncode = 0
        stderr = ""

    def _fake_run(argv, **kwargs):
        calls.append(argv)
        json_path = Path(argv[argv.index("-unityMcpPlaytestJson") + 1])
        junit_path = Path(argv[argv.index("-unityMcpPlaytestJunit") + 1])
        _write_receipt_pair(json_path, junit_path, ["LOG hi"], set())
        return _FakeCompleted()

    monkeypatch.setattr(rpp.subprocess, "run", _fake_run)
    files = [str(tmp_path / f"f{i}.playtest") for i in range(3)]

    results = rpp.run_all(files, "/Player", 2, [], str(tmp_path), executor_cls=ThreadPoolExecutor)

    assert len(calls) == 3
    assert len(results) == 3
    assert {r.file for r in results} == set(files)


def test_run_all_passes_jobs_as_max_workers(tmp_path, monkeypatch):
    captured = {}

    class _SpyExecutor(ThreadPoolExecutor):
        def __init__(self, *args, **kwargs):
            captured["max_workers"] = kwargs.get("max_workers")
            super().__init__(*args, **kwargs)

    def _fake_run(argv, **kwargs):
        json_path = Path(argv[argv.index("-unityMcpPlaytestJson") + 1])
        junit_path = Path(argv[argv.index("-unityMcpPlaytestJunit") + 1])
        _write_receipt_pair(json_path, junit_path, ["LOG hi"], set())

        class _Completed:
            returncode = 0
            stderr = ""
        return _Completed()

    monkeypatch.setattr(rpp.subprocess, "run", _fake_run)
    files = [str(tmp_path / "f.playtest")]

    rpp.run_all(files, "/Player", 7, [], str(tmp_path), executor_cls=_SpyExecutor)

    assert captured["max_workers"] == 7


def test_subprocess_timeout_surfaces_as_failure(tmp_path, monkeypatch):
    """Double-red target: a hung Player process must not block the fan-out
    forever. subprocess.run must be called with a timeout=, and a
    TimeoutExpired must be caught and turned into a crashed FileReport
    (never raised out of run_all/collect_result)."""
    def _fake_run(argv, **kwargs):
        assert kwargs.get("timeout") == rpp._DEFAULT_PLAYER_TIMEOUT_S
        raise rpp.subprocess.TimeoutExpired(cmd=argv, timeout=kwargs["timeout"])

    monkeypatch.setattr(rpp.subprocess, "run", _fake_run)
    files = [str(tmp_path / "hung.playtest")]

    results = rpp.run_all(files, "/Player", 1, [], str(tmp_path), executor_cls=ThreadPoolExecutor)

    assert len(results) == 1
    assert results[0].exit_code == -1
    assert "TIMEOUT" in results[0].stderr_tail

    report = rpp.collect_result(results[0])
    assert report.crashed is True


# ===========================================================================
# Group D: receipt validation reuses gauntlet.player_playtest_evidence
# ===========================================================================

def test_collect_result_success_returns_raw_and_failed_steps(tmp_path):
    json_path = tmp_path / "f.json"
    junit_path = tmp_path / "f.xml"
    _write_receipt_pair(json_path, junit_path, ["LOG a", "ASSERT b"], {"ASSERT b"})
    result = rpp.FileRunResult(
        file="f.playtest", exit_code=0,
        json_path=str(json_path), junit_path=str(junit_path), stderr_tail="",
    )

    report = rpp.collect_result(result)

    assert report.crashed is False
    assert report.raw_steps == ["LOG a", "ASSERT b"]
    assert report.failed_steps == ["ASSERT b"]


def test_collect_result_missing_receipts_is_crashed_not_dropped(tmp_path):
    """Double-red target: a non-zero exit with no receipts on disk must surface
    as a crashed report, never silently vanish from the result set."""
    result = rpp.FileRunResult(
        file="crashed.playtest", exit_code=134,
        json_path=str(tmp_path / "missing.json"), junit_path=str(tmp_path / "missing.xml"),
        stderr_tail="Segmentation fault",
    )

    report = rpp.collect_result(result)

    assert report.crashed is True
    assert "134" in report.crash_message
    assert "Segmentation fault" in report.crash_message


# ===========================================================================
# Group E: merge — crashed files are present in the combined report
# ===========================================================================

def test_merge_reports_includes_crashed_file_as_a_failure_not_dropped():
    clean = rpp.FileReport(file="ok.playtest", raw_steps=["LOG a"], failed_steps=[], crashed=False)
    crashed = rpp.FileReport(
        file="crashed.playtest", raw_steps=[], failed_steps=[], crashed=True,
        crash_message="exit=134: receipts missing",
    )

    suite = rpp.merge_reports([clean, crashed])

    classnames = {case.get("classname") for case in suite.findall("testcase")}
    assert classnames == {"ok.playtest", "crashed.playtest"}
    assert suite.get("tests") == "2"
    assert suite.get("failures") == "1"
    crashed_case = next(c for c in suite.findall("testcase") if c.get("classname") == "crashed.playtest")
    assert crashed_case.find("failure") is not None
