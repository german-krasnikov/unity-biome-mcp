"""Parity test for FieldProjector.cs Aliases vs compressor.py _FIELD_ALIASES.

DSL_GAP_ANALYSIS.md drift #4: FieldProjector.cs is a hand-written "port of compressor.py
project_fields()" with its own copy of the 12 field aliases. Nothing cross-checked the two
tables, so they could silently drift apart. This test reads both and asserts they match.
"""
import re
from pathlib import Path

import pytest

from unity_mcp.compressor import _FIELD_ALIASES

pytestmark = pytest.mark.csharp_parity

_FIELD_PROJECTOR_CS = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor" / "FieldProjector.cs"
_ENTRY_RE = re.compile(r'\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}')


def _parse_csharp_aliases() -> dict[str, str]:
    text = _FIELD_PROJECTOR_CS.read_text(encoding="utf-8")
    start = text.index("Aliases = new Dictionary<string, string>")
    end = text.index("};", start)
    return dict(_ENTRY_RE.findall(text[start:end]))


def test_field_projector_aliases_match_compressor_field_aliases():
    csharp_aliases = _parse_csharp_aliases()
    assert csharp_aliases, "no alias entries parsed from FieldProjector.cs: regex drifted from source"
    assert csharp_aliases == _FIELD_ALIASES, (
        "FieldProjector.cs Aliases and compressor.py _FIELD_ALIASES drifted out of sync -- keep "
        "both tables identical (DSL_GAP_ANALYSIS.md drift #4)"
    )
