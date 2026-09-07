"""Aggregate gate for the mutation regression lane -- sibling to
validate_fsr_qualification_receipts.py, but built for a floating-master
provider pin instead of a frozen base SHA. Deliberately does NOT reuse
fq.assert_base_sha_untouched (see Plans/MUTATION-REGRESSION-MODULE.md
section 7 "Not reused from FSR cell").

Runs in the standard `scripts/tests` lane: no Unity, no network, no git --
every receipt is a synthetic fixture written under tmp_path.
"""
import json
import shutil
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS.parent))
import validate_mutation_regression_receipts as validator  # noqa: E402

TRACKED_REF = "master"
REQUIRED_CELLS = ("min-macos-arm64", "min-linux-x64")


def _receipt(*, provider_overrides: dict[str, object] | None = None, **overrides: object) -> dict[str, object]:
    provider = {
        "package": "com.handzlikchris.fastscriptreload",
        "requested_ref": TRACKED_REF,
        "resolved_sha": "b" * 40,
        "upm_lock_hash": "deadbeef",
    }
    if provider_overrides:
        provider.update(provider_overrides)
    receipt: dict[str, object] = {
        "schema_version": 1,
        "checkout_sha": "a" * 40,
        "provider": provider,
        "unity_version": "6000.0.65f1",
        "port": 9610,
        "mode": "full",
        "outcome": "PASS",
        "python_lane": {"passed": 10, "failed": 0, "skipped": 0, "exit_code": 0},
        "csharp_lane": {"passed": 3, "failed": 0, "expected_count": 3, "run_id": "run-1", "exit_code": 0},
        "timeline": [],
        "failed_phase": None,
    }
    receipt.update(overrides)
    return receipt


def _write_receipts(tmp_path: Path, per_cell: dict[str, dict[str, object]] | None = None) -> Path:
    receipts_root = tmp_path / "receipts"
    per_cell = per_cell or {}
    for cell in REQUIRED_CELLS:
        receipt = per_cell.get(cell, _receipt())
        cell_dir = receipts_root / f"mutation-regression-{cell}"
        cell_dir.mkdir(parents=True)
        (cell_dir / "receipt.json").write_text(json.dumps(receipt), encoding="utf-8")
    return receipts_root


def _write_pin(tmp_path: Path, ref: str = TRACKED_REF) -> Path:
    pin_path = tmp_path / "pin.json"
    pin_path.write_text(
        json.dumps({"package_name": "com.handzlikchris.fastscriptreload", "ref": ref}), encoding="utf-8"
    )
    return pin_path


def test_main_returns_zero_on_clean_pass_set(tmp_path: Path) -> None:
    receipts_root = _write_receipts(tmp_path)
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) == 0


def test_main_returns_nonzero_on_malformed_receipt_json(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    receipts_root = _write_receipts(tmp_path)
    malformed_path = receipts_root / "mutation-regression-min-macos-arm64" / "receipt.json"
    malformed_path.write_text("{not valid json", encoding="utf-8")
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) != 0
    assert "Malformed JSON" in capsys.readouterr().err


def test_main_returns_nonzero_on_missing_required_cell(tmp_path: Path) -> None:
    receipts_root = _write_receipts(tmp_path)
    shutil.rmtree(receipts_root / "mutation-regression-min-linux-x64")
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) != 0


@pytest.mark.parametrize(
    "bad_receipt",
    [
        _receipt(outcome="FAIL"),
        _receipt(python_lane={"passed": 5, "failed": 1, "skipped": 0, "exit_code": 1}),
        _receipt(python_lane={"passed": 0, "failed": 0, "skipped": 0, "exit_code": 0}),
        _receipt(csharp_lane={"passed": 0, "failed": 1, "expected_count": 3, "run_id": "r", "exit_code": 1}),
        _receipt(csharp_lane={"passed": 3, "failed": 0, "expected_count": 0, "run_id": "r", "exit_code": 0}),
        _receipt(provider_overrides={"requested_ref": "some-other-branch"}),
        _receipt(provider_overrides={"lock_sha_mismatch": "c" * 40}),
    ],
    ids=[
        "fail-outcome",
        "python-lane-failed",
        "python-lane-passed-zero",
        "csharp-lane-failed",
        "csharp-expected-count-zero",
        "requested-ref-mismatch",
        "lock-drift-without-flag",
    ],
)
def test_main_returns_nonzero_on_bad_required_cell_receipt(tmp_path: Path, bad_receipt: dict[str, object]) -> None:
    receipts_root = _write_receipts(tmp_path, {"min-macos-arm64": bad_receipt})
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) != 0


def test_main_returns_nonzero_on_mixed_checkout_sha(tmp_path: Path) -> None:
    receipts_root = _write_receipts(tmp_path, {"min-macos-arm64": _receipt(checkout_sha="c" * 40)})
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) != 0


def test_main_returns_nonzero_on_mixed_resolved_sha(tmp_path: Path) -> None:
    receipts_root = _write_receipts(
        tmp_path, {"min-macos-arm64": _receipt(provider_overrides={"resolved_sha": "c" * 40})}
    )
    pin_path = _write_pin(tmp_path)
    assert validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)]) != 0


def test_main_returns_zero_on_lock_drift_with_allow_flag(tmp_path: Path) -> None:
    receipts_root = _write_receipts(
        tmp_path, {"min-macos-arm64": _receipt(provider_overrides={"lock_sha_mismatch": "c" * 40})}
    )
    pin_path = _write_pin(tmp_path)
    assert (
        validator.main(
            ["--receipts-root", str(receipts_root), "--pin", str(pin_path), "--allow-lock-drift"]
        )
        == 0
    )


def test_main_prints_compact_table_with_cell_names(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    receipts_root = _write_receipts(tmp_path)
    pin_path = _write_pin(tmp_path)
    validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)])
    out = capsys.readouterr().out
    assert "min-macos-arm64" in out
    assert "min-linux-x64" in out


def test_main_prints_clear_message_on_missing_cell(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    receipts_root = _write_receipts(tmp_path)
    shutil.rmtree(receipts_root / "mutation-regression-min-linux-x64")
    pin_path = _write_pin(tmp_path)
    validator.main(["--receipts-root", str(receipts_root), "--pin", str(pin_path)])
    err = capsys.readouterr().err
    assert "min-linux-x64" in err


def test_cell_name_strips_known_artifact_prefix() -> None:
    assert validator._cell_name("mutation-regression-min-macos-arm64") == "min-macos-arm64"


def test_cell_name_passes_through_unprefixed_directory_name() -> None:
    assert validator._cell_name("min-macos-arm64") == "min-macos-arm64"


def test_load_tracked_ref_reads_ref_field_from_pin(tmp_path: Path) -> None:
    pin_path = _write_pin(tmp_path, ref="some-branch")
    assert validator.load_tracked_ref(pin_path) == "some-branch"
