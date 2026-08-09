"""Cross-file coherence tests for one profile evidence bundle."""

from __future__ import annotations

import hashlib
import sys
from dataclasses import replace
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.attestations import build_file_reference, build_receipt  # noqa: E402
from gauntlet.evidence_schema import ProfileRequirement  # noqa: E402
from gauntlet.profile_evidence import (  # noqa: E402
    ProfileEvidenceError,
    verify_profile_artifacts,
)
from gauntlet_test_fixtures import (  # noqa: E402
    write_attested_junit,
    write_complete_journal,
    write_json,
)

PROFILE = "public-stdio-linux"
RUN_ID = "run-public-stdio"
RUN_MANIFEST_SHA = "a" * 64
SCENARIOS = ("tests.contracts::test_stdio",)
PYTEST_NODES = ("server/tests/contracts/test_stdio.py::test_stdio",)
ARTIFACTS = {
    "python_wheel": "b" * 64,
    "unity_editor_upm": "c" * 64,
    "unity_reload_upm": "d" * 64,
}


def _sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _bundle(tmp_path: Path) -> tuple[dict[str, object], ProfileRequirement]:
    root = tmp_path / "evidence"
    root.mkdir()
    junit = root / "junit.xml"
    journal = root / "journal.jsonl"
    runtime = root / "runtime.json"
    cleanup_process = root / "cleanup-process.json"
    cleanup_peer = root / "cleanup-peer.json"
    finished_at = "2026-08-09T00:00:00+00:00"
    write_attested_junit(junit, zip(SCENARIOS, PYTEST_NODES, strict=True))
    write_complete_journal(
        journal,
        SCENARIOS,
        run_id=RUN_ID,
        run_manifest_sha=RUN_MANIFEST_SHA,
        profile=PROFILE,
        timestamp=finished_at,
    )
    common = {
        "profile": PROFILE,
        "run_id": RUN_ID,
        "run_manifest_sha": RUN_MANIFEST_SHA,
    }
    write_json(
        runtime,
        build_receipt(
            "runtime",
            {
                **common,
                "driver": "public_stdio",
                "head_sha": "d" * 40,
                "os": "linux",
                "python": "3.10",
                "unity": None,
                "plugin_scope": "none",
                "consumed_artifacts": {"python_wheel": ARTIFACTS["python_wheel"]},
                "junit_sha": _sha(junit),
                "journal_sha": _sha(journal),
                "exit_code": 0,
            },
        ),
    )
    cleanup_paths = (cleanup_process, cleanup_peer)
    for path, obligation in zip(
        cleanup_paths,
        ("stdio-process", "tcp-peer"),
        strict=True,
    ):
        write_json(
            path,
            build_receipt(
                "cleanup",
                {
                    **common,
                    "obligation": obligation,
                    "clean": True,
                    "details_hash": "e" * 64,
                },
            ),
        )
    refs = {
        "junit": build_file_reference(junit, root).as_dict(),
        "journal": build_file_reference(journal, root).as_dict(),
        "runtime": build_file_reference(runtime, root).as_dict(),
        "workers": [],
        "cleanup": [build_file_reference(path, root).as_dict() for path in cleanup_paths],
    }
    requirement = ProfileRequirement(
        profile_manifest_sha="f" * 64,
        scenario_ids=SCENARIOS,
        pytest_node_ids=PYTEST_NODES,
        driver="public_stdio",
        operating_system="linux",
        python_version="3.10",
        unity_version=None,
        plugin_scope="none",
        required_workers=0,
        worker_roles=(),
        consumed_artifacts=("python_wheel",),
        cleanup_obligations=("stdio-process", "tcp-peer"),
    )
    return refs, requirement


def test_zero_worker_profile_still_requires_independent_cleanup(tmp_path: Path) -> None:
    refs, requirement = _bundle(tmp_path)

    derived = verify_profile_artifacts(
        refs,
        bundle_root=tmp_path / "evidence",
        profile_id=PROFILE,
        requirement=requirement,
        expected_head_sha="d" * 40,
        expected_artifacts=ARTIFACTS,
        expected_run_manifest_sha=RUN_MANIFEST_SHA,
    )

    assert derived.worker_count == 0
    cleanup = refs["cleanup"]
    assert isinstance(cleanup, list)
    refs["cleanup"] = cleanup[:1]
    with pytest.raises(ProfileEvidenceError, match="cleanup obligations"):
        verify_profile_artifacts(
            refs,
            bundle_root=tmp_path / "evidence",
            profile_id=PROFILE,
            requirement=requirement,
            expected_head_sha="d" * 40,
            expected_artifacts=ARTIFACTS,
            expected_run_manifest_sha=RUN_MANIFEST_SHA,
        )


def test_profile_evidence_rejects_pytest_node_substitution(tmp_path: Path) -> None:
    refs, requirement = _bundle(tmp_path)
    substituted = replace(
        requirement,
        pytest_node_ids=("server/tests/contracts/test_other.py::test_always_green",),
    )

    with pytest.raises(ProfileEvidenceError, match="pytest node"):
        verify_profile_artifacts(
            refs,
            bundle_root=tmp_path / "evidence",
            profile_id=PROFILE,
            requirement=substituted,
            expected_head_sha="d" * 40,
            expected_artifacts=ARTIFACTS,
            expected_run_manifest_sha=RUN_MANIFEST_SHA,
        )
