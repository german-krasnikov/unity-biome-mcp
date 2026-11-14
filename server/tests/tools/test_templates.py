"""Tests for scene templates and spatial_query tool."""
import os
from unittest.mock import AsyncMock, patch
from unity_mcp.server import apply_template, save_template, list_templates, spatial_query


async def test_apply_template_not_found(tmp_path, mock_bridge):
    """Returns available list when template not found."""
    tdir = tmp_path / ".claude" / "templates"
    tdir.mkdir(parents=True)
    (tdir / "other.cs").write_text("// code", encoding="utf-8")
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await apply_template("missing")
    assert "missing" in result
    assert "other" in result


async def test_apply_template_no_templates_dir(tmp_path, mock_bridge):
    """Returns helpful message when templates dir missing."""
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await apply_template("setup")
    assert "templates" in result.lower()


async def test_save_template_creates_file(tmp_path, mock_bridge):
    """File exists after save_template."""
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await save_template("level_setup", "var go = new GameObject();")
    expected = tmp_path / ".claude" / "templates" / "level_setup.cs"
    assert expected.exists()
    assert expected.read_text(encoding="utf-8") == "var go = new GameObject();"
    assert "level_setup" in result


async def test_list_templates(tmp_path, mock_bridge):
    """Returns saved template names."""
    tdir = tmp_path / ".claude" / "templates"
    tdir.mkdir(parents=True)
    (tdir / "level_setup.cs").write_text("// level", encoding="utf-8")
    (tdir / "arena.cs").write_text("// arena", encoding="utf-8")
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await list_templates()
    assert "level_setup" in result
    assert "arena" in result


async def test_list_templates_empty(tmp_path, mock_bridge):
    """Returns friendly message when no templates yet."""
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await list_templates()
    assert "template" in result.lower()


async def test_apply_template_with_params(tmp_path, mock_bridge):
    """Replaces ${key} placeholders in template code."""
    tdir = tmp_path / ".claude" / "templates"
    tdir.mkdir(parents=True)
    (tdir / "spawn.cs").write_text("var go = new GameObject(\"${name}\"); go.transform.position = ${pos};", encoding="utf-8")
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        await apply_template("spawn", "name=Player,pos=(0,1,0)")
    call_args = mock_bridge.send.call_args[0][1]
    assert "Player" in call_args["code"]
    assert "(0,1,0)" in call_args["code"]
    assert "${name}" not in call_args["code"]


async def test_apply_template_calls_execute_code(tmp_path, mock_bridge):
    """apply_template sends execute_code command."""
    tdir = tmp_path / ".claude" / "templates"
    tdir.mkdir(parents=True)
    (tdir / "setup.cs").write_text("return \"done\";", encoding="utf-8")
    mock_bridge.send.return_value = {"ok": True, "data": "done"}
    with patch("unity_mcp.tools.skills.os.getcwd", return_value=str(tmp_path)):
        result = await apply_template("setup")
    mock_bridge.send.assert_called_once()
    assert mock_bridge.send.call_args[0][0] == "execute_code"
    assert result == "done"


async def test_spatial_query_nearest(mock_bridge):
    """spatial_query nearest sends correct args."""
    mock_bridge.send.return_value = {"ok": True, "data": "/Enemy dist=3.14 pos=(1,0,0)"}
    result = await spatial_query(action="nearest", path="/Player", component="Enemy")
    mock_bridge.send.assert_called_once_with(
        "spatial_query",
        {"action": "nearest", "path": "/Player", "component": "Enemy"},
        timeout=30.0,
    )
    assert "dist=" in result


async def test_spatial_query_in_front_of(mock_bridge):
    """spatial_query in_front_of sends distance arg."""
    mock_bridge.send.return_value = {"ok": True, "data": "(0.00,0.00,5.00)"}
    result = await spatial_query(action="in_front_of", path="/Player", distance=5.0)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "in_front_of"
    assert sent["distance"] == "5.0"
    assert "path" in sent


async def test_spatial_query_objects_in_radius(mock_bridge):
    """spatial_query objects_in_radius sends radius arg."""
    mock_bridge.send.return_value = {"ok": True, "data": "3 objects within 10m"}
    result = await spatial_query(action="objects_in_radius", path="/Player", radius=10.0)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "objects_in_radius"
    assert sent["radius"] == "10.0"


async def test_spatial_query_bounds_info(mock_bridge):
    """spatial_query bounds_info sends correct command."""
    mock_bridge.send.return_value = {"ok": True, "data": "center=(0,1,0) size=(2,2,2)"}
    result = await spatial_query(action="bounds_info", path="/Player")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "bounds_info"
    assert sent["path"] == "/Player"
    assert "center" in result
