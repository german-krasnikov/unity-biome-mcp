"""P0-70 commit 1: `build(source-patch): pin qualified FSR provider`.

Pins FINAL_FSR_ADAPTER_SHA into a *disposable worker's* manifest only —
never the tracked `unity-test-project/Packages/manifest.json` and never the
base product's `unity-plugin/package.json`. See §6 P0-70 in
Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.

Runs in the standard `scripts/tests` lane: no Unity, no network, hermetic
tmp_path/JSON only.
"""
import json
import sys
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
SCRIPTS = TESTS.parent
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))
import create_unity_test_worker as worker
from test_create_unity_test_worker import source_project

REPO_ROOT = SCRIPTS.parent
PIN_PATH = SCRIPTS / "source_patch_provider_pin.json"
EXPECTED_PACKAGE = "com.handzlikchris.fastscriptreload"
EXPECTED_SHA = "e50d43dda33e2d62c68be25278d48bc07f6003ff"
EXPECTED_URL = "https://github.com/german-krasnikov/FastScriptReload.git?path=/Assets"
EXPECTED_DEPENDENCY = f"{EXPECTED_URL}#{EXPECTED_SHA}"


def _write_pin(tmp_path: Path, **overrides) -> Path:
    payload = {
        "schema_version": 1,
        "package_name": EXPECTED_PACKAGE,
        "git_url": EXPECTED_URL,
        "ref": EXPECTED_SHA,
    }
    payload.update(overrides)
    path = tmp_path / "pin.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


# ---------------------------------------------------------------------------
# The tracked pin file itself
# ---------------------------------------------------------------------------

def test_tracked_pin_file_matches_final_adapter_sha():
    """The one tracked pin — must name exactly FINAL_FSR_ADAPTER_SHA."""
    payload = json.loads(PIN_PATH.read_text(encoding="utf-8"))
    assert payload["package_name"] == EXPECTED_PACKAGE
    assert payload["git_url"] == EXPECTED_URL
    assert payload["ref"] == EXPECTED_SHA


def test_tracked_pin_git_url_hardcodes_assets_path_literal():
    """Independent of EXPECTED_URL/EXPECTED_DEPENDENCY — hardcoded literal,
    never derived from this module's own fixture constants.

    Real incident (Cycle A attempt 6, P0-80): the FSR fork's package.json
    lives under Assets/ (the qualified branch is a full Unity project, not a
    bare UPM package), so the git dependency needs `?path=/Assets` or Unity's
    package resolution fails with "Repository does not contain a package
    manifest". The pin file was missing it, and this test's own
    EXPECTED_URL constant was ALSO missing it — so
    test_tracked_pin_file_matches_final_adapter_sha compared the tracked
    file against an equally-wrong fixture and could never catch this class
    of regression. This test must never read EXPECTED_URL or
    EXPECTED_DEPENDENCY, so a future accidental edit to those constants
    cannot silently defeat it a second time (double-red: fails if the pin
    loses the path, and fails if _load_source_patch_pin's composition
    breaks)."""
    payload = json.loads(PIN_PATH.read_text(encoding="utf-8"))
    assert "?path=/Assets" in payload["git_url"]
    dependency = worker._load_source_patch_pin(PIN_PATH)["com.handzlikchris.fastscriptreload"]
    assert dependency == (
        "https://github.com/german-krasnikov/FastScriptReload.git"
        "?path=/Assets#e50d43dda33e2d62c68be25278d48bc07f6003ff"
    )


# ---------------------------------------------------------------------------
# _load_source_patch_pin
# ---------------------------------------------------------------------------

def test_load_source_patch_pin_merges_git_ref(tmp_path: Path):
    pin_path = _write_pin(tmp_path)
    result = worker._load_source_patch_pin(pin_path)
    assert result == {EXPECTED_PACKAGE: EXPECTED_DEPENDENCY}


def test_load_source_patch_pin_missing_file_raises(tmp_path: Path):
    with pytest.raises(worker.WorkerCreationError):
        worker._load_source_patch_pin(tmp_path / "absent.json")


@pytest.mark.parametrize("missing_key", ["package_name", "git_url", "ref"])
def test_load_source_patch_pin_missing_field_raises(tmp_path: Path, missing_key: str):
    payload = {
        "package_name": EXPECTED_PACKAGE,
        "git_url": EXPECTED_URL,
        "ref": EXPECTED_SHA,
    }
    del payload[missing_key]
    path = tmp_path / "pin.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(worker.WorkerCreationError):
        worker._load_source_patch_pin(path)


# ---------------------------------------------------------------------------
# create_worker(..., source_patch_provider_pin=...)
# ---------------------------------------------------------------------------

def _repository(tmp_path: Path) -> Path:
    repository = tmp_path / "repository"
    (repository / "unity-plugin").mkdir(parents=True)
    (repository / "unity-plugin-reload").mkdir()
    (repository / "unity-plugin" / "package.json").write_text("{}", encoding="utf-8")
    (repository / "unity-plugin-reload" / "package.json").write_text("{}", encoding="utf-8")
    return repository


def test_create_worker_with_pin_merges_manifest_dependency(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"
    pin_path = _write_pin(tmp_path)

    worker.create_worker(source, destination, source_patch_provider_pin=pin_path)

    manifest = json.loads((destination / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["dependencies"][EXPECTED_PACKAGE] == EXPECTED_DEPENDENCY
    # Merge, not replace: the repo-local packages must still be present.
    assert manifest["dependencies"]["com.unity-biome-mcp.editor"] == (
        "file:../LocalPackages/unity-plugin"
    )


def test_create_worker_without_pin_leaves_manifest_unchanged(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"

    worker.create_worker(source, destination)

    manifest = json.loads((destination / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    assert EXPECTED_PACKAGE not in manifest["dependencies"]


def test_create_worker_with_pin_records_marker_fields(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"
    pin_path = _write_pin(tmp_path)

    marker = worker.create_worker(source, destination, source_patch_provider_pin=pin_path)

    assert marker["source_patch_provider_ref"] == EXPECTED_SHA
    assert marker["source_patch_provider_package"] == EXPECTED_PACKAGE
    assert len(marker["source_patch_provider_pin_sha256"]) == 64


def test_create_worker_without_pin_omits_marker_fields(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"

    marker = worker.create_worker(source, destination)

    assert "source_patch_provider_ref" not in marker
