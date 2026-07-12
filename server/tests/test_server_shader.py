import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import shader


async def test_shader_get_sends_action_and_path(mock_bridge):
    """shader get forwards action and path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: Standard\nproperties:\n  _Color: Color"})
    result = await shader(action="get", path="/Cube")
    mock_bridge.send.assert_called_once_with(
        "shader",
        {"action": "get", "path": "/Cube"},
        timeout=30.0,
    )
    assert "Standard" in result


async def test_shader_get_material_sends_target(mock_bridge):
    """shader get with target=material includes target in args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material on '/Cube'\nshader: Standard"})
    result = await shader(action="get", path="/Cube", target="material")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["target"] == "material"
    assert "Material" in result


async def test_shader_excludes_none_params(mock_bridge):
    """None parameters not sent to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await shader(action="get", path="/Cube")
    call_args = mock_bridge.send.call_args[0]
    assert "preset" not in call_args[1]
    assert "code" not in call_args[1]
    assert "keyword" not in call_args[1]


async def test_shader_error_raises_tool_error(mock_bridge):
    """Bridge error raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Shader not found"})
    with pytest.raises(ToolError, match="Shader not found"):
        await shader(action="get", path="/NonExistent")


async def test_shader_create_with_preset(mock_bridge):
    """shader create with preset forwards all args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: \"Custom/Test\"\nproperties:\n  _Color: Color"})
    result = await shader(action="create", path="Assets/Shaders/Test.shader", preset="unlit")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "create"
    assert call_args[1]["preset"] == "unlit"
    assert call_args[1]["path"] == "Assets/Shaders/Test.shader"
    assert "Shader:" in result


async def test_shader_create_with_code(mock_bridge):
    """shader create with custom code forwards code param."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: \"Custom/Test\""})
    code = 'Shader "Test" { SubShader { Pass {} } }'
    result = await shader(action="create", path="Assets/Shaders/Test.shader", code=code)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["code"] == code
    assert "preset" not in call_args[1]


async def test_shader_set_material_property(mock_bridge):
    """shader set forwards prop and value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "_Color=#FF0000 on /Cube"})
    result = await shader(action="set", path="/Cube", prop="_Color", value="#FF0000")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "set"
    assert call_args[1]["prop"] == "_Color"
    assert call_args[1]["value"] == "#FF0000"
    assert "_Color" in result


async def test_shader_set_keyword(mock_bridge):
    """shader set keyword forwards keyword and enabled."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "keyword _EMISSION enabled on /Cube"})
    result = await shader(action="set", path="/Cube", keyword="_EMISSION", enabled="true")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["keyword"] == "_EMISSION"
    assert call_args[1]["enabled"] == "true"
    assert "prop" not in call_args[1]


# Phase 20c tests

async def test_shader_graph_get_sends_path(mock_bridge):
    """shader graph_get forwards path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: Assets/Shaders/Test.shadergraph\nnodes: 3\nedges: 2"})
    result = await shader(action="graph_get", path="Assets/Shaders/Test.shadergraph")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_get"
    assert call_args[1]["path"] == "Assets/Shaders/Test.shadergraph"
    assert "ShaderGraph:" in result


async def test_shader_graph_get_returns_nodes(mock_bridge):
    """shader graph_get result includes node list."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: Assets/Test.shadergraph\nnodes: 2\n  [abc] ColorNode\n  [def] MultiplyNode\nedges: 1"})
    result = await shader(action="graph_get", path="Assets/Test.shadergraph")
    assert "ColorNode" in result
    assert "MultiplyNode" in result


async def test_shader_graph_create_sends_preset(mock_bridge):
    """shader graph_create forwards preset to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: Assets/Test.shadergraph\nnodes: 2"})
    result = await shader(action="graph_create", path="Assets/Shaders/Test.shadergraph", preset="unlit_graph")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_create"
    assert call_args[1]["preset"] == "unlit_graph"


async def test_shader_graph_create_returns_data(mock_bridge):
    """shader graph_create returns created graph info."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: Assets/New.shadergraph\nnodes: 3\nedges: 2\nproperties: 1"})
    result = await shader(action="graph_create", path="Assets/Shaders/New.shadergraph", preset="lit_graph")
    assert "ShaderGraph:" in result
    assert "nodes:" in result


# Phase 20d tests

async def test_shader_graph_node_add_sends_type(mock_bridge):
    """shader graph_node add forwards node_type."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: test.shadergraph\nnodes: 8\n  [abc] MultiplyNode"})
    result = await shader(action="graph_node", path="Assets/Test.shadergraph", node_type="MultiplyNode", node_action="add")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_node"
    assert call_args[1]["node_type"] == "MultiplyNode"
    assert call_args[1]["node_action"] == "add"
    assert "MultiplyNode" in result


async def test_shader_graph_node_remove_sends_id(mock_bridge):
    """shader graph_node remove forwards node_id."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: test.shadergraph\nnodes: 6"})
    result = await shader(action="graph_node", path="Assets/Test.shadergraph", node_id="abc123", node_action="remove")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["node_id"] == "abc123"
    assert call_args[1]["node_action"] == "remove"
    assert "node_type" not in call_args[1]


async def test_shader_graph_edge_add_sends_slots(mock_bridge):
    """shader graph_edge add forwards output/input node and slot."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: test.shadergraph\nedges: 5"})
    result = await shader(action="graph_edge", path="Assets/Test.shadergraph",
                        output_node="node1", output_slot=0, input_node="node2", input_slot=1, edge_action="add")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["output_node"] == "node1"
    assert call_args[1]["output_slot"] == 0
    assert call_args[1]["input_node"] == "node2"
    assert call_args[1]["input_slot"] == 1


async def test_shader_graph_edge_remove(mock_bridge):
    """shader graph_edge remove forwards edge_action=remove."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: test.shadergraph\nedges: 3"})
    result = await shader(action="graph_edge", path="Assets/Test.shadergraph",
                        output_node="n1", output_slot=2, input_node="n2", input_slot=0, edge_action="remove")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["edge_action"] == "remove"
    assert "edges:" in result


# New tests

async def test_shader_create_overwrite(mock_bridge):
    """create with preset=lit called twice on same path — both calls go through."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: \"Custom/MyShader\""})
    path = "Assets/Shaders/MyShader.shader"

    result1 = await shader(action="create", path=path, preset="lit")
    result2 = await shader(action="create", path=path, preset="lit")

    assert mock_bridge.send.call_count == 2
    for call in mock_bridge.send.call_args_list:
        args = call[0]
        assert args[1]["action"] == "create"
        assert args[1]["path"] == path
        assert args[1]["preset"] == "lit"
    assert "Shader:" in result1
    assert "Shader:" in result2


async def test_shader_create_with_shader_name(mock_bridge):
    """create with preset=unlit and shader_name sends shader_name to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: \"MyProject/Glow\""})
    result = await shader(action="create", path="Assets/Shaders/Glow.shader",
                          preset="unlit", shader_name="MyProject/Glow")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "create"
    assert call_args[1]["preset"] == "unlit"
    assert call_args[1]["shader_name"] == "MyProject/Glow"
    assert "Glow" in result



async def test_shader_set_missing_prop_and_value_raises_tool_error(mock_bridge):
    """set without prop/value and without keyword/enabled raises ToolError from bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Missing prop or keyword"})
    with pytest.raises(ToolError, match="Missing prop or keyword"):
        await shader(action="set", path="/Cube")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1] == {"action": "set", "path": "/Cube"}


# L4: Shader Graph blackboard properties CRUD

async def test_shader_graph_add_property_sends_name_type(mock_bridge):
    """graph_add_property forwards name and type to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: x.shadergraph\nproperties: 1\n  Speed: Float (_Speed)"})
    result = await shader(action="graph_add_property", path="Assets/MyShader.shadergraph",
                          name="Speed", type="Float")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_add_property"
    assert call_args[1]["name"] == "Speed"
    assert call_args[1]["type"] == "Float"
    assert "properties:" in result


async def test_shader_graph_remove_property_sends_name(mock_bridge):
    """graph_remove_property forwards name to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "property removed: Speed"})
    result = await shader(action="graph_remove_property", path="Assets/MyShader.shadergraph",
                          name="Speed")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_remove_property"
    assert call_args[1]["name"] == "Speed"
    assert "removed" in result


async def test_shader_graph_rename_property_sends_names(mock_bridge):
    """graph_rename_property forwards name and new_name to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "property renamed: Speed → MovementSpeed"})
    result = await shader(action="graph_rename_property", path="Assets/MyShader.shadergraph",
                          name="Speed", new_name="MovementSpeed")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "graph_rename_property"
    assert call_args[1]["name"] == "Speed"
    assert call_args[1]["new_name"] == "MovementSpeed"
    assert "MovementSpeed" in result


async def test_shader_graph_add_property_default_type_omitted(mock_bridge):
    """graph_add_property without type does not send type to bridge (C# defaults to Float)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ShaderGraph: x.shadergraph\nproperties: 1"})
    await shader(action="graph_add_property", path="Assets/MyShader.shadergraph", name="X")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["name"] == "X"
    assert "type" not in call_args[1]
