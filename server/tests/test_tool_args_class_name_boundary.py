"""ToolArgs.g.cs (TEMPLATE csargs of dsl/constructs/tool.construct) now names every accessor class
by the PascalCase of its TOOL/PYTOOL/INTERNAL name plus the literal suffix `Args` (biome-dsl commit
e9d718f4, review T2.4). Dropping the template's `@` escape is only safe if none of those 171 class
names collides with a type unity-plugin/Editor already declares by hand (outside Generated/, which
the catalog itself owns): a same-named `class`/`struct`/`enum`/`interface` in the same `UnityMCP.Editor`
namespace would not compile. This file is that proof, re-run on every change to the catalog or the
hand-written plugin source, and a focused unit test that the collision check itself can fail.

`_to_pascal_case` is an INDEPENDENT re-implementation of the toolchain's case-transform filter
(packages/kit/src/names.ts toPascalCase, read in the T2.4 review): split on a run of non-alphanumeric
characters, drop the empty parts a leading/trailing/doubled separator produces, upper-case the first
character of each remaining part, join. Nothing else.
"""
import re
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

_ROOT = Path(__file__).resolve().parents[2]
_CATALOG = _ROOT / "dsl" / "tools.biome"
_EDITOR = _ROOT / "unity-plugin" / "Editor"

_HEAD_RE = re.compile(r"^(?:TOOL|PYTOOL|INTERNAL) ([a-z][a-z0-9_]*)\b", re.MULTILINE)
_TYPE_RE = re.compile(r"\b(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)")


def _to_pascal_case(s: str) -> str:
    return "".join(part[0].upper() + part[1:] for part in re.split(r"[^a-zA-Z0-9]+", s) if part)


def _generated_class_names(catalog_text: str) -> set[str]:
    return {_to_pascal_case(m.group(1)) + "Args" for m in _HEAD_RE.finditer(catalog_text)}


def _hand_written_type_names() -> set[str]:
    names = set()
    for path in _EDITOR.rglob("*.cs"):
        if "Generated" in path.parts:
            continue  # the catalog's own output; not a pre-existing hand-written type
        names.update(_TYPE_RE.findall(path.read_text(encoding="utf-8")))
    return names


def test_no_generated_args_class_name_collides_with_an_existing_hand_written_type():
    catalog_text = _CATALOG.read_text(encoding="utf-8")
    generated = _generated_class_names(catalog_text)
    assert generated, "no TOOL/PYTOOL/INTERNAL head found: this test would pass without checking anything"
    collisions = generated & _hand_written_type_names()
    assert not collisions, f"generated *Args class(es) collide with an existing hand-written type: {sorted(collisions)}"


# Double-red proof: the test above passes today because the real catalog and the real plugin source
# have zero collisions, not because the check is inert. This feeds a crafted pair through the same
# two functions and proves the intersection is caught.
def test_collision_detector_catches_a_generated_class_name_already_declared_by_hand():
    generated = _generated_class_names('TOOL asset CORE RO "fixture"\n')
    assert generated == {"AssetArgs"}
    hand_written = {"AssetArgs", "SomeOtherType"}
    collisions = generated & hand_written
    assert collisions == {"AssetArgs"}
