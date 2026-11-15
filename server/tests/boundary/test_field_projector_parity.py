"""Parity test for FieldAliases.g.cs vs compressor.py _FIELD_ALIASES.

Wave 1D: both sides now derive from dsl/data/fieldaliases.core-data via FIELD_ALIAS_ROWS.
This test verifies that compressor.py correctly builds _FIELD_ALIASES from the generated
rows, and that FieldAliases.g.cs contains the same data (content-locked by DSL pipeline).
"""
import re
from pathlib import Path

import pytest

from unity_mcp.compressor import _FIELD_ALIASES
from unity_mcp.generated.field_aliases import FIELD_ALIAS_ROWS

pytestmark = pytest.mark.csharp_parity

_FIELD_ALIASES_CS = Path(__file__).resolve().parents[3] / "unity-plugin" / "Editor" / "Generated" / "FieldAliases.g.cs"
_ENTRY_RE = re.compile(r'new FieldAlias\(alias:\s*"([^"]+)",\s*canonical:\s*"([^"]+)"\)')


def _parse_csharp_aliases() -> dict[str, str]:
    text = _FIELD_ALIASES_CS.read_text(encoding="utf-8")
    return dict(_ENTRY_RE.findall(text))


def test_field_aliases_rows_match_compressor_field_aliases():
    """compressor.py _FIELD_ALIASES is built correctly from FIELD_ALIAS_ROWS."""
    expected = {row.alias: row.canonical for row in FIELD_ALIAS_ROWS}
    assert expected, "FIELD_ALIAS_ROWS is empty: data-contract drifted"
    assert _FIELD_ALIASES == expected, (
        "compressor.py _FIELD_ALIASES does not match FIELD_ALIAS_ROWS; "
        "re-run biome generate (Wave 1D)"
    )


def test_field_aliases_cs_matches_field_alias_rows():
    """FieldAliases.g.cs contains the same data as FIELD_ALIAS_ROWS (DSL parity)."""
    csharp_aliases = _parse_csharp_aliases()
    assert csharp_aliases, "no alias entries parsed from FieldAliases.g.cs: regex drifted from source"
    expected = {row.alias: row.canonical for row in FIELD_ALIAS_ROWS}
    assert csharp_aliases == expected, (
        "FieldAliases.g.cs and FIELD_ALIAS_ROWS drifted out of sync -- run biome generate "
        "(Wave 1D, dsl/data/fieldaliases.core-data)"
    )
