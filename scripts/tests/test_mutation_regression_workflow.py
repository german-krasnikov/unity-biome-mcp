"""Structural guards on `.github/workflows/mutation-regression.yml` --
mirrors test_fsr_qualification_workflow.py's `_text()`/`_parsed()`
convention. This workflow supersedes fsr-qualification.yml as the ongoing
regression lane for the Source Patch provider integration (see that file's
header comment and Plans/MUTATION-REGRESSION-MODULE.md section 7).

Runs in the standard `scripts/tests` lane: no Unity, no network, reads the
tracked workflow file only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "mutation-regression.yml"


def _text() -> str:
    return WORKFLOW_PATH.read_text(encoding="utf-8")


def _parsed() -> dict:
    return yaml.safe_load(_text())


def test_workflow_parses_as_valid_yaml() -> None:
    assert _parsed()["name"] == "Mutation Regression"


def test_workflow_triggers_only_on_dispatch_and_weekly_schedule() -> None:
    data = _parsed()
    triggers = data[True] if True in data else data["on"]
    assert set(triggers.keys()) == {"workflow_dispatch", "schedule"}
    assert triggers["schedule"] == [{"cron": "0 4 * * 1"}]


def test_workflow_never_triggers_on_push() -> None:
    data = _parsed()
    triggers = data[True] if True in data else data["on"]
    assert "push" not in triggers


def test_workflow_defines_resolve_cell_and_aggregate_jobs() -> None:
    data = _parsed()
    assert set(data["jobs"].keys()) == {"resolve", "cell", "aggregate"}


def test_workflow_never_cancels_in_progress_or_fails_fast() -> None:
    data = _parsed()
    assert data["concurrency"]["cancel-in-progress"] is False
    assert data["jobs"]["cell"]["strategy"]["fail-fast"] is False


def test_workflow_permissions_are_read_only() -> None:
    data = _parsed()
    assert data["permissions"] == {"contents": "read"}


def test_workflow_cell_job_matrix_has_exactly_the_two_required_cells() -> None:
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    assert sorted(entry["cell"] for entry in cells) == ["min-linux-x64", "min-macos-arm64"]


def test_workflow_resolve_job_runs_provider_ref_and_uploads_resolved_pin() -> None:
    text = _text()
    resolve_index = text.index("\n  resolve:")
    cell_index = text.index("\n  cell:")
    block = text[resolve_index:cell_index]
    assert "scripts/gauntlet/provider_ref.py" in block
    assert "--pin scripts/source_patch_provider_pin.json" in block
    assert "--out resolved-pin.json" in block
    assert "actions/upload-artifact" in block
    assert "name: resolved-pin" in block


def test_workflow_cell_job_runs_the_cell_driver_in_full_mode() -> None:
    text = _text()
    assert "run_mutation_regression_cell.py" in text
    assert "--mode full" in text
    assert "--port 9610" in text
    assert "--provider-resolved resolved-pin.json" in text


def test_workflow_cell_job_reuses_pinned_actions_from_fsr_qualification() -> None:
    text = _text()
    for pinned in (
        "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1",
        "actions/setup-python@5fda3b95a4ea91299a34e894583c3862153e4b97",
        "actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9",
        "buildalon/unity-setup@30fcbcb56c10ea5d64298e970d952b8d29bc268b",
        "buildalon/activate-unity-license@e0d245d0787b7b9931b56ccbde3b508f6b70f1af",
        "actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
        "actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c",
    ):
        assert pinned in text, pinned


def test_workflow_cell_job_owns_and_tears_down_xvfb_on_linux_only() -> None:
    text = _text()
    assert "Xvfb :99" in text
    assert "runner.os == 'Linux'" in text
    assert "Stop owned Xvfb" in text
    assert "kill \"${{ steps.xvfb.outputs.xvfb_pid }}\"" in text


def test_workflow_uploads_the_receipt_even_on_failure() -> None:
    text = _text()
    upload_index = text.index("Upload cell receipt")
    block = text[upload_index : upload_index + 300]
    assert "if: always()" in block
    assert "actions/upload-artifact" in block
    assert "mutation-regression-${{ matrix.cell }}" in block


def test_workflow_aggregate_job_needs_cell_and_validates_receipts() -> None:
    data = _parsed()
    aggregate = data["jobs"]["aggregate"]
    assert aggregate["needs"] == ["cell"]
    text = _text()
    aggregate_index = text.index("\n  aggregate:")
    block = text[aggregate_index:]
    assert "validate_mutation_regression_receipts.py" in block
    assert "--receipts-root receipts" in block
    assert "if: always()" in block


def test_workflow_unity_version_resolved_from_fsr_lock_u_min_window() -> None:
    text = _text()
    assert 'lock["cells"]["u_min"]' in text


def test_workflow_pins_linux_hub_version_matching_working_ci_lanes() -> None:
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    for entry in cells:
        if entry["runner"].startswith("ubuntu-"):
            assert entry["hub-version"] == "3.19.5", entry["cell"]
        else:
            assert entry["hub-version"] == "", entry["cell"]
