"""ToolArgs.g.cs (TEMPLATE csargs of dsl/constructs/tool.construct) now names every accessor class
by the PascalCase of its TOOL/PYTOOL/INTERNAL name plus the literal suffix `Args` (biome-dsl commit
e9d718f4, review T2.4). Dropping the template's `@` escape is only safe if none of those generated
class names collides with a type unity-plugin/Editor already declares by hand (outside Generated/,
which the catalog itself owns): a same-named `class`/`struct`/`enum`/`interface` in the same
`UnityMCP.Editor` namespace would not compile. This file is that proof, re-run on every change to
the catalog or the hand-written plugin source.

This file does NOT re-implement the PascalCase transform. That algorithm (and its edge cases --
consecutive separators, a leading separator, a digit-leading part, etc.) is pinned once, in the C#
pure lane, by unity-plugin/Tests~/Decisions/ToolArgsPascalCaseTests.cs against
unity-plugin/Tests~/Decisions/CatalogToolBlocks.cs's ToPascalCase. Re-deriving it here would be a
second implementation of the same algorithm drifting from the one the toolchain actually runs; instead
this file reads the generated class names directly out of the real
unity-plugin/Editor/Generated/ToolArgs.g.cs, so the only thing it uniquely checks is the boundary: do
those generated names collide with existing hand-written types.
"""
import re
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

_ROOT = Path(__file__).resolve().parents[3]
_GENERATED = _ROOT / "unity-plugin" / "Editor" / "Generated" / "ToolArgs.g.cs"
_EDITOR = _ROOT / "unity-plugin" / "Editor"

_GENERATED_CLASS_RE = re.compile(r"internal static class ([A-Za-z_][A-Za-z0-9_]*)")
_TYPE_RE = re.compile(r"\b(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)")


def _generated_class_names(generated_text: str) -> set[str]:
    return set(_GENERATED_CLASS_RE.findall(generated_text))


def _hand_written_type_names(root: Path) -> set[str]:
    names = set()
    for path in root.rglob("*.cs"):
        if "Generated" in path.parts:
            continue  # the catalog's own output; not a pre-existing hand-written type
        names.update(_TYPE_RE.findall(path.read_text(encoding="utf-8")))
    return names


def test_no_generated_args_class_name_collides_with_an_existing_hand_written_type():
    generated = _generated_class_names(_GENERATED.read_text(encoding="utf-8"))
    assert generated, "no generated accessor class found: this test would pass without checking anything"
    collisions = generated & _hand_written_type_names(_EDITOR)
    assert not collisions, f"generated *Args class(es) collide with an existing hand-written type: {sorted(collisions)}"


# Double-red proof: the test above passes today because the real generated file and the real plugin
# source have zero collisions, not because the check is inert. This plants a hand-written type named
# after a REAL generated class in a tmp_path scratch directory (never under unity-plugin/) and scans
# that directory in place of _EDITOR, proving the intersection is caught.
def test_collision_detector_catches_a_generated_class_name_already_declared_by_hand(tmp_path):
    generated = _generated_class_names(_GENERATED.read_text(encoding="utf-8"))
    assert generated, "no generated accessor class found"
    victim = sorted(generated)[0]
    (tmp_path / "Fixture.cs").write_text(f"namespace UnityMCP.Editor {{ class {victim} {{}} }}\n", encoding="utf-8")

    collisions = generated & _hand_written_type_names(tmp_path)

    assert collisions == {victim}
