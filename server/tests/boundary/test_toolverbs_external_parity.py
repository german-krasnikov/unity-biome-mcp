"""Parity test for ExternalToolVerbs.g.cs vs ToolVerbMap._externalMap.

Wave 2C-3: external tool verbs (Claude Code built-ins) moved from a hardcoded
dict in ToolVerbMap.cs to dsl/data/toolverbs-external.core-data.
This test verifies the generated rows match the expected entries.
"""
import re
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

_REPO_ROOT = Path(__file__).resolve().parents[3]
_CORE_DATA = _REPO_ROOT / "dsl" / "data" / "toolverbs-external.core-data"
_CS_FILE = _REPO_ROOT / "unity-plugin" / "Editor" / "Generated" / "ExternalToolVerbs.g.cs"
_PY_FILE = _REPO_ROOT / "server" / "src" / "unity_mcp" / "generated" / "external_tool_verbs.py"

# 15 external (non-MCP) tool entries from ToolVerbMap.cs._externalMap
_EXPECTED_TOOLS = {
    "Agent": "Agent",
    "Task": "Agent",
    "TodoWrite": "Updated tasks",
    "TaskCreate": "Creating task",
    "TaskUpdate": "Updating task",
    "Edit": "Editing file",
    "Write": "Writing file",
    "MultiEdit": "Editing files",
    "Bash": "Running command",
    "Read": "Reading file",
    "LS": "Listing files",
    "Glob": "Searching files",
    "Grep": "Searching",
    "WebFetch": "Fetching",
    "WebSearch": "Searching web",
}

_ENTRY_RE = re.compile(
    r'new ExternalToolVerb\(tool:\s*"([^"]+)",\s*verb:\s*"([^"]+)"\)'
)


def test_core_data_file_exists():
    """dsl/data/toolverbs-external.core-data must exist."""
    assert _CORE_DATA.exists(), (
        f"{_CORE_DATA} not found — run Wave 2C-3 to create it"
    )


def test_core_data_has_expected_count():
    """core-data must have exactly 15 EXTERNALTOOLVERB rows."""
    text = _CORE_DATA.read_text(encoding="utf-8")
    count = text.count("EXTERNALTOOLVERB {")
    assert count == 15, f"Expected 15 rows, got {count}"


def test_py_generated_file_exists():
    """server/src/unity_mcp/generated/external_tool_verbs.py must exist."""
    assert _PY_FILE.exists(), (
        f"{_PY_FILE} not found — run Wave 2C-3 to generate it"
    )


def test_py_rows_match_expected():
    """EXTERNAL_TOOL_VERB_ROWS must match the 15 expected entries."""
    from unity_mcp.generated.external_tool_verbs import EXTERNAL_TOOL_VERB_ROWS
    actual = {r.tool: r.verb for r in EXTERNAL_TOOL_VERB_ROWS}
    assert actual == _EXPECTED_TOOLS, (
        f"EXTERNAL_TOOL_VERB_ROWS does not match expected; check toolverbs-external.core-data"
    )


def test_cs_generated_file_exists():
    """unity-plugin/Editor/Generated/ExternalToolVerbs.g.cs must exist."""
    assert _CS_FILE.exists(), (
        f"{_CS_FILE} not found — run Wave 2C-3 to generate it"
    )


def test_cs_rows_match_expected():
    """ExternalToolVerbs.g.cs must contain exactly the 15 expected entries."""
    text = _CS_FILE.read_text(encoding="utf-8")
    actual = dict(_ENTRY_RE.findall(text))
    assert actual, "No ExternalToolVerb entries parsed from .g.cs — regex drifted"
    assert actual == _EXPECTED_TOOLS, (
        "ExternalToolVerbs.g.cs and expected entries diverged — run biome generate"
    )


def test_toolverbmap_references_generated_rows():
    """ToolVerbMap.cs must reference ExternalToolVerbRows (not a hardcoded dict)."""
    toolverbmap = _REPO_ROOT / "unity-plugin" / "Editor" / "Chat" / "CLI" / "ToolVerbMap.cs"
    text = toolverbmap.read_text(encoding="utf-8")
    assert "ExternalToolVerbRows" in text, (
        "ToolVerbMap.cs does not reference ExternalToolVerbRows — "
        "update it to use the generated rows (Wave 2C-3)"
    )
