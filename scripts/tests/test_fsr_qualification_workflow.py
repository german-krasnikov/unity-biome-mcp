"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: structural
guards on `.github/workflows/fsr-qualification.yml`, mirroring
`test_ci_release_workflows.py`'s `_workflow(name)` text-assertion
convention.

Runs in the standard `scripts/tests` lane: no Unity, no network, reads the
tracked workflow file only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "fsr-qualification.yml"


def _text() -> str:
    return WORKFLOW_PATH.read_text(encoding="utf-8")


def _parsed() -> dict:
    return yaml.safe_load(_text())


def test_workflow_triggers_only_on_workflow_dispatch():
    """Advisory only — never push/PR triggered (§7 P1-20 DoD)."""
    data = _parsed()
    triggers = data[True] if True in data else data["on"]
    assert list(triggers.keys()) == ["workflow_dispatch"]


def test_workflow_defines_exactly_six_frozen_cells():
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    names = sorted(entry["cell"] for entry in cells)
    assert names == [
        "max-linux-x64",
        "max-macos-arm64",
        "max-windows-x64",
        "min-linux-x64",
        "min-macos-arm64",
        "min-windows-x64",
    ]


def test_workflow_pairs_each_os_with_both_windows():
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    by_runner = {}
    for entry in cells:
        by_runner.setdefault(entry["runner"], set()).add(entry["window"])
    assert by_runner == {
        "macos-15": {"u_min", "u_max"},
        "windows-2022": {"u_min", "u_max"},
        "ubuntu-24.04": {"u_min", "u_max"},
    }


def test_workflow_never_uses_fail_fast_or_cancels_in_progress():
    data = _parsed()
    assert data["jobs"]["cell"]["strategy"]["fail-fast"] is False
    assert data["concurrency"]["cancel-in-progress"] is False


def test_workflow_launches_unity_headed_not_batchmode():
    """A batchmode lane is not FSR qualification evidence (§7 P1-20) — the
    cell driver must be invoked, and nothing in this workflow may pass
    -batchmode/-nographics to Unity directly."""
    text = _text()
    assert "run_fsr_qualification_cell.py" in text
    code_lines = "\n".join(
        line for line in text.splitlines() if not line.strip().startswith("#")
    )
    assert "-batchmode" not in code_lines
    assert "-nographics" not in code_lines
    assert "UNITY_MCP_ENABLE_BATCHMODE" not in code_lines


def test_workflow_runs_fixture_free_pilot_before_semantic_cell():
    text = _text()
    pilot_index = text.index("--mode pilot")
    full_index = text.index("--mode full")
    assert pilot_index < full_index


def test_workflow_owns_and_tears_down_xvfb_on_linux_only():
    text = _text()
    assert "Xvfb :99" in text
    assert "runner.os == 'Linux'" in text
    assert "Stop owned Xvfb" in text
    assert "kill \"${{ steps.xvfb.outputs.xvfb_pid }}\"" in text


def test_workflow_uploads_evidence_even_on_failure():
    text = _text()
    upload_index = text.index("Upload cell evidence")
    block = text[upload_index : upload_index + 300]
    assert "if: always()" in block
    assert "actions/upload-artifact" in block
    assert "Library" not in block


def test_workflow_reuses_pinned_actions_from_existing_ci():
    """Same pinned action refs already proven by unity-tests.yml/
    ci-conformance.yml — no new supply-chain surface for this workflow."""
    text = _text()
    for pinned in (
        "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1",
        "actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9",
        "buildalon/unity-setup@30fcbcb56c10ea5d64298e970d952b8d29bc268b",
        "buildalon/activate-unity-license@e0d245d0787b7b9931b56ccbde3b508f6b70f1af",
        "actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
        "actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c",
    ):
        assert pinned in text


def test_workflow_aggregate_job_needs_cell_and_validates_receipts():
    data = _parsed()
    aggregate = data["jobs"]["aggregate"]
    assert aggregate["needs"] == ["cell"]
    text = _text()
    aggregate_index = text.index("aggregate:")
    block = text[aggregate_index:]
    assert "validate_fsr_qualification_receipts.py" in block
    assert "--receipts-root receipts" in block


def test_workflow_license_secrets_reference_existing_repo_secrets():
    text = _text()
    assert "secrets.UNITY_EMAIL" in text
    assert "secrets.UNITY_PASSWORD" in text
