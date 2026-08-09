"""Pytest plugin that binds final collected leaves to a reviewed scenario manifest."""

from __future__ import annotations

import os
import stat
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import TYPE_CHECKING

import pytest

from gauntlet.json_io import JsonFileError, atomic_write_json, load_json_object
from gauntlet.policy_fields import (
    PolicyError,
    require_exact_keys,
    require_id,
    require_pytest_node_id,
    require_scenario_ids,
)
from gauntlet.receipts import JournalError, content_hash

if TYPE_CHECKING:
    from collections.abc import Sequence

_ROOT_KEYS = {"schema_version", "profile", "scenarios"}
_SCENARIO_KEYS = {"id", "pytest_node_id"}
_SCENARIO_PROPERTY = "gauntlet_scenario_id"
_PYTEST_NODE_PROPERTY = "gauntlet_pytest_node_id"


class AttestationError(ValueError):
    """Raised when pytest selection cannot prove the reviewed scenario set."""


@dataclass(frozen=True, slots=True)
class ScenarioBinding:
    scenario_id: str
    pytest_node_id: str


@dataclass(frozen=True, slots=True)
class AttestationManifest:
    profile_id: str
    bindings: tuple[ScenarioBinding, ...]
    manifest_sha: str


def write_attestation_manifest(
    path: Path,
    profile_id: str,
    bindings: Sequence[ScenarioBinding],
) -> str:
    payload = _manifest_payload(profile_id, bindings)
    manifest = _parse_manifest(payload)
    try:
        atomic_write_json(path, payload)
    except JsonFileError as exc:
        raise AttestationError(str(exc)) from exc
    return manifest.manifest_sha


def load_attestation_manifest(path: Path, *, expected_sha: str) -> AttestationManifest:
    try:
        payload = load_json_object(path)
    except JsonFileError as exc:
        raise AttestationError(str(exc)) from exc
    manifest = _parse_manifest(payload)
    if manifest.manifest_sha != expected_sha.lower():
        raise AttestationError("attestation manifest digest does not match")
    return manifest


def _manifest_payload(
    profile_id: str,
    bindings: Sequence[ScenarioBinding],
) -> dict[str, object]:
    return {
        "schema_version": 1,
        "profile": profile_id,
        "scenarios": [
            {"id": binding.scenario_id, "pytest_node_id": binding.pytest_node_id}
            for binding in bindings
        ],
    }


def _parse_manifest(payload: dict[str, object]) -> AttestationManifest:
    try:
        require_exact_keys(payload, _ROOT_KEYS, "attestation manifest schema")
        if payload["schema_version"] != 1:
            raise AttestationError("unsupported attestation manifest schema")
        profile_id = require_id(payload["profile"], "attestation profile")
        raw_bindings = payload["scenarios"]
        if not isinstance(raw_bindings, list) or not raw_bindings:
            raise AttestationError("attestation manifest requires scenarios")
        bindings = tuple(_parse_binding(raw) for raw in raw_bindings)
    except PolicyError as exc:
        raise AttestationError(str(exc)) from exc
    scenario_ids = [binding.scenario_id for binding in bindings]
    node_ids = [binding.pytest_node_id for binding in bindings]
    if len(set(scenario_ids)) != len(scenario_ids):
        raise AttestationError("attestation manifest contains a duplicate scenario")
    if len(set(node_ids)) != len(node_ids):
        raise AttestationError("attestation manifest contains a duplicate pytest node")
    canonical = tuple(sorted(bindings, key=lambda binding: binding.scenario_id))
    canonical_payload = _manifest_payload(profile_id, canonical)
    try:
        digest = content_hash(canonical_payload)
    except JournalError as exc:
        raise AttestationError(str(exc)) from exc
    return AttestationManifest(profile_id, canonical, digest)


def _parse_binding(value: object) -> ScenarioBinding:
    if not isinstance(value, dict):
        raise AttestationError("attestation scenario must be an object")
    require_exact_keys(value, _SCENARIO_KEYS, "attestation scenario schema")
    return ScenarioBinding(
        require_scenario_ids([value["id"]])[0],
        require_pytest_node_id(value["pytest_node_id"]),
    )


def pytest_addoption(parser: pytest.Parser) -> None:
    group = parser.getgroup("contract-gauntlet")
    group.addoption("--gauntlet-manifest")
    group.addoption("--gauntlet-manifest-sha")
    group.addoption("--gauntlet-source-root")


def pytest_configure(config: pytest.Config) -> None:
    manifest_path = config.getoption("gauntlet_manifest")
    manifest_sha = config.getoption("gauntlet_manifest_sha")
    source_root = config.getoption("gauntlet_source_root")
    if not all(isinstance(value, str) and value for value in (manifest_path, manifest_sha, source_root)):
        raise pytest.UsageError("attested pytest requires manifest, digest, and source root")
    if config.option.markexpr or config.option.keyword or config.option.collectonly:
        raise pytest.UsageError("attested pytest forbids selector overrides")
    if os.environ.get("PYTEST_ADDOPTS") or os.environ.get("PYTEST_PLUGINS"):
        raise pytest.UsageError("attested pytest forbids ambient selector or plugin overrides")
    if os.environ.get("PYTEST_DISABLE_PLUGIN_AUTOLOAD") != "1":
        raise pytest.UsageError("attested pytest requires disabled plugin autoload")
    if sys.flags.optimize:
        raise pytest.UsageError("attested pytest forbids Python optimization")
    if not getattr(config.option, "xmlpath", None):
        raise pytest.UsageError("attested pytest requires JUnit output")
    try:
        resolved_root = _source_root(Path(source_root))
        manifest = load_attestation_manifest(Path(manifest_path), expected_sha=manifest_sha)
    except AttestationError as exc:
        raise pytest.UsageError(str(exc)) from exc
    config.pluginmanager.register(
        _AttestationPlugin(manifest, resolved_root),
        "contract-gauntlet-runtime",
    )


def _source_root(path: Path) -> Path:
    try:
        metadata = path.lstat()
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise AttestationError("attested source root is not accessible") from exc
    if not stat.S_ISDIR(metadata.st_mode):
        raise AttestationError("attested source root must be a real directory")
    return resolved


class _AttestationPlugin:
    def __init__(self, manifest: AttestationManifest, source_root: Path) -> None:
        self._source_root = source_root
        self._expected = {binding.pytest_node_id: binding for binding in manifest.bindings}

    @pytest.hookimpl(tryfirst=True)
    def pytest_runtestloop(self, session: pytest.Session) -> None:
        actual_nodes = [self._canonical_node(item) for item in session.items]
        duplicates = sorted(node for node, count in Counter(actual_nodes).items() if count > 1)
        if duplicates:
            raise pytest.UsageError(f"attested collection contains duplicate nodes: {duplicates}")
        missing = sorted(set(self._expected) - set(actual_nodes))
        extra = sorted(set(actual_nodes) - set(self._expected))
        if missing or extra:
            raise pytest.UsageError(
                f"attested collection mismatch: missing={missing}, extra={extra}"
            )
        xfail_nodes = sorted(
            node_id
            for item, node_id in zip(session.items, actual_nodes, strict=True)
            if any(item.iter_markers(name="xfail"))
        )
        if xfail_nodes:
            raise pytest.UsageError(f"attested collection forbids xfail markers: {xfail_nodes}")
        for item, node_id in zip(session.items, actual_nodes, strict=True):
            self._set_properties(item.user_properties, self._expected[node_id])

    @pytest.hookimpl(hookwrapper=True, tryfirst=True)
    def pytest_runtest_makereport(self, item: pytest.Item, call: pytest.CallInfo[object]):
        outcome = yield
        report = outcome.get_result()
        node_id = self._canonical_node(item)
        binding = self._expected.get(node_id)
        if binding is not None:
            self._set_properties(report.user_properties, binding)

    def _canonical_node(self, item: pytest.Item) -> str:
        try:
            relative = item.path.resolve(strict=True).relative_to(self._source_root)
            _, selector = item.nodeid.split("::", 1)
        except (OSError, ValueError) as exc:
            raise pytest.UsageError("collected pytest node is outside the attested source root") from exc
        return f"{PurePosixPath(relative.as_posix())}::{selector}"

    @staticmethod
    def _set_properties(properties: list[tuple[str, object]], binding: ScenarioBinding) -> None:
        properties[:] = [
            (name, value)
            for name, value in properties
            if name not in {_SCENARIO_PROPERTY, _PYTEST_NODE_PROPERTY}
        ]
        properties.extend(
            (
                (_SCENARIO_PROPERTY, binding.scenario_id),
                (_PYTEST_NODE_PROPERTY, binding.pytest_node_id),
            )
        )
