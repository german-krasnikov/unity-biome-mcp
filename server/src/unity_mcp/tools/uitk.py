"""UI Toolkit tools: inspect_uitk, lint_uitk, uitk_element."""
from typing import Literal

from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def inspect_uitk(
    path: str | None = None,
    depth: int | None = None,
    selector: str | None = None,
    filter: str | None = None,
    show_unity_private: bool | None = None,
    show_style: bool | None = None,
) -> str:
    """Inspect the VisualElement tree of a UIDocument panel
    (UI Toolkit only — use `get_component` for UIDocument component fields,
    use `get_hierarchy` for the scene GameObject tree,
    use `create_ui` for uGUI Canvas elements).
    Returns compact text tree with ~N refids; pass ~N to uitk_element as selector.
    path: scene path to UIDocument GameObject (e.g. /HUD), or 'scene' to list all.
    depth: max traversal depth (default 4; use selector to focus a subtree).
    selector: start tree from first matching element (name, .class, TypeName, ~refid).
    filter: show only elements whose name or classes contain this substring.
    show_unity_private: show #unity-* prefixed elements normally hidden by default.
    show_style: include non-default computed style values per element.
    """
    return await _send("inspect_uitk", _args(
        path=path, depth=depth, selector=selector,
        filter=filter, include_internal=show_unity_private, show_style=show_style,
    ))


async def lint_uitk(
    path: str | None = None,
    fix: bool | None = None,
) -> str:
    """Validate a UXML or USS file for structural errors and broken references
    (use `get_compile_errors` for C# compile errors,
    use `verify_after_change` for multi-gate scene verification after mutations).
    Checks: well-formed XML (UXML), broken <Style src> refs, missing <Template src> deps,
    CamelCase class names (use kebab-case), star selectors, duplicate CSS variables.
    fix: auto-remove unsupported CSS properties and normalize format.
    path: Assets/ path to UXML or USS file.
    """
    return await _send("lint_uitk", _args(path=path, fix=fix))


async def uitk_element(
    action: Literal["query", "get", "set_style", "add_class", "remove_class", "get_style", "enable", "disable"],
    path: str | None = None,
    ref: str | None = None,
    selector: str | None = None,
    name: str | None = None,
    value: str | None = None,
    property: str | None = None,
    class_name: str | None = None,
) -> str:
    """Mutate or query a VisualElement in a UIDocument
    (use inspect_uitk to find elements first, then pass ~N ref for zero-token addressing;
    use set_property for serialized component fields on the UIDocument GameObject;
    use create_ui for uGUI Canvas elements).
    action: query (find elements) | get (read value/text) | set_style | add_class | remove_class | get_style | enable | disable.
    Element addressing priority: ref (~N from inspect_uitk) → name → selector (CSS class/type).
    path: scene path to UIDocument GameObject (e.g. /HUD).
    ref: ~N refid from inspect_uitk (highest priority, stale after re-inspect or domain reload).
    selector: CSS selector — .class-name, TypeName, or element name.
    name: element name (equivalent to bare name in selector).
    value: value to write (for set_style/add_class).
    property: CSS property name for set_style/get_style.
    class_name: USS class name for add_class/remove_class (no leading dot).
    warn: set_style/add_class/remove_class/enable/disable in Play Mode — change not persisted.
    """
    return await _send("uitk_element", _args(
        action=action, path=path, ref=ref, selector=selector,
        name=name, value=value, property=property, class_name=class_name,
    ))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(inspect_uitk)
    mcp.tool(annotations=_RO)(lint_uitk)
    mcp.tool(annotations=_RW)(uitk_element)
