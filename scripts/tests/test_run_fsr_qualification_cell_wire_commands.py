"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: every raw TCP
wire command `scripts/run_fsr_qualification_cell.py` sends must be a real
C#-side command registered in `CommandRouter.Registration.cs` — a Python-only
tool wrapper (e.g. the MCP server's `mcp_status`) is not reachable over raw
TCP and fails closed with "is a Python-only tool" at runtime, exactly the
class of bug that produced 6x INFRASTRUCTURE_BLOCKED on the first matrix run.

Runs in the standard `scripts/tests` lane: no Unity, no network — reads the
tracked script and the tracked C# registration file only.
"""
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CELL_SCRIPT = REPO_ROOT / "scripts" / "run_fsr_qualification_cell.py"
REGISTRATION_CS = REPO_ROOT / "unity-plugin" / "Editor" / "CommandRouter.Registration.cs"

CALL_PATTERN = re.compile(r'durable\.call\(\s*port,\s*"([a-zA-Z_]+)"')
REGISTER_PATTERN = re.compile(r'CommandRegistry\.(?:Register|RegisterAction|RegisterAsync)\(\s*"([a-zA-Z_]+)"')


def _wire_commands_used_by_cell_script() -> set[str]:
    text = CELL_SCRIPT.read_text(encoding="utf-8")
    return set(CALL_PATTERN.findall(text))


def _registered_wire_commands() -> set[str]:
    text = REGISTRATION_CS.read_text(encoding="utf-8")
    return set(REGISTER_PATTERN.findall(text))


def test_cell_script_calls_at_least_one_wire_command():
    """Guards against the pattern regexes silently matching nothing (e.g.
    after a call-site refactor) and this test going green for the wrong
    reason."""
    assert len(_wire_commands_used_by_cell_script()) >= 2


def test_cell_script_wire_commands_are_all_registered_in_csharp():
    used = _wire_commands_used_by_cell_script()
    registered = _registered_wire_commands()
    unregistered = used - registered
    assert not unregistered, (
        f"run_fsr_qualification_cell.py calls unregistered wire command(s): "
        f"{sorted(unregistered)} — not in CommandRouter.Registration.cs"
    )


def test_cell_script_never_calls_python_only_mcp_status():
    """The regression this test was added for: mcp_status is a Python MCP
    tool (server/src/unity_mcp/tools/...), not a C# wire command — sending
    it over raw TCP fails with "is a Python-only tool"."""
    assert "mcp_status" not in _wire_commands_used_by_cell_script()


def test_cell_script_uses_get_status_for_the_pilot_health_probe():
    text = CELL_SCRIPT.read_text(encoding="utf-8")
    assert '"get_status"' in text


def test_registered_wire_commands_include_async_registrations():
    """Regression guard: REGISTER_PATTERN previously only matched
    CommandRegistry.Register/RegisterAction, missing RegisterAsync entirely
    — source_patch_write (the correct ON-mode write route,
    CommandRouter.Registration.cs) is registered via RegisterAsync and
    would have been silently treated as "unregistered" by this test."""
    assert "source_patch_write" in _registered_wire_commands()


def test_cell_script_routes_on_mode_writes_through_source_patch_write():
    """Run 5 (33387852561): min-macos-arm64 reached real semantic
    execution and failed with "asset failed: STATE: state=OnReady: source
    patch active — legacy .cs write rejected pre-effect" — the C#-side
    asset/write_text route explicitly rejects .cs writes while mutation is
    ON; the real Python asset.py tool routes those through source_patch_write
    instead (server/src/unity_mcp/tools/asset.py). The cell script must do
    the same while mutation is ON."""
    assert "source_patch_write" in _wire_commands_used_by_cell_script()
