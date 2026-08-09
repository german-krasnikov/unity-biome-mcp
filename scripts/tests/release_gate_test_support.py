"""Reusable, coherent release-evidence bundle for gate integration tests."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from typing import TYPE_CHECKING

from gauntlet.artifacts import build_artifact_manifest, write_artifact_manifest
from gauntlet.attestations import build_file_reference, build_receipt
from gauntlet.evidence_schema import evidence_hash, run_manifest_hash
from gauntlet.release_evidence import build_conformance_evidence, write_conformance_evidence
from gauntlet.release_gate import validate_release_gate
from gauntlet_test_fixtures import (
    write_attested_junit,
    write_complete_journal,
    write_json,
    write_upm,
    write_wheel,
)
from release_source_test_support import HARNESS_LOCK_RELATIVE, prepare_source

if TYPE_CHECKING:
    from collections.abc import Callable
    from pathlib import Path

VERSION = "1.27.0"
RUN_ID = "run-release-gate"
WORKER_ID = "worker-a-epoch-1"
PROFILE_ID = "unity-linux-py310"
SCENARIOS = (
    "tests.contracts::test_schema_parity[stdio]",
    "tests.contracts::test_version_handshake",
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare_bundle(tmp_path: Path) -> dict[str, Path]:
    tmp_path.mkdir(parents=True, exist_ok=True)
    source = prepare_source(
        tmp_path / "source",
        version=VERSION,
        profile_id=PROFILE_ID,
        scenarios=SCENARIOS,
    )
    policy = source.policy
    catalog = source.catalog
    source_observation = source.observation
    head_sha = source.head_sha

    artifact_root = tmp_path / "artifacts"
    artifact_root.mkdir()
    wheel = artifact_root / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    upm = artifact_root / "unity-biome-mcp-1.27.0.tgz"
    write_wheel(wheel, VERSION)
    write_upm(upm, VERSION)
    manifest = build_artifact_manifest(
        head_sha,
        VERSION,
        {"python_wheel": wheel, "unity_upm": upm},
    )
    manifest_path = artifact_root / "artifact-manifest.json"
    write_artifact_manifest(manifest_path, manifest)

    profile = policy.active_profiles[0]
    run_manifest_sha = run_manifest_hash(
        head_sha=head_sha,
        source_observation_sha=source_observation.observation_sha,
        policy_sha=policy.policy_sha,
        contract_catalog_sha=catalog.catalog_sha,
        profile_manifest_sha=profile.manifest_sha,
        harness_lock_sha=source_observation.file_digests[HARNESS_LOCK_RELATIVE],
        artifact_manifest_sha=manifest.manifest_sha,
        artifacts=manifest.artifact_digests,
    )

    bundle = tmp_path / "evidence"
    bundle.mkdir()
    paths = {
        "source_root": source.root,
        "policy": source.policy_path,
        "catalog": source.catalog_path,
        "head": tmp_path / "head.txt",
        "manifest": manifest_path,
        "artifact_root": artifact_root,
        "harness_lock": source.harness_lock_path,
        "wheel": wheel,
        "bundle": bundle,
        "junit": bundle / "junit.xml",
        "journal": bundle / "journal.jsonl",
        "runtime": bundle / "runtime.json",
        "worker": bundle / "worker-a.json",
        "cleanup_process": bundle / "cleanup-process.json",
        "cleanup_worker": bundle / "cleanup-worker.json",
        "evidence": bundle / "evidence.json",
    }
    paths["head"].write_text(head_sha, encoding="ascii")
    finished_at = datetime.now(timezone.utc).isoformat()
    write_attested_junit(
        paths["junit"],
        zip(profile.scenario_ids, profile.pytest_node_ids, strict=True),
    )
    write_complete_journal(
        paths["journal"],
        SCENARIOS,
        run_id=RUN_ID,
        run_manifest_sha=run_manifest_sha,
        profile=PROFILE_ID,
        timestamp=finished_at,
        workers={"worker-a": WORKER_ID},
    )
    _write_run_receipts(
        paths,
        manifest.artifact_digests,
        run_manifest_sha,
        head_sha=head_sha,
    )
    artifacts = _evidence_artifacts(paths)
    evidence = build_conformance_evidence(
        run_id=RUN_ID,
        run_manifest_sha=run_manifest_sha,
        head_sha=head_sha,
        source_observation_sha=source_observation.observation_sha,
        policy_version=policy.policy_version,
        policy_sha=policy.policy_sha,
        contract_catalog_sha=catalog.catalog_sha,
        harness_lock_sha=source_observation.file_digests[HARNESS_LOCK_RELATIVE],
        artifact_manifest_sha=manifest.manifest_sha,
        artifacts=manifest.artifact_digests,
        profile=profile.profile_id,
        profile_manifest_sha=profile.manifest_sha,
        expected_scenario_ids=profile.scenario_ids,
        executed_scenario_ids=SCENARIOS,
        selected_tests=2,
        passed=2,
        failed=0,
        skipped=0,
        blocked=0,
        untested=0,
        exit_code=0,
        required_workers=1,
        evidence_artifacts=artifacts,
        created_at=finished_at,
    )
    write_conformance_evidence(paths["evidence"], evidence)
    return paths


def validate_bundle(
    paths: dict[str, Path],
    *,
    expected_head_sha: str | None = None,
) -> None:
    validate_release_gate(
        policy_path=paths["policy"],
        source_root=paths["source_root"],
        artifact_manifest_path=paths["manifest"],
        artifact_root=paths["artifact_root"],
        evidence_paths=(paths["evidence"],),
        expected_head_sha=expected_head_sha or read_head(paths),
    )


def read_head(paths: dict[str, Path]) -> str:
    return paths["head"].read_text(encoding="ascii")


def mutate_evidence(path: Path, mutate: Callable[[dict[str, object]], None]) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    mutate(data)
    data.pop("evidence_hash")
    data["evidence_hash"] = evidence_hash(data)
    path.write_text(json.dumps(data), encoding="utf-8")


def rewrite_receipt(
    paths: dict[str, Path],
    path_key: str,
    kind: str,
    updates: dict[str, object],
) -> None:
    path = paths[path_key]
    raw = json.loads(path.read_text(encoding="utf-8"))
    fields = {key: value for key, value in raw.items() if key not in {"schema_version", "kind", "receipt_hash"}}
    fields.update(updates)
    write_json(path, build_receipt(kind, fields))
    reference = build_file_reference(path, paths["bundle"]).as_dict()

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        if path_key == "runtime":
            artifacts["runtime"] = reference
        elif path_key == "worker":
            artifacts["workers"] = [reference]
        else:
            cleanup = artifacts["cleanup"]
            assert isinstance(cleanup, list)
            index = 0 if path_key == "cleanup_process" else 1
            cleanup[index] = reference

    mutate_evidence(paths["evidence"], update)


def refresh_junit_and_runtime(paths: dict[str, Path]) -> None:
    rewrite_receipt(paths, "runtime", "runtime", {"junit_sha": sha256(paths["junit"])})

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        artifacts["junit"] = build_file_reference(paths["junit"], paths["bundle"]).as_dict()

    mutate_evidence(paths["evidence"], update)


def refresh_journal_and_runtime(paths: dict[str, Path]) -> None:
    rewrite_receipt(paths, "runtime", "runtime", {"journal_sha": sha256(paths["journal"])})

    def update(data: dict[str, object]) -> None:
        artifacts = data["evidence_artifacts"]
        assert isinstance(artifacts, dict)
        artifacts["journal"] = build_file_reference(paths["journal"], paths["bundle"]).as_dict()

    mutate_evidence(paths["evidence"], update)


def _write_run_receipts(
    paths: dict[str, Path],
    artifact_digests: dict[str, str],
    run_manifest_sha: str,
    *,
    head_sha: str,
) -> None:
    common = {"profile": PROFILE_ID, "run_id": RUN_ID, "run_manifest_sha": run_manifest_sha}
    write_json(
        paths["runtime"],
        build_receipt(
            "runtime",
            {
                **common,
                "driver": "unity_editor",
                "head_sha": head_sha,
                "os": "linux",
                "python": "3.10",
                "unity": "6000.0.65f1",
                "plugin_scope": "exact",
                "consumed_artifacts": artifact_digests,
                "junit_sha": sha256(paths["junit"]),
                "journal_sha": sha256(paths["journal"]),
                "exit_code": 0,
            },
        ),
    )
    write_json(
        paths["worker"],
        build_receipt(
            "worker_identity",
            {
                **common,
                "role": "worker-a",
                "worker_id": WORKER_ID,
                "project_id": "c" * 64,
                "port": 9500,
                "os": "linux",
                "unity": "6000.0.65f1",
                "plugin_scope": "exact",
                "loaded_artifacts": {"unity_upm": artifact_digests["unity_upm"]},
                "clean_before": True,
            },
        ),
    )
    for key, obligation in (
        ("cleanup_process", "process-tree"),
        ("cleanup_worker", "worker-a"),
    ):
        write_json(
            paths[key],
            build_receipt(
                "cleanup",
                {**common, "obligation": obligation, "clean": True, "details_hash": "d" * 64},
            ),
        )


def _evidence_artifacts(paths: dict[str, Path]) -> dict[str, object]:
    root = paths["bundle"]
    return {
        "junit": build_file_reference(paths["junit"], root).as_dict(),
        "journal": build_file_reference(paths["journal"], root).as_dict(),
        "runtime": build_file_reference(paths["runtime"], root).as_dict(),
        "workers": [build_file_reference(paths["worker"], root).as_dict()],
        "cleanup": [
            build_file_reference(paths["cleanup_process"], root).as_dict(),
            build_file_reference(paths["cleanup_worker"], root).as_dict(),
        ],
    }
