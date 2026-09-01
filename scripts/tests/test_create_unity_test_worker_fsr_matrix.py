"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: worker-lifecycle
helpers needed by `scripts/run_fsr_qualification_cell.py`.

`rewrite_manifest_pin` adds/removes the Source Patch provider dependency on
an *already-created* disposable worker mid-lifecycle (offline install ->
offline uninstall, §6 P0-80 steps 3 and 7) — distinct from
`create_worker(..., source_patch_provider_pin=...)`, which only pins at
creation time.

`rewrite_project_version` targets a different Unity version/revision on an
already-created worker so a headed (non-batchmode) launch of U_MAX never
hits Unity's interactive "project created with an earlier version, continue
anyway?" dialog, which blocks indefinitely outside batchmode.

Both are disposable-worker-only, mid-lifecycle mutations — never touch a
non-disposable project, and `create_unity_test_worker`'s default
`create_worker(...)` behavior is unchanged (both target_unity_* kwargs
default to None).

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

PIN_PATH = SCRIPTS / "source_patch_provider_pin.json"
EXPECTED_PACKAGE = "com.handzlikchris.fastscriptreload"


def _worker_dir(tmp_path: Path) -> Path:
    destination = tmp_path / "worker"
    packages = destination / "Packages"
    packages.mkdir(parents=True)
    (packages / "manifest.json").write_text(
        json.dumps({"dependencies": {"com.unity.test-framework": "1.6.0"}}),
        encoding="utf-8",
    )
    (packages / "packages-lock.json").write_text("{}", encoding="utf-8")
    (destination / "ProjectSettings").mkdir()
    return destination


# ---------------------------------------------------------------------------
# rewrite_manifest_pin
# ---------------------------------------------------------------------------

def test_rewrite_manifest_pin_install_true_adds_pinned_dependency(tmp_path: Path):
    destination = _worker_dir(tmp_path)

    worker.rewrite_manifest_pin(destination, PIN_PATH, install=True)

    manifest = json.loads((destination / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    assert EXPECTED_PACKAGE in manifest["dependencies"]
    assert manifest["dependencies"]["com.unity.test-framework"] == "1.6.0"


def test_rewrite_manifest_pin_install_true_drops_stale_lock(tmp_path: Path):
    destination = _worker_dir(tmp_path)

    worker.rewrite_manifest_pin(destination, PIN_PATH, install=True)

    assert not (destination / "Packages" / "packages-lock.json").exists()


def test_rewrite_manifest_pin_install_false_removes_dependency(tmp_path: Path):
    destination = _worker_dir(tmp_path)
    worker.rewrite_manifest_pin(destination, PIN_PATH, install=True)

    worker.rewrite_manifest_pin(destination, PIN_PATH, install=False)

    manifest = json.loads((destination / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    assert EXPECTED_PACKAGE not in manifest["dependencies"]
    assert manifest["dependencies"]["com.unity.test-framework"] == "1.6.0"


def test_rewrite_manifest_pin_install_false_is_idempotent_when_absent(tmp_path: Path):
    destination = _worker_dir(tmp_path)

    worker.rewrite_manifest_pin(destination, PIN_PATH, install=False)  # must not raise

    manifest = json.loads((destination / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    assert EXPECTED_PACKAGE not in manifest["dependencies"]


def test_rewrite_manifest_pin_missing_pin_file_raises(tmp_path: Path):
    destination = _worker_dir(tmp_path)

    with pytest.raises(worker.WorkerCreationError):
        worker.rewrite_manifest_pin(destination, tmp_path / "absent.json", install=True)


def test_rewrite_manifest_pin_never_touches_tracked_base_manifest():
    """The tracked base manifest is never a valid `destination` — it has no
    Packages/manifest.json at exactly that path *and* no dependencies key
    named after the qualification pin already, so an accidental call against
    the repo root would fail loudly on manifest_path.read_text, not silently
    corrupt the tracked file."""
    repo_manifest = SCRIPTS.parent / "unity-test-project" / "Packages" / "manifest.json"
    payload = json.loads(repo_manifest.read_text(encoding="utf-8"))
    assert EXPECTED_PACKAGE not in payload["dependencies"]


# ---------------------------------------------------------------------------
# rewrite_project_version
# ---------------------------------------------------------------------------

def test_rewrite_project_version_overwrites_editor_version_and_revision(tmp_path: Path):
    destination = _worker_dir(tmp_path)
    (destination / "ProjectSettings" / "ProjectVersion.txt").write_text(
        "m_EditorVersion: 6000.0.65f1\n"
        "m_EditorVersionWithRevision: 6000.0.65f1 (a18e2220bd50)\n",
        encoding="utf-8",
    )

    worker.rewrite_project_version(
        destination, unity_version="6000.5.10f1", unity_revision="3bd4f66ad299"
    )

    text = (destination / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    assert "m_EditorVersion: 6000.5.10f1" in text
    assert "m_EditorVersionWithRevision: 6000.5.10f1 (3bd4f66ad299)" in text
    assert "6000.0.65f1" not in text


def test_rewrite_project_version_missing_file_raises(tmp_path: Path):
    destination = tmp_path / "no-settings"
    destination.mkdir()

    with pytest.raises(worker.WorkerCreationError):
        worker.rewrite_project_version(
            destination, unity_version="6000.5.10f1", unity_revision="3bd4f66ad299"
        )


# ---------------------------------------------------------------------------
# create_worker(..., target_unity_version=..., target_unity_revision=...)
# ---------------------------------------------------------------------------

def _repository(tmp_path: Path) -> Path:
    repository = tmp_path / "repository"
    (repository / "unity-plugin").mkdir(parents=True)
    (repository / "unity-plugin-reload").mkdir()
    (repository / "unity-plugin" / "package.json").write_text("{}", encoding="utf-8")
    (repository / "unity-plugin-reload" / "package.json").write_text("{}", encoding="utf-8")
    return repository


def test_create_worker_default_leaves_project_version_unchanged(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Default behavior (no target_unity_* kwargs) must not change —
    approved explicitly: 'дефолтное поведение create_unity_test_worker не
    менять'."""
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"

    worker.create_worker(source, destination)

    text = (destination / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    assert f"m_EditorVersion: {worker.UNITY_VERSION}" in text


def test_create_worker_with_target_version_rewrites_project_version(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"

    worker.create_worker(
        source,
        destination,
        target_unity_version="6000.5.10f1",
        target_unity_revision="3bd4f66ad299",
    )

    text = (destination / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    assert "m_EditorVersion: 6000.5.10f1" in text


def test_create_worker_target_version_requires_both_fields(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    source = source_project(tmp_path)
    monkeypatch.setattr(worker, "REPO_ROOT", _repository(tmp_path))
    destination = tmp_path / "worker"

    with pytest.raises(worker.WorkerCreationError):
        worker.create_worker(source, destination, target_unity_version="6000.5.10f1")
