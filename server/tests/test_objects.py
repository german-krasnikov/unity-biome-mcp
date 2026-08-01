"""TDD: objects.py — #03 compress=true fields reorder."""
from unittest.mock import AsyncMock


def _make_args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


def _setup(monkeypatch):
    from unity_mcp.tools import objects
    mock = AsyncMock(return_value="mass: 1.0")
    monkeypatch.setattr(objects, "_send", mock)
    monkeypatch.setattr(objects, "_args", _make_args)
    monkeypatch.setattr(objects, "_project_fields", lambda r, f: r)
    return mock


# --- get_component ---

async def test_get_component_compress_with_fields_omits_compress(monkeypatch):
    """#03: when fields= is set, compress must NOT be sent to C# (would strip requested fields)."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import get_component
    await get_component("path", "Rigidbody", fields="mass", compress=True)
    call_args = mock.call_args[0][1]
    assert "compress" not in call_args


async def test_get_component_compress_without_fields_includes_compress(monkeypatch):
    """#03: when fields= is absent, compress=true is passed through."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import get_component
    await get_component("path", "Rigidbody", compress=True)
    call_args = mock.call_args[0][1]
    assert call_args.get("compress") == "true"


async def test_get_component_no_compress_no_fields_clean(monkeypatch):
    """Baseline: neither compress nor fields → plain args."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import get_component
    await get_component("path", "Rigidbody")
    call_args = mock.call_args[0][1]
    assert "compress" not in call_args
    assert "_no_distill" not in call_args


# --- inspect ---

async def test_inspect_compress_with_fields_omits_compress(monkeypatch):
    """#03: inspect same fix — compress must be absent when fields= is set."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import inspect
    await inspect("a,b", fields="mass", compress=True)
    call_args = mock.call_args[0][1]
    assert "compress" not in call_args


async def test_inspect_compress_without_fields_includes_compress(monkeypatch):
    """#03: inspect — compress passed through when no fields projection."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import inspect
    await inspect("a,b", compress=True)
    call_args = mock.call_args[0][1]
    assert call_args.get("compress") == "true"


# ── #13: inspect find_type ────────────────────────────────────────────────────

async def test_inspect_find_type_passed_to_bridge(monkeypatch):
    """#13: inspect(find_type=X) sends find_type to bridge."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import inspect
    await inspect(find_type="Rigidbody")
    call_args = mock.call_args[0][1]
    assert call_args.get("find_type") == "Rigidbody"
    assert "paths" not in call_args


async def test_inspect_paths_and_find_type_both_forwarded(monkeypatch):
    """paths and find_type can both be provided."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import inspect
    await inspect(paths="/A,/B", find_type="Rigidbody")
    call_args = mock.call_args[0][1]
    assert call_args["paths"] == "/A,/B"
    assert call_args["find_type"] == "Rigidbody"


# ── #13: set_property find_type ───────────────────────────────────────────────

async def test_set_property_find_type_bulk(monkeypatch):
    """#13: set_property(find_type=X) sends find_type instead of path."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_property
    await set_property(component="Rigidbody", prop="mass", value="5", find_type="Rigidbody")
    call_args = mock.call_args[0][1]
    assert call_args.get("find_type") == "Rigidbody"
    assert "path" not in call_args


# ── #12B: get_unity_events ────────────────────────────────────────────────────

async def test_get_unity_events_no_filter(monkeypatch):
    """#12B: get_unity_events() sends command with no path arg."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import get_unity_events
    await get_unity_events()
    call_args = mock.call_args[0]
    assert call_args[0] == "get_unity_events"
    assert "path" not in call_args[1]


async def test_get_unity_events_with_path_filter(monkeypatch):
    """#12B: get_unity_events(path='/UI') forwards path filter."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import get_unity_events
    await get_unity_events(path="/UI")
    call_args = mock.call_args[0]
    assert call_args[1].get("path") == "/UI"


# ── set_parent world_position_stays Pattern A′ ───────────────────────────────

async def test_set_parent_wps_default_omitted(monkeypatch):
    """world_position_stays=True (default) omits key (Pattern A′: C# default is true)."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_parent
    await set_parent(path="/Child", parent="/Root")
    args = mock.call_args[0][1]
    assert "world_position_stays" not in args, (
        f"world_position_stays should be omitted when True, got {args.get('world_position_stays')!r}"
    )


async def test_set_parent_wps_false_sends_string(monkeypatch):
    """world_position_stays=False sends 'false' string (Pattern A′)."""
    mock = _setup(monkeypatch)
    from unity_mcp.tools.objects import set_parent
    await set_parent(path="/Child", parent="/Root", world_position_stays=False)
    args = mock.call_args[0][1]
    assert args["world_position_stays"] == "false"
