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


def test_workflow_triggers_on_workflow_dispatch_and_scoped_push():
    """Advisory (§7 P1-20 DoD): workflow_dispatch is the intended trigger.
    A temporary push trigger exists only because GitHub refuses to
    register workflow_dispatch for a workflow that is absent from the
    default branch, and this repo's mandate keeps master untouched — the
    push trigger is scoped to this exact branch and to only the
    workflow/lock files themselves, so an ordinary product commit on this
    branch never fires the matrix (dispatch stays de-facto manual)."""
    data = _parsed()
    triggers = data[True] if True in data else data["on"]
    assert set(triggers.keys()) == {"workflow_dispatch", "push"}
    assert triggers["push"]["branches"] == ["feature/mutation-fsr-mvp"]
    # Exact paths-list coverage is
    # test_workflow_push_trigger_paths_cover_the_full_cell_mechanization's
    # job — this test only pins the trigger shape (branch-scoped, path
    # filter present) so the two tests fail for one reason each.
    assert ".github/workflows/fsr-qualification.yml" in triggers["push"]["paths"]


def test_workflow_push_trigger_is_documented_as_temporary():
    text = _text()
    assert "temporary trigger" in text
    assert "requires the file on the default" in text
    assert "after the first" in text


def test_workflow_defines_exactly_the_three_narrowed_cells():
    """Narrowed after run 5 (coordinator decision): u_max is shelved to
    P2-07 — only the three u_min cells remain (macOS/Linux required-pass,
    Windows documented-blocked)."""
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    names = sorted(entry["cell"] for entry in cells)
    assert names == [
        "min-linux-x64",
        "min-macos-arm64",
        "min-windows-x64",
    ]


def test_workflow_only_runs_u_min_window():
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    assert {entry["window"] for entry in cells} == {"u_min"}


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


def test_workflow_pins_linux_hub_version_matching_working_ci_lanes():
    """Regression guard: the first matrix run failed 6x
    INFRASTRUCTURE_BLOCKED on Linux with buildalon/unity-setup unable to
    find /opt/unityhub/unityhub. unity-tests.yml/unity-compat.yml/
    ci-conformance.yml all already work around this by pinning
    hub-version: '3.19.5' on Linux only (3.20.0+ regresses that path,
    issue #57) — this workflow must repeat that exact working pattern."""
    data = _parsed()
    cells = data["jobs"]["cell"]["strategy"]["matrix"]["include"]
    for entry in cells:
        if entry["runner"].startswith("ubuntu-"):
            assert entry["hub-version"] == "3.19.5", entry["cell"]
        else:
            assert entry["hub-version"] == "", entry["cell"]
    unity_setup = next(
        step for step in data["jobs"]["cell"]["steps"] if step.get("id") == "unity-setup"
    )
    assert unity_setup["with"]["hub-version"] == "${{ matrix.hub-version }}"


def test_workflow_push_trigger_paths_cover_the_full_cell_mechanization():
    """Fix-only pushes (the cell driver, the fixture generator, the fixture
    source itself) must self-trigger a rerun — not just edits to the
    workflow/lock files."""
    data = _parsed()
    triggers = data[True] if True in data else data["on"]
    paths = set(triggers["push"]["paths"])
    assert paths == {
        ".github/workflows/fsr-qualification.yml",
        "scripts/fsr_qualification_lock.json",
        "scripts/run_fsr_qualification_cell.py",
        "scripts/gauntlet/fsr_qualification.py",
        "scripts/gauntlet/fsr_qualification_fixture.py",
        "scripts/fixtures/fsr_qualification/**",
    }


def test_workflow_checkout_steps_fetch_full_history():
    """Run 2 crashed 4/6 cells: git diff --name-only <base>..HEAD failed
    exit 128 on actions/checkout's default shallow clone (fetch-depth: 1),
    which has no history reaching the frozen base_product_sha. Both the
    cell job and the aggregate job run that diff."""
    data = _parsed()
    for job_name in ("cell", "aggregate"):
        checkout = next(
            step
            for step in data["jobs"][job_name]["steps"]
            if step.get("uses", "").startswith("actions/checkout")
        )
        assert checkout["with"]["fetch-depth"] == 0, job_name


def test_workflow_work_root_uses_runner_temp_env_var_not_expression():
    """Matches ci-conformance.yml's already-proven windows-2022 pattern
    ($RUNNER_TEMP, a bash env var) instead of the ${{ runner.temp }} GH
    expression this workflow originally used, which — concatenated with a
    hand-appended '/subdir' — produced a mixed-separator path on Windows
    (D:\\a\\_temp/fsr-pilot-...) in the first two matrix runs."""
    text = _text()
    assert "$RUNNER_TEMP/fsr-pilot-" in text
    assert "$RUNNER_TEMP/fsr-cell-" in text
    assert "runner.temp" not in text


def test_workflow_pilot_step_captures_evidence():
    """The pilot previously uploaded nothing but a bare receipt.json on
    failure (Run 2: min-windows-x64/max-windows-x64 INFRASTRUCTURE_BLOCKED
    with zero diagnostic content) — it must now pass --evidence-out and the
    identifying cell/os/arch so a future failure is diagnosable from the
    uploaded artifact alone."""
    text = _text()
    pilot_index = text.index("Fixture-free GUI baseline (pilot)")
    full_index = text.index("Run cell scenario")
    pilot_block = text[pilot_index:full_index]
    assert "--evidence-out \"artifacts/${{ matrix.cell }}/pilot\"" in pilot_block
    assert "--cell-name ${{ matrix.cell }}" in pilot_block
    assert "--os-name ${{ matrix.os_name }}" in pilot_block


def test_workflow_bumps_startup_timeout_for_cold_provider_compile():
    """Run 3 (33381259363): min-linux-x64/min-macos-arm64 pilots passed and
    reached real semantic execution, but "Run cell scenario" timed out
    waiting for the MCP port while Unity's own build log showed it still
    actively compiling the FSR provider's full dependency graph (829 build
    items, no warm cache in a fresh disposable worker) — not a hang, a cold
    compile that didn't finish inside 420s. The evidence artifact's
    unity.log (captured moments later) showed the build had in fact
    succeeded. 900s gives real headroom."""
    text = _text()
    assert "--startup-timeout 420" not in text
    assert text.count("--startup-timeout 900") == 2


def test_workflow_job_timeout_covers_worst_case_phase_sum():
    """Up to 4 startup-timeout waits (pilot + 3 Editor launches in --mode
    full) plus unity-setup (40 min) must fit inside the job timeout with
    real headroom, not truncate mid-run before evidence is written."""
    data = _parsed()
    assert data["jobs"]["cell"]["timeout-minutes"] >= 150


def test_workflow_aggregate_wording_no_longer_claims_six_of_six():
    """Narrowed after run 5: the aggregate gate is 2/2 required-pass +
    one documented-blocked Windows receipt, never a stale "6/6" claim."""
    text = _text()
    assert "6/6" not in text
    aggregate_index = text.index("aggregate:")
    block = text[aggregate_index:]
    assert "documented-blocked" in block.lower() or "required-pass" in block.lower()
