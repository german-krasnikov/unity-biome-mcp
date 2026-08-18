"""Post-process FastMCP-generated inputSchema at registration time.

Called once per tool — zero cost at runtime.
Transforms: additionalProperties injection, param description injection.
anyOf, integer→number, and default type inference are intentionally NOT transformed per architect spec.
"""

from ._param_descriptions import _COMMON, PARAM_DESCRIPTIONS, PARAM_SCHEMA_EXTRAS


def postprocess_schema(tool_name: str, parameters: dict) -> None:
    """Mutate tool.parameters in place. Idempotent — safe to call twice.

    1. Inject additionalProperties: false (if properties present and not already set)
    2. Inject param descriptions from PARAM_DESCRIPTIONS or _COMMON fallback
    3. Inject extra schema properties from PARAM_SCHEMA_EXTRAS (e.g. pattern)
    """
    props = parameters.get("properties")
    if props is None:
        return

    tool_descs = PARAM_DESCRIPTIONS.get(tool_name, {})
    tool_extras = PARAM_SCHEMA_EXTRAS.get(tool_name, {})
    for pname, pdef in props.items():
        _inject_description(pname, pdef, tool_descs)
        _inject_extras(pname, pdef, tool_extras)

    parameters.setdefault("additionalProperties", False)


def _inject_description(pname: str, pdef: dict, tool_descs: dict) -> None:
    """Inject description into pdef in place. No-op if description already exists."""
    if "description" in pdef:
        return
    desc = tool_descs.get(pname) or _COMMON.get(pname)
    if desc:
        pdef["description"] = desc


def _inject_extras(pname: str, pdef: dict, tool_extras: dict) -> None:
    """Merge extra schema keys into pdef. Uses setdefault — never overwrites."""
    for k, v in tool_extras.get(pname, {}).items():
        pdef.setdefault(k, v)
