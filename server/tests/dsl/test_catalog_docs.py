"""The catalog table of docs/tools/index.md lists the tools of the catalog (not its _INTERNAL wire commands), as their ToolSpec says (the plane) and the catalog states (category, effect)."""
import re
from pathlib import Path

from unity_mcp.tools.generated import catalog_specs
from unity_mcp.tools.tool_specs import _SPECS

_INDEX = Path(__file__).resolve().parents[3] / "docs" / "tools" / "index.md"


def test_the_catalog_table_of_the_tools_reference_lists_the_catalog_tools():
    region = re.search(r"<!-- biome:begin catalog -->\n(.*?)\n?<!-- biome:end catalog -->", _INDEX.read_text(encoding="utf-8"), re.DOTALL)
    assert region, "docs/tools/index.md has no catalog region"
    rows = [f"| `{name}` | {_SPECS[name].plane} | {kwargs['category']} | {kwargs['effect']} |" for name, kwargs in catalog_specs.SPEC_KWARGS.items() if kwargs["category"] != "_INTERNAL"]
    assert region.group(1).splitlines() == ["| Tool | Plane | Category | Effect |", "|---|---|---|---|", *rows]
