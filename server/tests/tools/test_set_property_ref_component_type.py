"""TDD — P-258b: ref_component_type embeds ::TypeName into the value string before TCP send."""
import pytest
from unittest.mock import AsyncMock


def _make_args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


def _setup(monkeypatch):
    from unity_mcp.tools import objects
    mock = AsyncMock(return_value="m_ConnectedBody = /Enemy::BoxCollider #999")
    monkeypatch.setattr(objects, "_send", mock)
    monkeypatch.setattr(objects, "_args", _make_args)
    return mock


@pytest.mark.asyncio
async def test_ref_component_type_embeds_in_value(monkeypatch):
    """P-258b: ref_component_type embeds as '::TypeName' suffix in the value sent to C#."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    await set_property(path="/Hero", component="HeroController",
                       prop="m_Target", value="/Enemy",
                       ref_component_type="BoxCollider")
    call_args = mock.call_args[0][1]
    assert call_args.get("value") == "/Enemy::BoxCollider"
    assert "ref_component_type" not in call_args


@pytest.mark.asyncio
async def test_ref_component_type_none_no_change(monkeypatch):
    """P-258b: ref_component_type=None → value is unchanged (backward compat)."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    await set_property(path="/Player", component="Transform",
                       prop="m_LocalPosition", value="(1,2,3)")
    call_args = mock.call_args[0][1]
    assert call_args.get("value") == "(1,2,3)"
    assert "ref_component_type" not in call_args


@pytest.mark.asyncio
async def test_ref_component_type_empty_no_change(monkeypatch):
    """P-258b: empty string ref_component_type treated as None — value unchanged."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    await set_property(path="/Player", component="HeroController",
                       prop="m_Target", value="/Enemy",
                       ref_component_type="")
    call_args = mock.call_args[0][1]
    assert call_args.get("value") == "/Enemy"
    assert "ref_component_type" not in call_args


@pytest.mark.asyncio
async def test_ref_component_type_works_with_find_type(monkeypatch):
    """P-258b: ref_component_type embeds correctly alongside find_type bulk mode."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    await set_property(find_type="HeroController", prop="m_Target",
                       value="/Enemy", ref_component_type="BoxCollider")
    call_args = mock.call_args[0][1]
    assert call_args.get("value") == "/Enemy::BoxCollider"
    assert call_args.get("find_type") == "HeroController"
    assert "ref_component_type" not in call_args


@pytest.mark.asyncio
async def test_ref_component_type_already_has_wire_id_no_double_embed(monkeypatch):
    """P-258b: if value already has '::' (wire format), don't embed again."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    wire_value = "/Enemy::BoxCollider #12345"
    await set_property(path="/Hero", component="HeroController",
                       prop="m_Target", value=wire_value,
                       ref_component_type="Rigidbody")
    call_args = mock.call_args[0][1]
    # Wire format already has ::, don't embed ref_component_type
    assert call_args.get("value") == wire_value
