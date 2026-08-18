
from conformance.workers import ConformanceWorker, _parse_status


def test_dual_worker_different_run_ids():
    a = ConformanceWorker(port=9500, project_path="/proj_a")
    b = ConformanceWorker(port=9600, project_path="/proj_b")
    assert a.run_id != b.run_id


def test_dual_worker_scene_ns_isolation():
    a = ConformanceWorker(port=9500, project_path="/proj_a", run_id="aaa111")
    b = ConformanceWorker(port=9600, project_path="/proj_b", run_id="bbb222")
    assert a.scene_ns != b.scene_ns
    assert not a.scene_ns.startswith(b.scene_ns)
    assert not b.scene_ns.startswith(a.scene_ns)


def test_parse_status_multiple_workers():
    status_a = "scene=SampleScene\ndirty=false\nplaying=false\ncompiling=false\nport=9500\naliases=0"
    status_b = "scene=SampleScene\ndirty=false\nplaying=false\ncompiling=false\nport=9600\naliases=0"

    info_a = _parse_status(status_a)
    info_b = _parse_status(status_b)

    assert info_a["port"] == "9500"
    assert info_b["port"] == "9600"
    assert info_a["port"] != info_b["port"]
