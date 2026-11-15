"""TDD tests for search_scene scoped extension (root + limit params). F13."""
from unity_mcp.tools.scene import search_scene


# Scenario 1: root kwarg forwarded as 'root' key
async def test_search_scoped_root_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Child #1"}
    await search_scene(query="t:Rigidbody", root="/Player")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("root") == "/Player"


# Scenario 2: limit=10 forwarded as string "10"
async def test_search_scoped_limit_forwarded_as_string(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Obj #1"}
    await search_scene(query="t:Light", limit=10)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("limit") == "10"


# Scenario 3: limit=50 (default) NOT sent (saves wire bytes)
async def test_search_scoped_default_limit_not_sent(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Obj #1"}
    await search_scene(query="t:Light")
    sent = mock_bridge.send.call_args[0][1]
    assert "limit" not in sent


# Scenario 4: limit=0 forwarded as "0" (unlimited)
async def test_search_scoped_limit_zero_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Obj #1"}
    await search_scene(query="t:Light", limit=0)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("limit") == "0"


# Scenario 5: root=None NOT sent
async def test_search_scoped_root_none_not_sent(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Obj #1"}
    await search_scene(query="t:Light", root=None)
    sent = mock_bridge.send.call_args[0][1]
    assert "root" not in sent


# Scenario 6: root + limit both forwarded together
async def test_search_scoped_root_and_limit_combined(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Child #1\nChild #2"}
    await search_scene(query="Enemy", root="/EnemyContainer", limit=5)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("root") == "/EnemyContainer"
    assert sent.get("limit") == "5"
