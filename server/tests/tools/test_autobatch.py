"""Tests for P3 macro tools: setup_objects, set_properties, configure_objects."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mock_send():
    """Mock _send that captures batch commands arg."""
    send = AsyncMock(return_value="batch_result")
    return send


@pytest.fixture(autouse=True)
def patch_send(mock_send):
    """Inject mock_send into autobatch module."""
    import unity_mcp.tools.autobatch as ab
    ab._send = mock_send
    yield
    ab._send = None


def _batch_cmds(mock_send) -> str:
    """Extract 'commands' from last batch call."""
    return mock_send.call_args[0][1]["commands"]


# ── setup_objects ─────────────────────────────────────────────────────────────

async def test_setup_objects_single(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("MyObj")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=MyObj" in cmds
    assert "inspect paths=/MyObj" in cmds


async def test_setup_objects_with_position(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("MyObj pos=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/MyObj component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds


async def test_setup_objects_with_primitive(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("NPC1 primitive=Capsule")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=NPC1 primitive=Capsule" in cmds


async def test_setup_objects_with_components(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("NPC1 components=Health,Rigidbody")
    cmds = _batch_cmds(mock_send)
    assert "manage_component path=/NPC1 type=Health action=add" in cmds
    assert "manage_component path=/NPC1 type=Rigidbody action=add" in cmds


async def test_setup_objects_multiple(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("A\nB primitive=Cube")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=A" in cmds
    assert "create_object name=B primitive=Cube" in cmds
    # inspect should contain both
    assert "/A" in cmds
    assert "/B" in cmds


async def test_setup_objects_empty(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    result = await setup_objects("   ")
    # Should not call send at all, return error message
    mock_send.assert_not_called()
    assert "No" in result


async def test_setup_objects_parent_double_slash_normalized(mock_send):
    """parent path containing // is not double-slashed in generated commands."""
    from unity_mcp.tools.autobatch import setup_objects
    # parent already has leading slash — should not produce //
    await setup_objects("Enemy parent=/World/Enemies")
    cmds = _batch_cmds(mock_send)
    assert "//" not in cmds
    assert "create_object name=Enemy" in cmds
    assert "parent=/World/Enemies" in cmds


# ── set_properties ────────────────────────────────────────────────────────────

async def test_set_properties_single(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/NPC1 component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds


async def test_set_properties_multiple(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)\nRigidbody.mass=5\nTransform.m_LocalScale=(2,2,2)")
    cmds = _batch_cmds(mock_send)
    assert cmds.count("set_property") == 3
    # read-back for Transform and Rigidbody
    assert "get_component path=/NPC1 type=Rigidbody" in cmds
    assert "get_component path=/NPC1 type=Transform" in cmds


async def test_set_properties_semicolons(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5")
    cmds = _batch_cmds(mock_send)
    assert cmds.count("set_property") == 2


async def test_set_properties_invalid(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    result = await set_properties("/NPC1", "no_dot_here=value")
    mock_send.assert_not_called()
    assert "No valid" in result


# ── configure_objects ─────────────────────────────────────────────────────────

async def test_configure_objects_multi(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/NPC1 Transform.m_LocalPosition=(1,0,0)\n/NPC2 Transform.m_LocalPosition=(3,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/NPC1 component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds
    assert "set_property path=/NPC2 component=Transform prop=m_LocalPosition value=(3,0,0)" in cmds
    assert "inspect paths=" in cmds


async def test_configure_objects_invalid(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    result = await configure_objects("NPC1 Transform.pos=(1,0,0)")  # no leading /
    mock_send.assert_not_called()
    assert "No valid" in result


async def test_configure_objects_multiple_props(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100")
    cmds = _batch_cmds(mock_send)
    assert "component=Transform" in cmds
    assert "component=Health" in cmds


# ── configure_objects: multi-scene path support (Bug 3) ───────────────────────

async def test_configure_accepts_scene_qualified_path(mock_send):
    """Bug 3: SceneName:/Player path must not be rejected."""
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("SceneName:/Player Transform.m_LocalPosition=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=SceneName:/Player" in cmds


async def test_configure_still_accepts_bare_path(mock_send):
    """/Player (no scene prefix) must still work after the fix."""
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/Player Transform.m_LocalPosition=(2,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/Player" in cmds


async def test_configure_rejects_invalid_line(mock_send):
    """Lines without a path token are silently skipped."""
    from unity_mcp.tools.autobatch import configure_objects
    result = await configure_objects("not_a_path Transform.x=1")
    mock_send.assert_not_called()
    assert "No valid" in result


async def test_configure_mixed_paths(mock_send):
    """Both bare and scene-qualified paths can appear in one call."""
    from unity_mcp.tools.autobatch import configure_objects
    config = (
        "/NPC1 Transform.m_LocalPosition=(1,0,0)\n"
        "Level2:/NPC2 Transform.m_LocalPosition=(3,0,0)"
    )
    await configure_objects(config)
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/NPC1" in cmds
    assert "set_property path=Level2:/NPC2" in cmds


# ── Fix 22: autobatch setup_objects full path with parent ─────────────────────

async def test_setup_objects_uses_full_path_when_parent_given():
    """Fix 22: set_property/manage_component must use full path when parent specified."""
    import unity_mcp.tools.autobatch as ab
    from unittest.mock import AsyncMock
    send = AsyncMock(return_value="ok")
    ab._send = send

    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=Root pos=(1,0,0)")

    cmds = send.call_args[0][1]["commands"]
    assert "path=/Root/Child" in cmds or "path=Root/Child" in cmds

    ab._send = None


# ── BUG B: spaced values in autobatch ────────────────────────────────────────

async def test_set_properties_value_with_spaces_is_quoted(mock_send):
    """set_properties must wrap spaced values in quotes so the batch line parses correctly."""
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/UI", "Image.sprite=Assets/bubble blue small.png")
    cmds = _batch_cmds(mock_send)
    assert 'value="Assets/bubble blue small.png"' in cmds


async def test_configure_objects_value_with_spaces_is_quoted(mock_send):
    """configure_objects must quote spaced values in the generated batch line."""
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects('/UI Image.sprite=Assets/bubble blue small.png')
    cmds = _batch_cmds(mock_send)
    assert 'value="Assets/bubble blue small.png"' in cmds


async def test_configure_objects_value_already_quoted_not_double_quoted(mock_send):
    """Already-quoted values must not be wrapped in an extra pair of quotes."""
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects('/UI Image.sprite="Assets/bubble blue small.png"')
    cmds = _batch_cmds(mock_send)
    assert 'value=""' not in cmds
    assert '"Assets/bubble blue small.png"' in cmds


async def test_set_properties_no_space_value_not_quoted(mock_send):
    """Paren values (no space issue) must NOT be wrapped in extra quotes."""
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert 'value=(1,0,0)' in cmds
    assert 'value="(1,0,0)"' not in cmds


# ── G11: setup_objects with parent=#ID creates invalid paths ─────────────────

async def test_g11_setup_objects_parent_instance_id_no_leading_slash(mock_send):
    """G11: parent=#12345 must NOT produce /#12345/Name — use #12345/Name instead."""
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=#12345")
    cmds = _batch_cmds(mock_send)
    # Must NOT have /#12345 (leading slash before instance ID)
    assert "/#12345" not in cmds, "Instance-ID parent must not get a leading slash prepended"
    # Must have #12345/Child as the object path
    assert "#12345/Child" in cmds, "Path should be #12345/Child, not /#12345/Child"


async def test_g11_setup_objects_parent_instance_id_with_pos(mock_send):
    """G11: set_property path after #ID parent must also be correct."""
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=#99 pos=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "path=#99/Child" in cmds, "set_property must use valid path #99/Child"
    assert "path=/#99" not in cmds, "No leading slash before instance-ID"


async def test_g11_setup_objects_regular_parent_unaffected(mock_send):
    """G11 fix must not break normal named parent paths."""
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=Root pos=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "/Root/Child" in cmds, "Normal parent should still produce /Root/Child path"
    assert "/#" not in cmds, "No instance-ID-like paths for a normal parent"


async def test_setup_objects_parent_ampersand_ref_no_leading_slash(mock_send):
    """parent=&ref (new short-ID) should NOT get / prepended."""
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=&abc")
    cmds = _batch_cmds(mock_send)
    assert "&abc" in cmds, "&abc must appear in batch commands"
    assert "/&abc" not in cmds, "&abc must not get a leading slash prepended"


async def test_autobatch_setup_uses_batch_timeout(mock_send):
    """Bug 2: autobatch must pass timeout= to _send to avoid indefinite hangs."""
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("MyObj")
    assert mock_send.call_args.kwargs.get("timeout") == 75.0


async def test_autobatch_set_properties_uses_batch_timeout(mock_send):
    """Bug 2: set_properties must pass timeout= to _send."""
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)")
    assert mock_send.call_args.kwargs.get("timeout") == 75.0


async def test_autobatch_configure_objects_uses_batch_timeout(mock_send):
    """Bug 2: configure_objects must pass timeout= to _send."""
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/NPC1 Transform.m_LocalPosition=(1,0,0)")
    assert mock_send.call_args.kwargs.get("timeout") == 75.0
