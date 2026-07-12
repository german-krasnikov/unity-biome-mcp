"""Tests for run_playtest tool."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.server import run_playtest
from unity_mcp.tools.runtime import _compress_report


async def test_run_playtest_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 3 steps"}
    script = "WAIT 1\nASSERT_CONSOLE_CLEAN"
    result = await run_playtest(script)
    mock_bridge.send.assert_called_once_with(
        "run_playtest",
        {"script": script, "timeout": "120.0"},
        timeout=140.0,
    )
    assert result == "PASS: 3 steps"


async def test_run_playtest_timeout_passthrough(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("WAIT 1", timeout=60.0)
    call = mock_bridge.send.call_args
    assert call[0][1]["timeout"] == "60.0"
    assert call[1]["timeout"] == 80.0


async def test_run_playtest_default_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG hi")
    call = mock_bridge.send.call_args
    assert call[0][1]["timeout"] == "120.0"
    assert call[1]["timeout"] == 140.0


def test_compress_report_all_pass_returns_compact():
    report = "PLAYTEST: 3/3 (1.2s) OK"
    assert _compress_report(report) == report


def test_compress_report_strips_passing_lines():
    report = "PLAYTEST: 2/3 (1.0s)\n[1] ASSERT HP==100 — PASS (100)\n[2] ASSERT Money>500 — FAIL (100)\n[3] LOG check"
    result = _compress_report(report)
    assert "FAIL" in result
    assert "LOG check" in result
    assert "PASS" not in result


def test_compress_report_short_passthrough():
    assert _compress_report("OK") == "OK"
    assert _compress_report("") == ""


def test_compress_report_snapshot_kept():
    report = "PLAYTEST: 2/2 (1.0s)\n[1] SNAPSHOT\nhp=100\n[2] ASSERT HP==100 — PASS"
    result = _compress_report(report)
    assert "SNAPSHOT" in result
    assert "hp=100" in result


@pytest.mark.parametrize("marker,stripped_pattern", [
    ("— done", "done"),
    ("— ok", "— ok"),
    ("— PASS", "PASS"),
])
def test_compress_report_strips_all_pass_markers(marker, stripped_pattern):
    """All three passing markers (— PASS, — done, — ok) are stripped from output."""
    report = f"PLAYTEST: 2/2\n[1] STEP {marker} (1.2s)\n[2] ASSERT — FAIL"
    result = _compress_report(report)
    assert stripped_pattern not in result
    assert "FAIL" in result


# ── defs param tests ──────────────────────────────────────────────────────────

async def test_defs_prepended_to_script(mock_bridge):
    """defs lines are prepended (as VAL ...) before the actual script."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 1/1"}
    await run_playtest("ASSERT $hp == 100", defs="hp /P|HP|health")
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert sent.startswith("VAL hp /P|HP|health\n")
    assert "ASSERT $hp == 100" in sent


async def test_defs_auto_val_prefix(mock_bridge):
    """Lines without VAL prefix get it added automatically."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG ok", defs="hp /P|HP|h")
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert "VAL hp /P|HP|h" in sent


async def test_defs_already_prefixed_no_double(mock_bridge):
    """Lines already starting with VAL are NOT double-prefixed."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG ok", defs="VAL hp /P|HP|h")
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert sent.count("VAL") == 1


async def test_defs_none_script_unchanged(mock_bridge):
    """P0: defs=None (default) must not alter the script at all."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    script = "MOVE TO 0,0,0\nASSERT_CONSOLE_CLEAN"
    await run_playtest(script, defs=None)
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert sent == script


async def test_defs_multiline(mock_bridge):
    """Multiple defs lines are all prepended in order."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG ok", defs="hp /P|HP|h\nspeed /P|RB|v")
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert "VAL hp /P|HP|h" in sent
    assert "VAL speed /P|RB|v" in sent
    assert sent.index("VAL hp") < sent.index("LOG ok")
    assert sent.index("VAL speed") < sent.index("LOG ok")


async def test_defs_blank_lines_stripped(mock_bridge):
    """Blank/whitespace-only lines in defs are not emitted."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    defs = "\n  \nhp /P|HP|h\n\nspeed /P|RB|v\n  \n"
    await run_playtest("LOG done", defs=defs)
    sent = mock_bridge.send.call_args[0][1]["script"]
    val_lines = [ln for ln in sent.splitlines() if ln.startswith("VAL")]
    assert len(val_lines) == 2
    assert "VAL hp /P|HP|h" in sent
    assert "VAL speed /P|RB|v" in sent


async def test_defs_case_insensitive_prefix(mock_bridge):
    """VAL prefix check is case-insensitive — no double-prefix for val/Val."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    defs = "VAL hp /P|HP|h\nval speed /P|RB|v\nVal score /P|S|pts"
    await run_playtest("LOG x", defs=defs)
    sent = mock_bridge.send.call_args[0][1]["script"]
    for line in sent.splitlines():
        if "VAL" in line.upper() and line.upper().startswith("VAL"):
            assert line.upper().count("VAL") == 1, f"Double prefix: {line!r}"


async def test_defs_val_prefix_normalized_to_uppercase(mock_bridge):
    """Lowercase 'val' prefix is normalized to uppercase 'VAL'."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG x", defs="val foo = bar")
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert "VAL foo = bar" in sent
    assert sent.count("val ") == 0, f"Lowercase val remains: {sent!r}"


# ── Wave 6.8: defs comment lines ignored ─────────────────────────────────────


@pytest.mark.asyncio
async def test_defs_comment_lines_skipped(mock_bridge):
    """Comment lines in defs (# ...) must not be turned into VAL entries."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    defs = "# player vars\nhp /P|HP|health\n# end"
    await run_playtest("LOG ok", defs=defs)
    sent = mock_bridge.send.call_args[0][1]["script"]
    assert "VAL # player vars" not in sent
    assert "VAL # end" not in sent
    assert "VAL hp /P|HP|health" in sent


# ── Wave 6.7: defs + inline VAL collision — last-wins semantics ───────────────

async def test_defs_inline_collision_script_wins(mock_bridge):
    """When defs and script both define $hp, defs comes first so script's VAL overrides (last-wins)."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("VAL $hp 100\nASSERT $hp == 100", defs="hp /P|HP|health")
    sent = mock_bridge.send.call_args[0][1]["script"]
    # defs is prepended before the script → script's VAL comes later → wins in CollectVals
    assert "VAL hp /P|HP|health" in sent
    assert "VAL $hp 100" in sent
    idx_defs = sent.index("VAL hp /P|HP|health")
    idx_script = sent.index("VAL $hp 100")
    assert idx_defs < idx_script  # defs first, script after → script overrides
