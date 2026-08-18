
import pytest
from conformance.workers import _parse_status

from unity_mcp.bridge import UnityBridge

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


async def test_aba_reconnect_keeps_project_identity(dual_worker_session):
    worker_a, bridge_a, worker_b, bridge_b = dual_worker_session

    for worker in (worker_a, worker_b, worker_a):
        bridge = UnityBridge(
            "127.0.0.1",
            port=worker.port,
            expected_project_path=worker.project_path,
        )
        await bridge.connect()
        try:
            resp = await bridge.send("get_status", {})
            assert resp["ok"], f"get_status failed on port {worker.port}: {resp}"
            assert _parse_status(resp["data"]).get("port") == str(worker.port)
        finally:
            await bridge.close()

    resp_a = await bridge_a.send("get_status", {})
    resp_b = await bridge_b.send("get_status", {})
    assert resp_a["ok"], f"Worker A existing bridge died after ABA reconnect: {resp_a}"
    assert resp_b["ok"], f"Worker B existing bridge died after ABA reconnect: {resp_b}"
