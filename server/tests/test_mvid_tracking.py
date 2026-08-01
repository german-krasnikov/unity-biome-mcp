"""MVID tracking tests — 4 Python verdict tests + 4 C# source-scan tests.

All Python tests mock _send (no Unity). C# tests regex-scan source files (no Unity).
"""
import pytest
from pathlib import Path
from unittest.mock import patch

import unity_mcp.tools.diagnose as _d

# ---------------------------------------------------------------------------
# Paths (same anchor as test_reload_stability.py:L22-23)
# ---------------------------------------------------------------------------
_PROJECT = Path(__file__).parents[2]
_PLUGIN = _PROJECT / "unity-plugin"


# ---------------------------------------------------------------------------
# Wire payloads
# ---------------------------------------------------------------------------

_CLEAN_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
"""

_STALE_DOMAIN_PAYLOAD = """\
mvid=aaaaaaaa-0000-0000-0000-000000000000
stamp=aaaaaaaa-0000-0000-0000-000000000000:100
compile=idle|3.0
sync=ready  epoch=1
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:100:fresh
errors=
log=clean
"""

# stamp_frozen=true but iscompiling=false → NOT WEDGE-ENGINE
_FROZEN_NO_COMPILE_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|3.0
sync=ready  epoch=1
iscompiling=false  cn_active=false  started=false  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
"""


# ---------------------------------------------------------------------------
# Fixture
# ---------------------------------------------------------------------------

@pytest.fixture(autouse=True)
def _reset_send():
    original = _d._send
    yield
    _d._send = original


def _make_send(payload: str):
    async def _send(cmd, args=None, **kwargs):
        if cmd == "diagnose":
            return payload
        raise AssertionError(f"Unexpected cmd: {cmd}")
    return _send


# ===========================================================================
# Group A: Python verdict tests
# ===========================================================================

@pytest.mark.asyncio
async def test_mvid_changed_between_calls_is_not_stale():
    """prev_mvid != current mvid → assembly reloaded → CLEAN-LIVE, not STALE-DOMAIN.

    Slot 11 condition is (prev_mvid and mvid and mvid == prev_mvid).
    Different MVIDs → condition false → falls through to CLEAN-LIVE (slot 14).
    """
    _d._send = _make_send(_CLEAN_PAYLOAD)
    # prev_mvid is different from the payload's mvid (60d2de34...)
    result = await _d.diagnose(prev_mvid="aaaaaaaa-0000-0000-0000-000000000000")
    assert result == "CLEAN-LIVE", f"Different MVID → CLEAN-LIVE, got: {result!r}"


@pytest.mark.asyncio
async def test_mvid_frozen_expected_compile_false_yields_noop():
    """Same MVID + expected_compile=False → NO-OP (slot 12, cache-hit / reverted edit).

    Slot 11/12: prev_mvid == mvid but expected_compile=False → NO-OP, not STALE-DOMAIN.
    """
    _d._send = _make_send(_STALE_DOMAIN_PAYLOAD)
    result = await _d.diagnose(
        prev_mvid="aaaaaaaa-0000-0000-0000-000000000000",
        expected_compile=False,
    )
    assert result == "NO-OP", f"Cache-hit (no compile expected) → NO-OP, got: {result!r}"


@pytest.mark.asyncio
async def test_stamp_frozen_without_iscompiling_does_not_wedge():
    """stamp_frozen=true alone is NOT sufficient for WEDGE-ENGINE.

    Slot 7 requires: iscompiling=true AND cn_active=false AND stamp_frozen=true.
    OR: stale_latch = iscompiling=true AND is_really_compiling=false AND stamp_frozen=true.
    With iscompiling=false, neither branch fires → falls through to CLEAN-LIVE.
    """
    _d._send = _make_send(_FROZEN_NO_COMPILE_PAYLOAD)
    result = await _d.diagnose()
    assert result == "CLEAN-LIVE", f"stamp_frozen alone must not wedge, got: {result!r}"


@pytest.mark.asyncio
async def test_guard_rejected_response_yields_unknown():
    """Guard-reject text ('Unity is compiling. Retry in 2s.') → UNKNOWN.

    Parser sets guard_rejected=True and stamp=UNDETERMINED.
    Slot 2: stamp==UNDETERMINED → UNKNOWN.
    """
    _d._send = _make_send("Unity is compiling. Retry in 2s.")
    result = await _d.diagnose()
    assert result == "UNKNOWN", f"Guard-reject wire must → UNKNOWN, got: {result!r}"


# ===========================================================================
# Group B: C# source-scan tests (no mock, no Unity)
# ===========================================================================

def test_compute_stamp_uses_module_version_id():
    """ComputeStamp() uses ModuleVersionId for per-assembly IL hash.

    Replacing with GetHashCode() or timestamp would lose identity semantics.
    Source: unity-plugin/Editor/SyncHelper.cs
    """
    src = (_PLUGIN / "Editor/SyncHelper.cs").read_text(encoding="utf-8")
    assert "ModuleVersionId" in src, "SyncHelper.cs must reference ModuleVersionId"


def test_compute_stamp_filters_unity_mcp_prefix():
    """ComputeStamp() must only hash UnityMCP.* assemblies.

    Hashing all AppDomain assemblies would produce different stamps on each launch.
    Source: unity-plugin/Editor/SyncHelper.cs
    """
    src = (_PLUGIN / "Editor/SyncHelper.cs").read_text(encoding="utf-8")
    assert 'StartsWith("UnityMCP.")' in src, (
        'SyncHelper.cs must filter assemblies with StartsWith("UnityMCP.")'
    )


def test_diagnose_command_mvid_is_first_stamp_segment():
    """mvid= field is stamp.Split(':')[0] — Python parser relies on this extraction.

    If extraction logic changes, Python's mvid-based STALE-DOMAIN detection breaks.
    Source: unity-plugin/Editor/DiagnoseCommand.cs
    """
    src = (_PLUGIN / "Editor/DiagnoseCommand.cs").read_text(encoding="utf-8")
    assert "stamp.Split(':')[0]" in src, (
        "DiagnoseCommand.cs must extract mvid as stamp.Split(':')[0]"
    )


def test_build_version_string_includes_stamp_field():
    """BuildVersionString() appends |stamp:{stamp} so get_version carries the domain stamp.

    Bridge uses this stamp for stale-DLL detection after reconnect.
    Source: unity-plugin/Editor/MCPServer.cs
    """
    src = (_PLUGIN / "Editor/MCPServer.cs").read_text(encoding="utf-8")
    assert '|stamp:{stamp}' in src, (
        'MCPServer.cs BuildVersionString must include |stamp:{stamp} interpolation'
    )
