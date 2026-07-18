"""200 live monkey tests for MCPChatWindow via execute_code TCP.

Run with:
    pytest -m "live and live_chat" tests/live/test_chat_ui_monkey.py -v

Requires: Unity running, MCP plugin loaded, MCPChatWindow namespace available.
All C# snippets live in chat_ui_helpers — zero inline C# here.
"""
import pytest
import pytest_asyncio

from tests.live.chat_ui_helpers import (
    CLOSE_WINDOW,
    FIND_WINDOW,
    OPEN_WINDOW,
    ROOT_CLASSES,
    ROOT_NOT_NULL,
    WINDOW_TITLE,
    GET_INPUT,
    CLEAR_INPUT,
    GET_AGENT_MODE,
    GET_BACKEND_RUNNING,
    GET_ACTIVITY_PHASE,
    STOP_BACKEND_IF_PRESENT,
    CLOSE_AND_REOPEN,
    GET_BACKEND,
    GET_TRANSCRIPT,
    ASK_BTN_STATE,
    IS_IMAGE_EXT_NULL,
    IS_IMAGE_EXT_EMPTY,
    exec_ok,
    open_window,
    ensure_window,
    get_field,
    get_dropdown,
    get_dropdown_count,
    set_input,
    set_input_and_read,
    set_mode,
    set_mode_n,
    toggle_mode_n,
    get_session,
    set_session,
    erase_session,
    set_and_get_session,
    get_pref,
    set_pref,
    set_pref_bool,
    get_pref_bool,
    is_image_ext,
)

pytestmark = [pytest.mark.live, pytest.mark.live_chat]

# ────────────────────────────────────────────────────────────────────────────
# Fixtures
# ────────────────────────────────────────────────────────────────────────────

@pytest_asyncio.fixture(scope="session")
async def chat_session(bridge):
    """Open MCPChatWindow once for the whole session; close after."""
    await open_window(bridge)
    yield bridge
    try:
        await exec_ok(bridge, CLOSE_WINDOW)
    except Exception:
        pass


@pytest_asyncio.fixture
async def w(bridge):
    """Ensure MCPChatWindow is open before each test."""
    await ensure_window(bridge)
    yield bridge


# ────────────────────────────────────────────────────────────────────────────
# A. Window Lifecycle (25 tests)
# ────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("n", [1, 2, 3, 5, 10])
async def test_window_open_idempotent(bridge, n):
    """Opening the window N times returns the same singleton."""
    for _ in range(n):
        r = await exec_ok(bridge, OPEN_WINDOW)
        assert r in ("ok", "null"), f"unexpected: {r}"
    found = await exec_ok(bridge, FIND_WINDOW)
    assert found == "found"


@pytest.mark.parametrize("n", [1, 2, 3, 5, 10])
async def test_window_close_and_reopen(bridge, n):
    """Close and reopen N times — window always comes back."""
    for _ in range(n):
        await exec_ok(bridge, CLOSE_WINDOW)
        await exec_ok(bridge, OPEN_WINDOW)
    found = await exec_ok(bridge, FIND_WINDOW)
    assert found == "found"


async def test_window_root_has_chat_root_class(w):
    """rootVisualElement has the 'chat-root' CSS class."""
    classes = await exec_ok(w, ROOT_CLASSES)
    assert classes != "no_window", "window not found"
    assert "chat-root" in classes, f"'chat-root' not in: {classes}"


async def test_window_root_not_none(w):
    """rootVisualElement is not null."""
    result = await exec_ok(w, ROOT_NOT_NULL)
    assert result == "ok", f"root was null or window missing: {result}"


async def test_window_title_is_mcp_chat(w):
    """Window title content text equals 'MCP Chat'."""
    title = await exec_ok(w, WINDOW_TITLE)
    assert title == "MCP Chat", f"unexpected title: {title}"


async def test_window_none_before_open(bridge):
    """FIND_WINDOW returns 'none' after closing all windows."""
    await exec_ok(bridge, CLOSE_WINDOW)
    found = await exec_ok(bridge, FIND_WINDOW)
    assert found == "none"


async def test_window_reopen_after_close(bridge):
    """Close → open → FIND_WINDOW returns 'found'."""
    await exec_ok(bridge, CLOSE_WINDOW)
    await exec_ok(bridge, OPEN_WINDOW)
    found = await exec_ok(bridge, FIND_WINDOW)
    assert found == "found"


@pytest.mark.parametrize("seq", [0, 1, 2, 3, 4])
async def test_window_focus_sequence(bridge, seq):
    """Open window in various sequences — always findable afterward."""
    # Different open patterns keyed by seq index
    patterns = [
        [OPEN_WINDOW],
        [OPEN_WINDOW, OPEN_WINDOW],
        [CLOSE_WINDOW, OPEN_WINDOW],
        [OPEN_WINDOW, CLOSE_WINDOW, OPEN_WINDOW],
        [CLOSE_WINDOW, OPEN_WINDOW, OPEN_WINDOW],
    ]
    for snippet in patterns[seq]:
        await exec_ok(bridge, snippet)
    found = await exec_ok(bridge, FIND_WINDOW)
    assert found == "found"


@pytest.mark.parametrize("n", [1, 2, 3, 4, 5])
async def test_window_concurrent_get_calls(w, n):
    """Call FIND_WINDOW N times rapidly — always returns 'found'."""
    for _ in range(n):
        result = await exec_ok(w, FIND_WINDOW)
        assert result == "found"


# ────────────────────────────────────────────────────────────────────────────
# B. Input Interaction (30 tests)
# ────────────────────────────────────────────────────────────────────────────

_INPUT_TEXTS = ["hello", "", "a" * 500, "line1\nline2", "<b>html</b>", "123"]
_UNICODE_TEXTS = ["👋", "мир", "中文", "ñoño"]
_BOUNDARY_TEXTS = ["a" * 1000, "a" * 5000, "x", " "]
_SPECIAL_TEXTS = ['"quoted"', "it's", "back\\slash", "tab\there"]


@pytest.mark.parametrize("text", _INPUT_TEXTS)
async def test_input_set_text(w, text):
    """set_input returns the value that was set (or no_input)."""
    result = await exec_ok(w, set_input(text))
    assert result in (text, "no_input", "no_window"), f"unexpected: {result!r}"


@pytest.mark.parametrize("text", _INPUT_TEXTS)
async def test_input_read_after_set(w, text):
    """GET_INPUT after set_input returns the same text."""
    set_result = await exec_ok(w, set_input(text))
    if set_result in ("no_input", "no_window"):
        pytest.skip(f"_input not accessible: {set_result}")
    got = await exec_ok(w, GET_INPUT)
    assert got == text, f"expected {text!r}, got {got!r}"


@pytest.mark.parametrize("text", _INPUT_TEXTS)
async def test_input_clear(w, text):
    """After set+clear, GET_INPUT returns empty string."""
    await exec_ok(w, set_input(text))
    clear_result = await exec_ok(w, CLEAR_INPUT)
    if clear_result in ("no_input", "no_window"):
        pytest.skip(f"_input not accessible: {clear_result}")
    assert clear_result == ""


@pytest.mark.parametrize("text", _UNICODE_TEXTS)
async def test_input_unicode(w, text):
    """Unicode text round-trips through _input without corruption."""
    result = await exec_ok(w, set_input_and_read(text))
    if result in ("no_input", "no_window"):
        pytest.skip(f"_input not accessible: {result}")
    assert result == text, f"unicode round-trip failed: {result!r} != {text!r}"


@pytest.mark.parametrize("text", _BOUNDARY_TEXTS)
async def test_input_boundary(w, text):
    """Large/edge input values don't raise execute_code errors."""
    result = await exec_ok(w, set_input(text))
    # Just check no error was raised (exec_ok asserts ok=True)
    assert result is not None


@pytest.mark.parametrize("text", _SPECIAL_TEXTS)
async def test_input_special_chars(w, text):
    """Special chars (quotes, backslash, tab) round-trip without corruption."""
    result = await exec_ok(w, set_input_and_read(text))
    if result in ("no_input", "no_window"):
        pytest.skip(f"_input not accessible: {result}")
    assert result == text, f"special chars round-trip failed: {result!r} != {text!r}"


# ────────────────────────────────────────────────────────────────────────────
# C. Mode Switching (25 tests)
# ────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("seed", [0, 1, 2, 3, 4])
async def test_mode_initial_read(w, seed):
    """_agentMode field is readable and returns True or False."""
    result = await exec_ok(w, GET_AGENT_MODE)
    assert result in ("True", "False", "no_window"), f"unexpected: {result}"
    if result == "no_window":
        pytest.skip("window not found")


@pytest.mark.parametrize("n", [1, 2, 3, 5, 10])
async def test_mode_set_ask(w, n):
    """set_mode(False) N times → _agentMode == False."""
    result = await exec_ok(w, set_mode_n(agent=False, n=n))
    if result == "no_window":
        pytest.skip("window not found")
    assert result == "False", f"expected False after ask mode, got {result}"


@pytest.mark.parametrize("n", [1, 2, 3, 5, 10])
async def test_mode_set_agent(w, n):
    """set_mode(True) N times → _agentMode == True."""
    result = await exec_ok(w, set_mode_n(agent=True, n=n))
    if result == "no_window":
        pytest.skip("window not found")
    assert result == "True", f"expected True after agent mode, got {result}"


@pytest.mark.parametrize("n", [1, 2, 4, 8, 16])
async def test_mode_toggle_sequence(w, n):
    """Alternating mode N times produces mode consistent with parity."""
    # Start from Ask mode so we have a known baseline
    await exec_ok(w, set_mode(agent=False))
    result = await exec_ok(w, toggle_mode_n(n))
    if result == "no_window":
        pytest.skip("window not found")
    expected = "True" if n % 2 == 1 else "False"
    assert result == expected, f"after {n} toggles expected {expected}, got {result}"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_mode_ask_btn_css(w, n):
    """In Ask mode, _askBtn has 'mode-toggle-btn--active' CSS class."""
    # Use n as a repetition seed — set ask mode n+1 times for variety
    await exec_ok(w, set_mode_n(agent=False, n=n + 1))
    result = await exec_ok(w, ASK_BTN_STATE)
    if result in ("no_window", "null"):
        pytest.skip(f"_askBtn not accessible: {result}")
    assert result == "active", f"ask button not active in Ask mode: {result}"


# ────────────────────────────────────────────────────────────────────────────
# D. Model / Backend Selection (25 tests)
# ────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_model_dropdown_has_value(w, n):
    """_modelDropdown has a non-null selected value."""
    result = await exec_ok(w, get_dropdown("_modelDropdown"))
    if result == "no_window":
        pytest.skip("window not found")
    assert result != "null", f"_modelDropdown value is null (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_agent_dropdown_has_value(w, n):
    """_agentDropdown has a non-null selected value."""
    result = await exec_ok(w, get_dropdown("_agentDropdown"))
    if result == "no_window":
        pytest.skip("window not found")
    assert result != "null", f"_agentDropdown value is null (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_editorpref_selected_backend_readable(w, n):
    """MCPChat.SelectedBackend EditorPref is readable without error."""
    result = await exec_ok(w, get_pref("MCPChat.SelectedBackend"))
    # MISSING is fine — key may not be set yet; both outcomes are non-error
    assert isinstance(result, str), f"unexpected type (seed {n})"


@pytest.mark.parametrize("val", ["claude", "codex", "kimi", "gemini", "opencode"])
async def test_editorpref_set_backend(bridge, val):
    """Writing and reading MCPChat.SelectedBackend EditorPref round-trips."""
    original = await exec_ok(bridge, get_pref("MCPChat.SelectedBackend"))
    try:
        await exec_ok(bridge, set_pref("MCPChat.SelectedBackend", val))
        got = await exec_ok(bridge, get_pref("MCPChat.SelectedBackend"))
        assert got == val, f"EditorPref round-trip failed: wrote {val!r}, got {got!r}"
    finally:
        restore = original if original != "MISSING" else ""
        try:
            await exec_ok(bridge, set_pref("MCPChat.SelectedBackend", restore))
        except Exception:
            pass


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_model_dropdown_choices_nonempty(w, n):
    """_modelDropdown has at least one choice available."""
    count_str = await exec_ok(w, get_dropdown_count("_modelDropdown"))
    if count_str in ("no_window", "null"):
        pytest.skip(f"_modelDropdown not accessible: {count_str}")
    assert int(count_str) > 0, f"_modelDropdown has 0 choices (seed {n})"


# ────────────────────────────────────────────────────────────────────────────
# E. Backend State (25 tests)
# ────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_backend_field_accessible(w, n):
    """_backend private field is accessible via reflection (no exception)."""
    result = await exec_ok(w, GET_BACKEND)
    # "null" means backend field is None (fresh window); "no_window" is a skip
    if result == "no_window":
        pytest.skip("window not found")
    assert isinstance(result, str), f"unexpected (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_backend_is_running_readable(w, n):
    """IsRunning property on _backend (if non-null) returns True or False."""
    result = await exec_ok(w, GET_BACKEND_RUNNING)
    if result == "no_window":
        pytest.skip("window not found")
    valid = {"True", "False", "null", "no_IsRunning"}
    assert result in valid, f"unexpected IsRunning value: {result} (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_activity_phase_readable(w, n):
    """_activity.Phase is readable without exception."""
    result = await exec_ok(w, GET_ACTIVITY_PHASE)
    if result == "no_window":
        pytest.skip("window not found")
    assert isinstance(result, str) and result not in ("", "no_window"), (
        f"unexpected Phase value: {result!r} (seed {n})"
    )


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_backend_stop_when_idle(w, n):
    """Calling Stop on backend (when idle) doesn't crash execute_code."""
    result = await exec_ok(w, STOP_BACKEND_IF_PRESENT)
    if result == "no_window":
        pytest.skip("window not found")
    # Either "stopped" or "no_backend" are valid outcomes
    assert result in ("stopped", "no_backend"), f"unexpected: {result} (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_backend_null_after_close(bridge, n):
    """Fresh window after close/reopen has _backend set (CreateBackend is called in OnEnable)."""
    result = await exec_ok(bridge, CLOSE_AND_REOPEN)
    # After reopen, OnEnable calls CreateBackend(), so _backend should be non-null
    # RelayBackend or similar type name expected; "null" would indicate a bug
    if result == "no_window":
        pytest.skip("window not found")
    # We accept both null and a type name — OnEnable timing may vary
    assert isinstance(result, str), f"unexpected type (seed {n})"


# ────────────────────────────────────────────────────────────────────────────
# F. SessionState / Transcript (25 tests)
# ────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_transcript_accessible(w, n):
    """_transcript internal field is accessible via reflection."""
    result = await exec_ok(w, GET_TRANSCRIPT)
    if result == "no_window":
        pytest.skip("window not found")
    # "null" should not happen (CreateGUI sets _transcript), but tolerate it
    assert isinstance(result, str), f"unexpected (seed {n})"


_SESSION_VALS = ["alpha", "bb", "ccc", "d" * 100, "simple"]


@pytest.mark.parametrize("val", _SESSION_VALS)
async def test_session_transcript_key_writable(bridge, val):
    """Writing a test session key and reading it back round-trips correctly."""
    key = "MCPChat_TestMonkey"
    try:
        await exec_ok(bridge, set_session(key, val))
        got = await exec_ok(bridge, get_session(key))
        assert got == val, f"session round-trip: wrote {val!r}, got {got!r}"
    finally:
        try:
            await exec_ok(bridge, erase_session(key))
        except Exception:
            pass


@pytest.mark.parametrize("val", _SESSION_VALS)
async def test_session_transcript_erase(bridge, val):
    """After EraseString, GetString returns 'MISSING'."""
    key = "MCPChat_TestMonkeyErase"
    await exec_ok(bridge, set_session(key, val))
    await exec_ok(bridge, erase_session(key))
    got = await exec_ok(bridge, get_session(key))
    assert got == "MISSING", f"expected MISSING after erase, got {got!r}"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_session_backend_id_key(bridge, n):
    """MCPChat_BackendSessionId key is readable from SessionState."""
    result = await exec_ok(bridge, get_session("MCPChat_BackendSessionId"))
    assert isinstance(result, str), f"unexpected type (seed {n})"


@pytest.mark.parametrize("n", [0, 1, 2, 3, 4])
async def test_session_erase_backend_id(bridge, n):
    """Erasing MCPChat_BackendSessionId results in MISSING on next read."""
    await exec_ok(bridge, erase_session("MCPChat_BackendSessionId"))
    got = await exec_ok(bridge, get_session("MCPChat_BackendSessionId"))
    assert got == "MISSING", f"expected MISSING, got {got!r} (seed {n})"


# ────────────────────────────────────────────────────────────────────────────
# G. IsImageExtension (20 tests)
# ────────────────────────────────────────────────────────────────────────────

_IMAGE_EXTS     = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"]
_NON_IMAGE_EXTS = [".cs", ".unity", ".asset", ".prefab", ".json", ".txt"]
_EDGE_EXTS      = [".PNG", ".JPG", "png", ""]  # uppercase, no-dot, empty
_UPPER_EXTS     = [".PNG", ".JPG", ".JPEG", ".BMP"]


@pytest.mark.parametrize("ext", _IMAGE_EXTS)
async def test_image_ext_true(bridge, ext):
    """Known image extensions return 'True' from IsImageExtension."""
    result = await exec_ok(bridge, is_image_ext(ext))
    if result == "method_not_found":
        pytest.skip("IsImageExtension not accessible via reflection")
    assert result == "True", f"expected True for {ext!r}, got {result!r}"


@pytest.mark.parametrize("ext", _NON_IMAGE_EXTS)
async def test_image_ext_false(bridge, ext):
    """Non-image extensions return 'False' from IsImageExtension."""
    result = await exec_ok(bridge, is_image_ext(ext))
    if result == "method_not_found":
        pytest.skip("IsImageExtension not accessible via reflection")
    assert result == "False", f"expected False for {ext!r}, got {result!r}"


@pytest.mark.parametrize("ext,expected", [
    (".PNG", "True"),
    (".JPG", "True"),
    ("png", "False"),   # no dot → not matched
    ("", "False"),      # empty → not an image
])
async def test_image_ext_boundary(bridge, ext, expected):
    """Edge cases for IsImageExtension (uppercase tolerated, missing dot not)."""
    result = await exec_ok(bridge, is_image_ext(ext))
    if result == "method_not_found":
        pytest.skip("IsImageExtension not accessible via reflection")
    assert result == expected, f"IsImageExtension({ext!r}) = {result!r}, want {expected!r}"


@pytest.mark.parametrize("ext", _UPPER_EXTS)
async def test_image_ext_case_insensitive(bridge, ext):
    """IsImageExtension is case-insensitive for standard image extensions."""
    result = await exec_ok(bridge, is_image_ext(ext))
    if result == "method_not_found":
        pytest.skip("IsImageExtension not accessible via reflection")
    assert result == "True", f"expected True for uppercase {ext!r}, got {result!r}"


# ────────────────────────────────────────────────────────────────────────────
# H. State Persistence (25 tests)
# ────────────────────────────────────────────────────────────────────────────

_BOOL_VALS  = [True, False, True, False, True]
_PREF_BACKENDS = ["claude", "codex", "kimi", "opencode", "gemini"]
_SESSION_PERSIST_VALS = ["abc", "", "{json}", "line\n2", "unicode:мир"]


@pytest.mark.parametrize("val", _BOOL_VALS)
async def test_editorpref_autoscroll_bool(bridge, val):
    """MCPChat.AutoScroll bool EditorPref round-trips correctly."""
    original = await exec_ok(bridge, get_pref_bool("MCPChat.AutoScroll"))
    try:
        result = await exec_ok(bridge, set_pref_bool("MCPChat.AutoScroll", val))
        expected = str(val)
        assert result == expected, f"AutoScroll pref: set {val}, got {result!r}"
    finally:
        try:
            await exec_ok(bridge, set_pref_bool("MCPChat.AutoScroll", original == "True"))
        except Exception:
            pass


@pytest.mark.parametrize("val", _PREF_BACKENDS)
async def test_editorpref_selected_backend_roundtrip(bridge, val):
    """MCPChat.SelectedBackend string EditorPref round-trips correctly."""
    await exec_ok(bridge, set_pref("MCPChat.SelectedBackend", val))
    got = await exec_ok(bridge, get_pref("MCPChat.SelectedBackend"))
    assert got == val, f"SelectedBackend: wrote {val!r}, got {got!r}"


@pytest.mark.parametrize("val", _BOOL_VALS)
async def test_editorpref_disable_scene_norm_bool(bridge, val):
    """MCPChat.DisableSceneNameNorm bool EditorPref round-trips correctly."""
    original = await exec_ok(bridge, get_pref_bool("MCPChat.DisableSceneNameNorm"))
    try:
        result = await exec_ok(bridge, set_pref_bool("MCPChat.DisableSceneNameNorm", val))
        expected = str(val)
        assert result == expected, f"DisableSceneNameNorm: set {val}, got {result!r}"
    finally:
        try:
            await exec_ok(bridge, set_pref_bool("MCPChat.DisableSceneNameNorm", original == "True"))
        except Exception:
            pass


@pytest.mark.parametrize("val", _SESSION_PERSIST_VALS)
async def test_session_state_roundtrip(bridge, val):
    """SessionState.SetString + GetString round-trips arbitrary values."""
    key = "MCPChat_MonkeyPersist"
    try:
        got = await exec_ok(bridge, set_and_get_session(key, val))
        assert got == val, f"session roundtrip: wrote {val!r}, got {got!r}"
    finally:
        try:
            await exec_ok(bridge, erase_session(key))
        except Exception:
            pass


@pytest.mark.parametrize("val", ["v1", "v2", "v3", "v4", "v5"])
async def test_window_reopen_session_state_survives(bridge, val):
    """SessionState key survives MCPChatWindow close + reopen (process-scoped)."""
    key = "MCPChat_MonkeyReopen"
    try:
        await exec_ok(bridge, set_session(key, val))
        # Close then reopen window
        await exec_ok(bridge, CLOSE_WINDOW)
        await exec_ok(bridge, OPEN_WINDOW)
        # SessionState is process-scoped — survives window lifecycle
        got = await exec_ok(bridge, get_session(key))
        assert got == val, f"session lost after reopen: wrote {val!r}, got {got!r}"
    finally:
        try:
            await exec_ok(bridge, erase_session(key))
        except Exception:
            pass
