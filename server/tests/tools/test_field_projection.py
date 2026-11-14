"""F07: get_component/inspect `fields=` projection wiring (tool level). D4: full= param."""
import pytest
from unity_mcp.tools import objects
from unity_mcp.tools import scene


def _args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


@pytest.fixture
def fake_send(monkeypatch):
    sent = {}
    COMP = "[Transform]\nm_LocalPosition.x: 5\nm_Mass: 2\nm_Drag: 0\n"

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return COMP

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    return sent


async def test_get_component_fields_projects_and_sets_no_strip(fake_send):
    result = await objects.get_component("/Cube", "Transform", fields="m_Mass")
    assert fake_send["args"].get("_no_strip") is True, "explicit fields must bypass default-stripping"
    assert "m_Mass: 2" in result
    assert "m_LocalPosition" not in result and "m_Drag" not in result


async def test_get_component_no_fields_no_strip_flag(fake_send):
    await objects.get_component("/Cube", "Transform")
    assert "_no_strip" not in fake_send["args"], "default path must not force _no_strip"


async def test_inspect_fields_projects(fake_send):
    result = await objects.inspect("/A,/B", fields="m_Mass")
    assert fake_send["args"].get("_no_strip") is True
    assert "m_Mass: 2" in result
    assert "m_Drag" not in result


# ── D4: full= parameter ────────────────────────────────────────────────────────

@pytest.fixture
def fake_send_scene(monkeypatch):
    sent = {}
    HIER = "Scene\n/Player\n/Enemy\n"

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return HIER

    monkeypatch.setattr(scene, "_send", send_fn)
    monkeypatch.setattr(scene, "_args", _args)
    return sent


async def test_get_component_full_sets_no_distill(monkeypatch):
    sent = {}

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return "[Transform]\nm_Mass: 2\n"

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    await objects.get_component("/Cube", "Transform", full=True)
    assert sent["args"].get("_no_distill") is True


async def test_get_component_full_false_no_flag(monkeypatch):
    sent = {}

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return "[Transform]\nm_Mass: 2\n"

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    await objects.get_component("/Cube", "Transform", full=False)
    assert "_no_distill" not in sent["args"]


async def test_inspect_full_sets_no_distill(monkeypatch):
    sent = {}

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return "[Transform]\nm_Mass: 2\n"

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    await objects.inspect("/A,/B", full=True)
    assert sent["args"].get("_no_distill") is True


async def test_get_object_detail_full_sets_no_distill(monkeypatch):
    sent = {}

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return "detail output\n"

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    await objects.get_object_detail(42, full=True)
    assert sent["args"].get("_no_distill") is True


async def test_get_hierarchy_full_sets_no_distill(fake_send_scene):
    await scene.get_hierarchy(full=True)
    assert fake_send_scene["args"].get("_no_distill") is True


async def test_get_hierarchy_full_false_no_flag(fake_send_scene):
    await scene.get_hierarchy(full=False)
    assert "_no_distill" not in fake_send_scene["args"]
