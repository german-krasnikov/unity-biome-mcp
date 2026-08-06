"""Post-process FastMCP-generated inputSchema at registration time.

Called once per tool — zero cost at runtime.
Transforms: additionalProperties injection, param description injection.
anyOf, integer→number, and default type inference are intentionally NOT transformed per architect spec.
"""
from __future__ import annotations

from ._param_descriptions import _COMMON, PARAM_DESCRIPTIONS


def postprocess_schema(tool_name: str, parameters: dict) -> None:
    """Mutate tool.parameters in place. Idempotent — safe to call twice.

    1. Inject additionalProperties: false (if properties present and not already set)
    2. Inject param descriptions from PARAM_DESCRIPTIONS or _COMMON fallback
    """
    props = parameters.get("properties")
    if props is None:
        return

    tool_descs = PARAM_DESCRIPTIONS.get(tool_name, {})
    for pname, pdef in props.items():
        _inject_description(pname, pdef, tool_descs)

    parameters.setdefault("additionalProperties", False)


def _inject_description(pname: str, pdef: dict, tool_descs: dict) -> None:
    """Inject description into pdef in place. No-op if description already exists."""
    if "description" in pdef:
        return
    desc = tool_descs.get(pname) or _COMMON.get(pname)
    if desc:
        pdef["description"] = desc
