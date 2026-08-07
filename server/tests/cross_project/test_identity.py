from __future__ import annotations

import pytest

from conformance.workers import _parse_status

pytestmark = [pytest.mark.live, pytest.mark.cross_project, pytest.mark.asyncio(loop_scope="session")]


async def test_workers_have_different_ports(dual_worker_session):
    worker_a, _, worker_b, _ = dual_worker_session
    assert worker_a.port != worker_b.port, \
        f"Workers share port {worker_a.port} — not independent"


async def test_status_reports_correct_port_a(dual_worker_session):
    worker_a, bridge_a, _, _ = dual_worker_session
    resp = await bridge_a.send("get_status", {})
    assert resp["ok"], f"get_status failed on A: {resp}"
    info = _parse_status(resp["data"])
    assert info.get("port") == str(worker_a.port), \
        f"Worker A reported port {info.get('port')}, expected {worker_a.port}"


async def test_status_reports_correct_port_b(dual_worker_session):
    _, _, worker_b, bridge_b = dual_worker_session
    resp = await bridge_b.send("get_status", {})
    assert resp["ok"], f"get_status failed on B: {resp}"
    info = _parse_status(resp["data"])
    assert info.get("port") == str(worker_b.port), \
        f"Worker B reported port {info.get('port')}, expected {worker_b.port}"
