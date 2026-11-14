"""Parity test for ChipKinds.g.cs vs built-in chip providers.

Wave 2C-1: chip kind data (key, hexColor, defaultDepth, priority) moved
from scattered provider files to dsl/data/chipkinds.core-data.
"""
import re
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

_REPO_ROOT = Path(__file__).resolve().parents[3]
_CORE_DATA = _REPO_ROOT / "dsl" / "data" / "chipkinds.core-data"
_CS_FILE = _REPO_ROOT / "unity-plugin" / "Editor" / "Generated" / "ChipKinds.g.cs"
_PY_FILE = _REPO_ROOT / "server" / "src" / "unity_mcp" / "generated" / "chip_kinds.py"

# 18 built-in chip kinds: (key, hexColor, defaultDepth, priority)
_EXPECTED_KINDS = {
    "hierarchy":          ("#4a9eff",  "path",    100),
    "scene":              ("#c084fc",  "path",    200),
    "script":             ("#4ade80",  "path",    300),
    "prefab":             ("#60a5fa",  "path",    400),
    "material":           ("#f97316",  "path",    500),
    "texture":            ("#facc15",  "path",    600),
    "so":                 ("#fb7185",  "path",    700),
    "folder":             ("#a78bfa",  "path",    150),
    "asset":              ("#94a3b8",  "path",    2147483647),
    "image":              ("#f472b6",  "path",    50),
    "model":              ("#34d399",  "path",    450),
    "audio":              ("#a78bfa",  "path",    550),
    "region":             ("#22d3ee",  "summary", 120),
    "field":              ("#f59e0b",  "summary", 130),
    "component":          ("#38bdf8",  "summary", 125),
    "annotated_screenshot": ("#e74c3c", "summary", 40),
    "agent":              ("#9b59b6",  "path",    5),
    "profile":            ("#f0a030",  "full",    75),
}

_ENTRY_RE = re.compile(
    r'new ChipKind\(key:\s*"([^"]+)",\s*hexColor:\s*"([^"]+)",\s*defaultDepth:\s*"([^"]+)",\s*priority:\s*(\d+)\)'
)


def test_core_data_file_exists():
    """dsl/data/chipkinds.core-data must exist."""
    assert _CORE_DATA.exists(), (
        f"{_CORE_DATA} not found — run Wave 2C-1 to create it"
    )


def test_core_data_has_expected_count():
    """core-data must have exactly 18 CHIPKIND rows."""
    text = _CORE_DATA.read_text(encoding="utf-8")
    count = text.count("CHIPKIND {")
    assert count == 18, f"Expected 18 rows, got {count}"


def test_py_generated_file_exists():
    """server/src/unity_mcp/generated/chip_kinds.py must exist."""
    assert _PY_FILE.exists(), (
        f"{_PY_FILE} not found — run Wave 2C-1 to generate it"
    )


def test_py_rows_match_expected():
    """CHIP_KIND_ROWS must match the 18 expected entries."""
    from unity_mcp.generated.chip_kinds import CHIP_KIND_ROWS
    actual = {r.key: (r.hexColor, r.defaultDepth, r.priority) for r in CHIP_KIND_ROWS}
    assert actual == _EXPECTED_KINDS, (
        "CHIP_KIND_ROWS does not match expected; check chipkinds.core-data"
    )


def test_cs_generated_file_exists():
    """unity-plugin/Editor/Generated/ChipKinds.g.cs must exist."""
    assert _CS_FILE.exists(), (
        f"{_CS_FILE} not found — run Wave 2C-1 to generate it"
    )


def test_cs_rows_match_expected():
    """ChipKinds.g.cs must contain exactly the 18 expected entries."""
    text = _CS_FILE.read_text(encoding="utf-8")
    actual = {}
    for m in _ENTRY_RE.finditer(text):
        key, hex_color, depth, prio = m.group(1), m.group(2), m.group(3), int(m.group(4))
        actual[key] = (hex_color, depth, prio)
    assert actual, "No ChipKind entries parsed from .g.cs — regex drifted"
    assert actual == _EXPECTED_KINDS, (
        "ChipKinds.g.cs and expected entries diverged — run biome generate"
    )
