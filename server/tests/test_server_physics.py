"""Physics component tests for MCP tools."""
import pytest
from unittest.mock import AsyncMock

from unity_mcp.server import manage_component, set_property, find_objects, create_object, batch


# --- manage_component: add physics components ---

@pytest.mark.parametrize("component_type", [
    "Rigidbody", "SphereCollider", "CapsuleCollider", "MeshCollider",
    "CharacterController", "HingeJoint", "SpringJoint", "FixedJoint",
])
async def test_manage_component_add_physics(mock_bridge, component_type):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Added"})
    await manage_component(path="/Obj", type=component_type, action="add")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Obj"
    assert args["type"] == component_type
    assert args["action"] == "add"


# --- set_property: Rigidbody and collider properties ---

async def test_set_property_rigidbody_mass(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="Rigidbody", prop="m_Mass", value="50")
    args = mock_bridge.send.call_args[0][1]
    assert args["component"] == "Rigidbody"
    assert args["prop"] == "m_Mass"
    assert args["value"] == "50"


async def test_set_property_rigidbody_kinematic(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="Rigidbody", prop="m_IsKinematic", value="true")
    args = mock_bridge.send.call_args[0][1]
    assert args["prop"] == "m_IsKinematic"
    assert args["value"] == "true"


async def test_set_property_rigidbody_constraints(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="Rigidbody", prop="m_Constraints", value="112")
    args = mock_bridge.send.call_args[0][1]
    assert args["prop"] == "m_Constraints"
    assert args["value"] == "112"


async def test_set_property_collider_trigger(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="SphereCollider", prop="m_IsTrigger", value="true")
    args = mock_bridge.send.call_args[0][1]
    assert args["component"] == "SphereCollider"
    assert args["prop"] == "m_IsTrigger"
    assert args["value"] == "true"


async def test_set_property_collider_size(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="BoxCollider", prop="m_Size", value="(2,3,4)")
    args = mock_bridge.send.call_args[0][1]
    assert args["component"] == "BoxCollider"
    assert args["prop"] == "m_Size"
    assert args["value"] == "(2,3,4)"


async def test_set_property_collision_detection(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="Rigidbody", prop="m_CollisionDetection", value="Continuous Dynamic")
    args = mock_bridge.send.call_args[0][1]
    assert args["prop"] == "m_CollisionDetection"
    assert args["value"] == "Continuous Dynamic"


async def test_set_property_character_controller(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="CharacterController", prop="m_Height", value="2")
    args = mock_bridge.send.call_args[0][1]
    assert args["component"] == "CharacterController"
    assert args["prop"] == "m_Height"
    assert args["value"] == "2"


async def test_set_property_hinge_joint_axis(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="HingeJoint", prop="m_Axis", value="(0,1,0)")
    args = mock_bridge.send.call_args[0][1]
    assert args["component"] == "HingeJoint"
    assert args["prop"] == "m_Axis"
    assert args["value"] == "(0,1,0)"


# --- find_objects: filter by physics component ---

async def test_find_objects_by_rigidbody(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Results"})
    await find_objects(component="Rigidbody")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"component": "Rigidbody"}


async def test_find_objects_by_sphere_collider(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Results"})
    await find_objects(component="SphereCollider")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"component": "SphereCollider"}


# --- create_object with physics components ---

async def test_create_object_with_physics_components(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await create_object(name="PhysObj", components="Rigidbody,BoxCollider,SphereCollider")
    args = mock_bridge.send.call_args[0][1]
    assert args["name"] == "PhysObj"
    assert args["components"] == "Rigidbody,BoxCollider,SphereCollider"


# --- batch: physics workflow ---

async def test_batch_physics_workflow(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[0] ok: /Obj\n[1] ok\n[2] ok"})
    commands = (
        "create_object name=Obj primitive=Cube\n"
        "manage_component path=/Obj type=Rigidbody action=add\n"
        "set_property path=/Obj component=Rigidbody prop=m_Mass value=10"
    )
    result = await batch(commands=commands)
    # A4: default timeout=75.0 -> timeout_ms=70000, no longer matches C#'s
    # hardcoded 25000ms default, so it's now sent explicitly.
    mock_bridge.send.assert_called_once_with(
        "batch",
        {"commands": commands, "timeout_ms": 70000},
        timeout=75.0,
    )
    assert "[0] ok" in result
    assert "[1] ok" in result
    assert "[2] ok" in result
