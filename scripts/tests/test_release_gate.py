"""End-to-end regressions for the fail-closed release evidence gate."""


import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.receipts import ReceiptJournal  # noqa: E402
from gauntlet.release_gate import GateError, validate_release_gate  # noqa: E402
from gauntlet_test_fixtures import rewrite_journal_events, write_complete_journal  # noqa: E402
from player_playtest_gate_test_support import player_evidence_paths  # noqa: E402
from release_gate_mutation_support import (  # noqa: E402
    mutate_evidence,
    refresh_journal_and_runtime,
    refresh_junit_and_runtime,
    rewrite_receipt,
)
from release_gate_test_support import (  # noqa: E402
    PROFILE_ID,
    RUN_ID,
    SCENARIOS,
    VERSION,
    prepare_bundle,
    read_head,
    validate_bundle,
)


def test_release_gate_accepts_exact_artifact_backed_bundle(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    summary = validate_release_gate(
        policy_path=paths["policy"],
        source_root=paths["source_root"],
        artifact_manifest_path=paths["manifest"],
        artifact_root=paths["artifact_root"],
        evidence_paths=(paths["evidence"],),
        player_playtest_evidence_paths=player_evidence_paths(paths),
        expected_head_sha=read_head(paths),
    )

    assert summary.product_version == VERSION
    assert summary.profiles == (PROFILE_ID,)


def test_release_gate_rejects_substituted_release_or_evidence_bytes(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    paths["wheel"].write_bytes(b"replacement")
    with pytest.raises(GateError, match="artifact"):
        validate_bundle(paths)

    paths = prepare_bundle(tmp_path / "second")
    paths["junit"].write_text("<testsuites />", encoding="utf-8")
    with pytest.raises(GateError, match="size|digest"):
        validate_bundle(paths)


def test_release_gate_rejects_manifest_with_wrong_type_specific_filename(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    manifest = json.loads(paths["manifest"].read_text(encoding="utf-8"))
    wheel = next(record for record in manifest["artifacts"] if record["type"] == "python_wheel")
    wheel["filename"] = "totally_other-9.9.9-py3-none-any.whl"
    paths["manifest"].write_text(json.dumps(manifest), encoding="utf-8")

    with pytest.raises(GateError, match="filename"):
        validate_bundle(paths)


def test_release_gate_rejects_artifact_symlink_escaping_staging_root(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    outside = tmp_path / "outside-wheel.whl"
    outside.write_bytes(paths["wheel"].read_bytes())
    paths["wheel"].unlink()
    try:
        paths["wheel"].symlink_to(outside)
    except OSError as exc:
        pytest.skip(f"symlinks are unavailable on this platform: {exc}")

    with pytest.raises(GateError, match="staging root|regular file"):
        validate_bundle(paths)


def test_release_gate_rejects_worker_missing_reload_package(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    evidence = json.loads(paths["evidence"].read_text(encoding="utf-8"))
    artifacts = evidence["artifacts"]
    assert isinstance(artifacts, dict)
    rewrite_receipt(
        paths,
        "worker",
        "worker_identity",
        {"loaded_artifacts": {"unity_editor_upm": artifacts["unity_editor_upm"]}},
    )

    with pytest.raises(GateError, match="worker runtime identity"):
        validate_bundle(paths)


def test_release_gate_rejects_reload_archive_not_matching_observed_source(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path, source_reload_version="0.1.5")

    with pytest.raises(GateError, match="reload.*observed source"):
        validate_bundle(paths)


def test_release_gate_rejects_invalid_observed_reload_package_name(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path, source_reload_name="com.example.other")

    with pytest.raises(GateError, match="reload.*source package name"):
        validate_bundle(paths)


def test_release_gate_derives_counts_instead_of_trusting_summary(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    mutate_evidence(paths["evidence"], lambda data: data.update({"passed": 1}))

    with pytest.raises(GateError, match="passed"):
        validate_bundle(paths)


def test_release_gate_rejects_content_addressed_incomplete_journal(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    evidence = json.loads(paths["evidence"].read_text(encoding="utf-8"))
    paths["journal"].unlink()
    journal = ReceiptJournal(paths["journal"], RUN_ID)
    journal.append(
        "run_started",
        {
            "profile": PROFILE_ID,
            "run_manifest_sha": evidence["run_manifest_sha"],
        },
    )
    refresh_journal_and_runtime(paths)

    with pytest.raises(GateError, match="run_finished"):
        validate_bundle(paths)


def test_release_gate_rejects_orphan_junit_case_even_when_rebound(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    paths["junit"].write_text(
        """<testsuites>
  <testsuite tests="1" failures="0" errors="0" skipped="0">
    <testcase classname="tests.contracts" name="test_schema_parity[stdio]" />
  </testsuite>
  <testcase classname="tests.contracts" name="test_version_handshake" />
</testsuites>""",
        encoding="utf-8",
    )
    refresh_junit_and_runtime(paths)

    with pytest.raises(GateError, match="owned by exactly one leaf suite"):
        validate_bundle(paths)


def test_release_gate_rejects_contradictory_junit_root_summary(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    original = paths["junit"].read_text(encoding="utf-8")
    paths["junit"].write_text(
        f'<testsuites tests="2" failures="1" errors="0" skipped="0">{original}</testsuites>',
        encoding="utf-8",
    )
    refresh_junit_and_runtime(paths)

    with pytest.raises(GateError, match="testsuites declared outcome counts"):
        validate_bundle(paths)


def test_release_gate_rejects_cross_run_receipt_splicing(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    rewrite_receipt(paths, "worker", "worker_identity", {"run_id": "different-run"})

    with pytest.raises(GateError, match="worker runtime identity"):
        validate_bundle(paths)


@pytest.mark.parametrize(
    ("updates", "message"),
    [
        ({"os": "windows"}, "runtime identity"),
        ({"python": "3.12"}, "runtime identity"),
        ({"unity": "6000.1.0f1"}, "runtime identity"),
        ({"plugin_scope": "none"}, "runtime identity"),
    ],
)
def test_release_gate_rejects_wrong_runtime_identity(
    tmp_path: Path,
    updates: dict[str, object],
    message: str,
) -> None:
    paths = prepare_bundle(tmp_path)
    rewrite_receipt(paths, "runtime", "runtime", updates)

    with pytest.raises(GateError, match=message):
        validate_bundle(paths)


def test_release_gate_requires_every_cleanup_obligation(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)

    def remove_cleanup(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        cleanup = artifacts["cleanup"]
        assert isinstance(cleanup, list)
        artifacts["cleanup"] = cleanup[:1]

    mutate_evidence(paths["evidence"], remove_cleanup)
    with pytest.raises(GateError, match="cleanup obligations"):
        validate_bundle(paths)


def test_release_gate_rejects_stale_journal_rewrapped_as_fresh(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    evidence = json.loads(paths["evidence"].read_text(encoding="utf-8"))
    paths["journal"].unlink()
    write_complete_journal(
        paths["journal"],
        SCENARIOS,
        run_id=RUN_ID,
        run_manifest_sha=str(evidence["run_manifest_sha"]),
        profile=PROFILE_ID,
        timestamp="2020-01-01T00:00:00+00:00",
        workers={"worker-a": "worker-a-epoch-1"},
    )
    refresh_journal_and_runtime(paths)

    with pytest.raises(GateError, match="created_at"):
        validate_bundle(paths)


def test_release_gate_rejects_late_worker_lease(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    evidence = json.loads(paths["evidence"].read_text(encoding="utf-8"))
    paths["journal"].unlink()
    write_complete_journal(
        paths["journal"],
        SCENARIOS,
        run_id=RUN_ID,
        run_manifest_sha=str(evidence["run_manifest_sha"]),
        profile=PROFILE_ID,
        timestamp=str(evidence["created_at"]),
        workers={"worker-a": "worker-a-epoch-1"},
        lease_workers_after_scenarios=True,
    )
    refresh_journal_and_runtime(paths)

    with pytest.raises(GateError, match="precede scenario"):
        validate_bundle(paths)


def test_release_gate_rejects_rehashed_unsupported_journal_envelope(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    rewrite_journal_events(
        paths["journal"],
        lambda event: event.update({"schema_version": 999, "unexpected": True}),
    )
    refresh_journal_and_runtime(paths)

    with pytest.raises(GateError, match="fields|schema"):
        validate_bundle(paths)
