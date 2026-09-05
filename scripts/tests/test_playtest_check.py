"""B19: scripts/playtest_check.py — validates a Player playtest receipt's
step/failed counts against its .playtest file's @expect header (B18's
scanner). Replaces the two hardcoded `!= 14` / `!= 4` step-count literals
that used to live directly in unity-player-playtest.yml.
"""
import json
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import playtest_check as pc  # noqa: E402


def _write_playtest(tmp_path, header_line):
    path = tmp_path / "fixture.playtest"
    path.write_text(f"{header_line}\nLOG hi\n", encoding="utf-8")
    return path


def _write_receipt(tmp_path, step_oks, failed):
    steps = [{"index": i, "ok": ok} for i, ok in enumerate(step_oks)]
    path = tmp_path / "receipt.json"
    path.write_text(
        json.dumps({"passed": len(steps) - failed, "failed": failed, "steps": steps}),
        encoding="utf-8",
    )
    return path


def test_check_passes_when_steps_match_header(tmp_path):
    playtest = _write_playtest(tmp_path, "# @expect steps=2 failed=0")
    receipt = _write_receipt(tmp_path, [True, True], failed=0)

    result = pc.check(playtest, receipt)

    assert "OK" in result


def test_check_fails_when_step_count_mismatches(tmp_path):
    playtest = _write_playtest(tmp_path, "# @expect steps=2 failed=0")
    receipt = _write_receipt(tmp_path, [True, True, True], failed=0)

    with pytest.raises(ValueError) as exc:
        pc.check(playtest, receipt)

    message = str(exc.value)
    assert playtest.name in message
    assert "2" in message
    assert "3" in message


def test_check_fails_when_failed_count_mismatches(tmp_path):
    playtest = _write_playtest(tmp_path, "# @expect steps=2 failed=0")
    receipt = _write_receipt(tmp_path, [True, False], failed=1)

    with pytest.raises(ValueError) as exc:
        pc.check(playtest, receipt)

    message = str(exc.value)
    assert playtest.name in message
    assert "0" in message
    assert "1" in message


def test_check_no_header_expect_skips_validation(tmp_path):
    playtest = _write_playtest(tmp_path, "# just a comment")
    receipt = _write_receipt(tmp_path, [True], failed=0)

    result = pc.check(playtest, receipt)

    assert "no @expect" in result
    assert "skipped" in result
