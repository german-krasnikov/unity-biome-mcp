"""TDD coverage for gauntlet.fetch_adapter_sources: happy-path fetch+verify,
fail-closed hash mismatch (no partial cache), fail-closed missing file, and
--update-lock regeneration. No network -- an injected fetcher stands in for
raw.githubusercontent.com. See Plans/MUTATION-REGRESSION-MODULE.md Phase C1.
"""
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))
import gauntlet.fetch_adapter_sources as fas  # noqa: E402

# 40-hex SHA ref -> resolve_provider_ref takes the passthrough path, no
# subprocess/git call, so these tests need no network and no git mock.
SHA = "b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf"
PIN = {
    "schema_version": 2,
    "package_name": "com.handzlikchris.fastscriptreload",
    "git_url": "https://github.com/german-krasnikov/FastScriptReload.git?path=/Assets",
    "ref": SHA,
    "ref_kind": "sha",
}


def _write_pin(tmp_path: Path) -> Path:
    path = tmp_path / "pin.json"
    path.write_text(json.dumps(PIN), encoding="utf-8")
    return path


def _fake_bytes(filename: str) -> bytes:
    return f"// fake content for {filename}\n".encode()


def _fake_fetcher(urls_seen: list[str]):
    def _fetch(url: str) -> bytes:
        urls_seen.append(url)
        filename = url.rsplit("/", 1)[-1]
        return _fake_bytes(filename)

    return _fetch


def test_fetch_adapter_sources_happy_path_writes_and_verifies(tmp_path):
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"
    urls_seen: list[str] = []

    # First call generates the lock from a real (fake) fetch.
    result = fas.fetch_adapter_sources(
        pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=True, fetcher=_fake_fetcher(urls_seen)
    )
    assert result["source_sha"] == SHA
    assert set(result["files"]) == set(fas.ADAPTER_FILES)
    for filename in fas.ADAPTER_FILES:
        assert (out_dir / filename).read_bytes() == _fake_bytes(filename)
    assert len(urls_seen) == len(fas.ADAPTER_FILES)
    assert all(url.startswith("https://raw.githubusercontent.com/") for url in urls_seen)

    # Second call verifies against the just-written lock -- must pass clean.
    out_dir_2 = tmp_path / "adapter-cache-2"
    result2 = fas.fetch_adapter_sources(
        pin_path=pin_path, out_dir=out_dir_2, lock_path=lock_path, update_lock=False, fetcher=_fake_fetcher([])
    )
    assert result2["files"] == result["files"]


def test_fetch_adapter_sources_hash_mismatch_rejected_no_partial_cache(tmp_path):
    """Double-red: if the sha256 verification in _verify_against_lock is
    removed (or its raise deleted), this test goes from RED to falsely
    GREEN -- it would silently accept drifted adapter content."""
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"

    tampered = dict.fromkeys(fas.ADAPTER_FILES, "0" * 64)
    lock_path.write_text(
        json.dumps({"schema_version": 1, "source_sha": SHA, "files": tampered}), encoding="utf-8"
    )

    with pytest.raises(fas.AdapterFetchError, match="mismatch"):
        fas.fetch_adapter_sources(
            pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=False, fetcher=_fake_fetcher([])
        )

    assert not out_dir.exists()


def test_fetch_adapter_sources_missing_file_rejected_no_partial_cache(tmp_path):
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"

    def _flaky_fetch(url: str) -> bytes:
        if "BiomeSingleFlightGate.cs" in url:
            raise fas.AdapterFetchError(f"404 for {url}")
        return _fake_bytes(url.rsplit("/", 1)[-1])

    with pytest.raises(fas.AdapterFetchError, match="BiomeSingleFlightGate"):
        fas.fetch_adapter_sources(
            pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=True, fetcher=_flaky_fetch
        )

    assert not out_dir.exists()
    assert not lock_path.exists()


def test_fetch_adapter_sources_malformed_lock_rejected(tmp_path):
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"
    lock_path.write_text("{not valid json", encoding="utf-8")

    with pytest.raises(fas.AdapterFetchError, match="not valid JSON"):
        fas.fetch_adapter_sources(
            pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=False, fetcher=_fake_fetcher([])
        )

    assert not out_dir.exists()


def test_fetch_adapter_sources_malformed_pin_rejected(tmp_path):
    pin_path = tmp_path / "pin.json"
    pin_path.write_text("{not valid json", encoding="utf-8")
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"

    with pytest.raises(fas.AdapterFetchError, match="not valid JSON"):
        fas.fetch_adapter_sources(
            pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=False, fetcher=_fake_fetcher([])
        )

    assert not out_dir.exists()


def test_fetch_adapter_sources_write_failure_leaves_no_partial_cache_or_tmp(tmp_path, monkeypatch):
    """Double-red: if the atomic swap in _write_out_dir_atomic is removed
    (writing straight into out_dir again), this test goes RED-for-the-wrong-
    reason -> falsely GREEN on the partial-cache assertion."""
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"
    tmp_sibling = tmp_path / "adapter-cache.tmp"

    # 3rd file in ADAPTER_FILES insertion order.
    failing_filename = fas.ADAPTER_FILES[2]
    original_write_bytes = Path.write_bytes

    def _flaky_write_bytes(self, data):
        if self.name == failing_filename:
            raise OSError(f"injected write failure for {self.name}")
        return original_write_bytes(self, data)

    monkeypatch.setattr(Path, "write_bytes", _flaky_write_bytes)

    with pytest.raises(OSError, match="injected write failure"):
        fas.fetch_adapter_sources(
            pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=True, fetcher=_fake_fetcher([])
        )

    assert not out_dir.exists()
    assert not tmp_sibling.exists()


def test_fetch_adapter_sources_update_lock_rewrites_hashes(tmp_path):
    pin_path = _write_pin(tmp_path)
    out_dir = tmp_path / "adapter-cache"
    lock_path = tmp_path / "adapter-sources.lock.json"
    lock_path.write_text(
        json.dumps({"schema_version": 1, "source_sha": "stale", "files": dict.fromkeys(fas.ADAPTER_FILES, "0" * 64)}),
        encoding="utf-8",
    )

    fas.fetch_adapter_sources(
        pin_path=pin_path, out_dir=out_dir, lock_path=lock_path, update_lock=True, fetcher=_fake_fetcher([])
    )

    rewritten = json.loads(lock_path.read_text(encoding="utf-8"))
    assert rewritten["source_sha"] == SHA
    for filename in fas.ADAPTER_FILES:
        assert rewritten["files"][filename] == fas._sha256_hex(_fake_bytes(filename))  # noqa: SLF001
