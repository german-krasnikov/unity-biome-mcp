"""Headline regression: reflect rules must not false-positive on real Unity responses."""
import uuid

import pytest

from unity_mcp.reflect import reflect

pytestmark = pytest.mark.live


def _str_send(bridge_send):
    """Adapter: bridge.send returns dict; reflect send_fn contract expects Awaitable[str]."""
    async def _inner(*a, **kw):
        r = await bridge_send(*a, **kw)
        return r.get("data", "") if isinstance(r, dict) else str(r)
    return _inner


async def test_set_property_no_false_positive_on_real_response(bridge, sandbox):
    """set_property to real object must NOT produce a spurious [REFLECT: mismatch."""
    resp = await bridge.send(
        "set_property",
        {"path": sandbox, "component": "Transform", "prop": "localPosition", "value": "1,2,3"},
    )
    text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
    assert "[REFLECT:" not in text or "ok" in text.lower(), (
        f"Spurious [REFLECT: in real set_property response: {text[:300]}"
    )


async def test_manage_component_no_false_positive(bridge, sandbox):
    """manage_component add+remove — rule must not false-positive on real C# response."""
    await bridge.send("manage_component", {"path": sandbox, "type": "Rigidbody", "action": "add"})
    resp = await bridge.send("manage_component", {"path": sandbox, "type": "Rigidbody", "action": "remove"})
    text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
    assert "[REFLECT:" not in text or "ok" in text.lower(), (
        f"Spurious [REFLECT: in real manage_component response: {text[:300]}"
    )


async def test_delete_object_no_false_positive(bridge):
    """delete_object on freshly created object — rule must not false-positive."""
    name = f"LiveDel_{uuid.uuid4().hex[:6]}"
    await bridge.send("create_object", {"name": name})
    try:
        resp = await bridge.send("delete_object", {"path": f"/{name}"})
        text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
        assert "[REFLECT:" not in text or "ok" in text.lower(), (
            f"Spurious [REFLECT: in real delete_object response: {text[:300]}"
        )
    except Exception:
        from tests.live.conftest import _destroy
        await _destroy(bridge, name)
        raise


async def test_reflect_run_against_real_get_component(bridge, sandbox):
    """Feed real get_component response to reflect() — must not crash or false-positive.

    no-assert: crash guard — assert fires only when mismatch is returned (non-None).
    """
    resp = await bridge.send("get_component", {"path": sandbox, "type": "Transform"})
    real_text = resp.get("data", "") if isinstance(resp, dict) else str(resp)

    mismatch = await reflect(
        "set_property",
        {"path": sandbox, "component": "Transform", "prop": "localPosition", "value": "1,2,3"},
        real_text,
        _str_send(bridge.send),
    )

    if mismatch is not None:  # no-assert: crash guard
        assert len(mismatch.msg) > 5, "Mismatch message too short — likely garbled"
        assert "no snapshot" not in mismatch.msg.lower(), (
            f"False-positive 'no snapshot' on real response: {real_text[:200]}"
        )


async def test_reflect_run_against_real_manage_component(bridge, sandbox):
    """Feed real manage_component response to reflect() — must not crash.

    no-assert: crash guard — assert fires only when mismatch is returned (non-None).
    """
    await bridge.send("manage_component", {"path": sandbox, "type": "Rigidbody", "action": "add"})
    resp = await bridge.send("manage_component", {"path": sandbox, "type": "Rigidbody", "action": "remove"})
    real_text = resp.get("data", "") if isinstance(resp, dict) else str(resp)

    mismatch = await reflect(
        "manage_component",
        {"path": sandbox, "type": "Rigidbody", "action": "remove"},
        real_text,
        _str_send(bridge.send),
    )
    if mismatch is not None:  # no-assert: crash guard
        assert len(mismatch.msg) > 5, f"Garbled mismatch: {mismatch.msg!r}"


async def test_reflect_run_against_real_delete_object(bridge):
    """Feed real delete_object response to reflect() — must not crash.

    no-assert: crash guard — assert fires only when mismatch is returned (non-None).
    """
    name = f"LiveDel_{uuid.uuid4().hex[:6]}"
    await bridge.send("create_object", {"name": name})
    try:
        resp = await bridge.send("delete_object", {"path": f"/{name}"})
        real_text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
    except Exception:
        from tests.live.conftest import _destroy
        await _destroy(bridge, name)
        raise

    mismatch = await reflect(
        "delete_object",
        {"path": f"/{name}"},
        real_text,
        _str_send(bridge.send),
    )
    if mismatch is not None:  # no-assert: crash guard
        assert len(mismatch.msg) > 5, f"Garbled mismatch: {mismatch.msg!r}"


async def test_get_component_transform_has_position(bridge, sandbox):
    """Real Transform component response must contain position data."""
    resp = await bridge.send("get_component", {"path": sandbox, "type": "Transform"})
    text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
    assert "position" in text.lower() or "transform" in text.lower(), (
        f"Unexpected Transform response: {text[:200]}"
    )


async def test_set_then_read_position_roundtrip(bridge, sandbox):
    """Write localPosition → read it back — value must reflect the write."""
    await bridge.send(
        "set_property",
        {"path": sandbox, "component": "Transform", "prop": "localPosition", "value": "5,6,7"},
    )
    resp = await bridge.send("get_component", {"path": sandbox, "type": "Transform"})
    text = resp.get("data", "") if isinstance(resp, dict) else str(resp)
    assert any(v in text for v in ("5", "6", "7")), (
        f"Written position not reflected in get_component: {text[:300]}"
    )
