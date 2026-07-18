"""Live tests: batch + runtime commands in Play Mode."""
import pytest

pytestmark = pytest.mark.live


async def test_batch_invoke_method_not_unknown(play_session):
    """invoke_method in batch must NOT return 'Unknown command' (schema regression)."""
    await play_session.send("create_object", {"name": "Live_BatchTest"})
    try:
        result = await play_session.send("batch", {
            "commands": "invoke_method path=/Live_BatchTest component=Transform method=Rotate args=0,1,0"
        })
        text = result.get("data", "") if isinstance(result, dict) else str(result)
        assert "Unknown command" not in text, f"Schema still missing invoke_method: {text}"
        assert "Unknown param" not in text, f"Schema param mismatch: {text}"
    finally:
        try:
            await play_session.send("delete_object", {"path": "/Live_BatchTest"})
        except Exception:
            pass


async def test_batch_query_state_not_unknown(play_session):
    """query_state in batch must NOT return 'Unknown command'."""
    await play_session.send("create_object", {"name": "Live_QSTest"})
    try:
        result = await play_session.send("batch", {
            "commands": "query_state queries=/Live_QSTest|Transform|localPosition"
        })
        text = result.get("data", "") if isinstance(result, dict) else str(result)
        assert "Unknown command" not in text, f"Schema missing query_state: {text}"
    finally:
        try:
            await play_session.send("delete_object", {"path": "/Live_QSTest"})
        except Exception:
            pass


async def test_batch_async_commands_blocked_not_unknown(ensure_edit_mode, bridge):
    """Async commands in batch get 'async-only' error, NOT 'Unknown command'."""
    result = await bridge.send("batch", {
        "commands": "wait_until path=/X component=Y field=z value=1"
    })
    text = result.get("data", "") if isinstance(result, dict) else str(result)
    assert "Unknown command" not in text, f"Schema missing wait_until: {text}"
    assert "async-only" in text.lower() or "BLOCKED" in text, f"Expected async block: {text}"


async def test_delete_object_by_path_in_batch(ensure_edit_mode, bridge):
    """delete_object with path= works in batch (was rejected by schema)."""
    await bridge.send("create_object", {"name": "Live_DelPath"})
    try:
        result = await bridge.send("batch", {
            "commands": "delete_object path=/Live_DelPath"
        })
        text = result.get("data", "") if isinstance(result, dict) else str(result)
        assert "Unknown param" not in text, f"path param rejected: {text}"
        assert "Deleted" in text or "ok:1" in text, f"Delete failed: {text}"
    finally:
        try:
            await bridge.send("delete_object", {"path": "/Live_DelPath"})
        except Exception:
            pass
