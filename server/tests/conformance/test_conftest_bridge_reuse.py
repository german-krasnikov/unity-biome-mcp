"""Unit tests for the conformance per-test teardown's bridge reuse.

`pytest_runtest_teardown` used to open a fresh `connect_bridge` on every
single `live`+`conformance` test's teardown. These tests pin the new
behavior: cache one bridge for the whole session, and reconnect only when
the cached bridge is no longer usable (closed/unreachable). No live Unity
worker is involved — `connect_bridge` and `ConformanceWorker` are faked.
"""

from types import SimpleNamespace

import pytest

from tests.conformance import conftest as conf_conftest


class _FakeWorker:
    """Effect spy: records every call so a silent early-return that reuses
    the cached bridge but skips the per-test cleanup work turns red."""

    work_calls: list[str] = []

    def __init__(self, **_kwargs):
        pass

    async def prove_absent(self, _bridge):
        _FakeWorker.work_calls.append("prove_absent")

    async def discard_if_dirty(self, _bridge):
        _FakeWorker.work_calls.append("discard_if_dirty")


class _FakeBridge:
    def __init__(self, connected=True):
        self.connected = connected

    async def close(self):
        self.connected = False


def _dummy_live_conformance_item():
    return SimpleNamespace(keywords={"live": True, "conformance": True})


@pytest.fixture(autouse=True)
def _fresh_session_bridge_holder(monkeypatch):
    """Each test starts from a pristine holder — the real module state is a
    process-lifetime singleton, not something tests should share."""
    monkeypatch.setattr(conf_conftest, "_session_bridge", conf_conftest._SessionBridgeHolder())


@pytest.fixture(autouse=True)
def _fresh_fake_worker_calls(monkeypatch):
    """Each test starts from an empty spy log — `work_calls` is a class
    variable shared across `_FakeWorker` instances by design."""
    monkeypatch.setattr(_FakeWorker, "work_calls", [])


def _patch_common(monkeypatch, fake_connect_bridge):
    monkeypatch.setattr(conf_conftest, "connect_bridge", fake_connect_bridge)
    monkeypatch.setattr(conf_conftest, "CONF_PROJECT", "FakeProject")
    monkeypatch.setattr(conf_conftest, "ConformanceWorker", _FakeWorker)


def test_teardown_reuses_cached_bridge_across_dummy_items(monkeypatch):
    connect_calls = []

    async def fake_connect_bridge(host, port, project):
        connect_calls.append((host, port, project))
        return _FakeBridge()

    _patch_common(monkeypatch, fake_connect_bridge)

    for _ in range(3):
        conf_conftest.pytest_runtest_teardown(_dummy_live_conformance_item(), None)

    assert len(connect_calls) <= 1
    assert _FakeWorker.work_calls == ["prove_absent", "discard_if_dirty"] * 3


def test_teardown_reconnects_when_cached_bridge_is_no_longer_usable(monkeypatch):
    connect_calls = []

    async def fake_connect_bridge(host, port, project):
        connect_calls.append((host, port, project))
        return _FakeBridge(connected=True)

    _patch_common(monkeypatch, fake_connect_bridge)
    conf_conftest._session_bridge._bridge = _FakeBridge(connected=False)

    conf_conftest.pytest_runtest_teardown(_dummy_live_conformance_item(), None)

    assert len(connect_calls) == 1
    assert conf_conftest._session_bridge._bridge is not None
    assert conf_conftest._session_bridge._bridge.connected is True
    assert _FakeWorker.work_calls == ["prove_absent", "discard_if_dirty"]
