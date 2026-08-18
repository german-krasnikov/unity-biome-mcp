"""C# snippet factories for MCPChatWindow live tests.

No pytest imports — pure factory functions and async helpers.
All C# strings are centralised here; test file has zero inline C#.

Reflection-free: uses MCPChatWindow.Test* public API (MCPChatWindow.TestInspect.cs)
so the execute_code security scanner sees no blocked patterns.
"""
from tests.live._markers import strip_markers

# Window type — fully-qualified for Roslyn snippets.
_T    = "UnityMCP.Editor.Chat.MCPChatWindow"
# FindObjectsOfTypeAll without LINQ (Unity6, no System.Linq guarantee in exec context).
_FIND = (
    f"var _all=Resources.FindObjectsOfTypeAll<{_T}>();"
    f"var w=_all.Length>0?_all[0]:null;"
)
_NO_WIN = 'if(w==null) return "no_window";'


def _cs_str(text: str) -> str:
    """Wrap Python text as a C# verbatim string literal @\"...\"."""
    return '@"' + text.replace('"', '""') + '"'


# ── Window access ───────────────────────────────────────────────────────────

OPEN_WINDOW = (
    f'var _w = UnityEditor.EditorWindow.GetWindow<{_T}>("MCP Chat");'
    f'return _w != null ? "ok" : "null";'
)

FIND_WINDOW = (
    f'var _a=Resources.FindObjectsOfTypeAll<{_T}>();'
    f'return _a.Length>0 ? "found" : "none";'
)

CLOSE_WINDOW = (
    f'foreach(var _w in Resources.FindObjectsOfTypeAll<{_T}>())'
    f'    _w.Close();'
    f'return "closed";'
)

ROOT_CLASSES = (
    f'{_FIND}{_NO_WIN}'
    f'var _c=w.rootVisualElement?.GetClasses();'
    f'return _c!=null ? string.Join(",",_c) : "null";'
)

WINDOW_TITLE = (
    f'{_FIND}{_NO_WIN}'
    f'return w.titleContent.text ?? "null";'
)

ROOT_NOT_NULL = (
    f'{_FIND}{_NO_WIN}'
    f'return w.rootVisualElement!=null ? "ok" : "null";'
)


# ── Private field read (via Test* public API — no reflection needed) ────────

def get_field(field: str) -> str:
    """Return private field value as string via public TestInspect property."""
    _MAP = {
        "_agentMode":  "w.TestAgentMode.ToString()",
        "_backend":    "w.TestBackendTypeName",
        "_transcript": "w.TestTranscriptTypeName",
        "_input":      "w.TestInputValue",
        "_askBtn":     "w.TestAskBtnState",
        "_activity":   "w.TestActivityPhase",
    }
    expr = _MAP.get(field, f'"unknown_field:{field}"')
    return f'{_FIND}{_NO_WIN}return {expr};'


def get_field_type(field: str) -> str:
    """Return runtime type name of a private instance field."""
    # Delegate to get_field — type names are embedded in Test* properties
    return get_field(field)


# Pre-built snippets for commonly-read fields
GET_AGENT_MODE  = f'{_FIND}{_NO_WIN}return w.TestAgentMode.ToString();'
GET_BACKEND     = f'{_FIND}{_NO_WIN}return w.TestBackendTypeName;'
GET_TRANSCRIPT  = f'{_FIND}{_NO_WIN}return w.TestTranscriptTypeName;'


# ── Input interaction ────────────────────────────────────────────────────────

def set_input(text: str) -> str:
    """Set _input TextField.value and return the value that was set."""
    cs = _cs_str(text)
    return f'{_FIND}{_NO_WIN}return w.TestSetInput({cs});'


GET_INPUT = f'{_FIND}{_NO_WIN}return w.TestInputValue;'

CLEAR_INPUT = f'{_FIND}{_NO_WIN}w.TestSetInput(""); return w.TestInputValue;'


def set_input_and_read(text: str) -> str:
    """Set _input, then read it back — single round-trip."""
    cs = _cs_str(text)
    return f'{_FIND}{_NO_WIN}return w.TestSetInput({cs});'


# ── Mode toggle ──────────────────────────────────────────────────────────────

def set_mode(agent: bool) -> str:
    """Call SetMode(bool) and return updated _agentMode."""
    flag = "true" if agent else "false"
    return (
        f'{_FIND}{_NO_WIN}'
        f'w.TestSetMode({flag});'
        f'return w.TestAgentMode.ToString();'
    )


def set_mode_n(agent: bool, n: int) -> str:
    """Call SetMode(bool) N times and return final _agentMode."""
    flag = "true" if agent else "false"
    return (
        f'{_FIND}{_NO_WIN}'
        f'for(int _i=0;_i<{n};_i++) w.TestSetMode({flag});'
        f'return w.TestAgentMode.ToString();'
    )


def toggle_mode_n(n: int) -> str:
    """Alternate Ask/Agent mode N times, return final _agentMode."""
    return (
        f'{_FIND}{_NO_WIN}'
        f'for(int _i=0;_i<{n};_i++) w.TestSetMode(!w.TestAgentMode);'
        f'return w.TestAgentMode.ToString();'
    )


ASK_BTN_STATE = f'{_FIND}{_NO_WIN}return w.TestAskBtnState;'


# ── Dropdowns ────────────────────────────────────────────────────────────────

_DROPDOWN_PROP = {
    "_modelDropdown": "TestModelDropdown",
    "_agentDropdown": "TestAgentDropdown",
}


def get_dropdown(field: str) -> str:
    """Read current value of a DropdownField private field."""
    prop = _DROPDOWN_PROP.get(field, f'"unknown_dropdown:{field}"')
    if prop.startswith('"'):
        return f'{_FIND}{_NO_WIN}return {prop};'
    return f'{_FIND}{_NO_WIN}return w.{prop};'


def get_dropdown_count(field: str) -> str:
    """Return the number of choices in a DropdownField."""
    # Only _modelDropdown tested; generalise if needed
    _ = field  # field param kept for API stability
    return f'{_FIND}{_NO_WIN}return w.TestModelDropdownCount.ToString();'


# ── Backend state ─────────────────────────────────────────────────────────────

GET_BACKEND_RUNNING = f'{_FIND}{_NO_WIN}return w.TestBackendIsRunning.ToString();'

GET_ACTIVITY_PHASE = f'{_FIND}{_NO_WIN}return w.TestActivityPhase;'

STOP_BACKEND_IF_PRESENT = (
    f'{_FIND}{_NO_WIN}'
    f'w.TestStopBackend();'
    f'return "stopped";'
)

CLOSE_AND_REOPEN = (
    f'foreach(var _ww in Resources.FindObjectsOfTypeAll<{_T}>())'
    f'    _ww.Close();'
    f'var _w2=UnityEditor.EditorWindow.GetWindow<{_T}>("MCP Chat");'
    f'return _w2.TestBackendTypeName;'
)


# ── SessionState ─────────────────────────────────────────────────────────────

def get_session(key: str) -> str:
    return f'return UnityEditor.SessionState.GetString({_cs_str(key)},"MISSING");'


def set_session(key: str, val: str) -> str:
    return (
        f'UnityEditor.SessionState.SetString({_cs_str(key)},{_cs_str(val)});'
        f'return "ok";'
    )


def erase_session(key: str) -> str:
    return f'UnityEditor.SessionState.EraseString({_cs_str(key)}); return "ok";'


def set_and_get_session(key: str, val: str) -> str:
    """Write then read SessionState in a single round-trip."""
    return (
        f'UnityEditor.SessionState.SetString({_cs_str(key)},{_cs_str(val)});'
        f'return UnityEditor.SessionState.GetString({_cs_str(key)},"MISSING");'
    )


# ── EditorPrefs ──────────────────────────────────────────────────────────────

def get_pref(key: str) -> str:
    return f'return UnityEditor.EditorPrefs.GetString({_cs_str(key)},"MISSING");'


def set_pref(key: str, val: str) -> str:
    return (
        f'UnityEditor.EditorPrefs.SetString({_cs_str(key)},{_cs_str(val)});'
        f'return "ok";'
    )


def set_pref_bool(key: str, val: bool) -> str:
    """Set a bool EditorPref and return the read-back value (roundtrip in one shot)."""
    flag = "true" if val else "false"
    return (
        f'UnityEditor.EditorPrefs.SetBool({_cs_str(key)},{flag});'
        f'return UnityEditor.EditorPrefs.GetBool({_cs_str(key)},false).ToString();'
    )


def get_pref_bool(key: str) -> str:
    return f'return UnityEditor.EditorPrefs.GetBool({_cs_str(key)},false).ToString();'


# ── IsImageExtension (G category) ───────────────────────────────────────────

def is_image_ext(ext: str) -> str:
    """Call public static TestIsImageExtension wrapper — no reflection needed."""
    cs = _cs_str(ext)
    return f'return {_T}.TestIsImageExtension({cs});'


IS_IMAGE_EXT_NULL  = f'return {_T}.TestIsImageExtension((string)null);'
IS_IMAGE_EXT_EMPTY = f'return {_T}.TestIsImageExtension(string.Empty);'


# ── Python async helpers ─────────────────────────────────────────────────────

async def exec_ok(bridge, code: str) -> str:
    """Send execute_code, assert ok=True, return data string (markers stripped)."""
    r = await bridge.send("execute_code", {"code": code})
    assert r.get("ok"), f"execute_code failed: {r.get('err') or r.get('data')}"
    return strip_markers(str(r.get("data", "")))


async def open_window(bridge) -> str:
    return await exec_ok(bridge, OPEN_WINDOW)


async def ensure_window(bridge) -> None:
    data = await exec_ok(bridge, FIND_WINDOW)
    if data != "found":
        await open_window(bridge)
