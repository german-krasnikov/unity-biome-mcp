"""TDD tests for resolve_scene_refs tool."""


async def test_resolve_sends_correct_command(mock_bridge):
    """resolve_scene_refs sends 'resolve_scene_refs' with refs arg."""
    mock_bridge.send.return_value = {"ok": True, "data": "OK\t/Player\tactive\tiid=1\tscene=Main"}
    from unity_mcp.tools.runtime import resolve_scene_refs
    await resolve_scene_refs("/Player")
    cmd = mock_bridge.send.call_args[0][0]
    sent = mock_bridge.send.call_args[0][1]
    assert cmd == "resolve_scene_refs"
    assert sent["refs"] == "/Player"


async def test_resolve_passes_fields(mock_bridge):
    """fields param is forwarded when provided."""
    mock_bridge.send.return_value = {"ok": True, "data": "OK\t/Player\tactive\tiid=1\tscene=Main\thealth=OK"}
    from unity_mcp.tools.runtime import resolve_scene_refs
    await resolve_scene_refs("/Player", fields="health")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("fields") == "health"


async def test_resolve_no_fields(mock_bridge):
    """fields is omitted from args when not provided."""
    mock_bridge.send.return_value = {"ok": True, "data": "OK\t/Player\tactive\tiid=1\tscene=Main"}
    from unity_mcp.tools.runtime import resolve_scene_refs
    await resolve_scene_refs("/Player")
    sent = mock_bridge.send.call_args[0][1]
    assert "fields" not in sent
