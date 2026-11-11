"""The C# that biome-dsl prints into the assemblies of the plugin has the .meta files Unity needs (step 7.2; the playtest grammar of step 7.5 D1 joins the Editor).

`biome generate` writes the .g.cs and nothing beside it. Unity makes a missing .meta by itself in a project, but in the immutable
folder of a package it only warns and the file is not imported with a stable GUID. So each *.g.cs of unity-plugin/Editor/Generated and of
unity-plugin/Runtime/Playtest/Core/Generated has a .meta with an importer of its kind and a GUID that no other .meta of the plugin has, and so has the folder.
"""
import re
from pathlib import Path

import pytest

_PLUGIN = Path(__file__).resolve().parents[2] / "unity-plugin"
_ROOTS = (_PLUGIN / "Editor" / "Generated", _PLUGIN / "Runtime" / "Playtest" / "Core" / "Generated")
_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)


def _guid(meta: Path) -> str:
    match = _GUID.search(meta.read_text(encoding="utf-8"))
    assert match, f"{meta.name} has no guid"
    return match.group(1)


@pytest.mark.parametrize("root", _ROOTS, ids=lambda root: root.parent.name)
def test_every_generated_cs_file_has_a_mono_importer_meta(root):
    files = sorted(root.glob("*.g.cs"))
    assert files, "no generated C# file was found: this test would pass without checking"
    for file in files:
        meta = file.with_name(file.name + ".meta")
        assert meta.is_file(), f"{file.name} has no .meta: write it with the new file"
        assert "MonoImporter:" in meta.read_text(encoding="utf-8"), file.name


@pytest.mark.parametrize("root", _ROOTS, ids=lambda root: root.parent.name)
def test_the_generated_folder_has_a_folder_meta(root):
    meta = root.with_name("Generated.meta")
    assert meta.is_file()
    assert "folderAsset: yes" in meta.read_text(encoding="utf-8")


def test_no_guid_of_the_generated_files_is_used_twice_in_the_plugin():
    ours = [meta for root in _ROOTS for meta in (root.with_name("Generated.meta"), *root.glob("*.g.cs.meta"))]
    theirs = {found.group(1) for meta in _PLUGIN.rglob("*.meta") if meta not in ours and (found := _GUID.search(meta.read_text(encoding="utf-8")))}
    guids = [_guid(meta) for meta in ours]
    assert len(set(guids)) == len(guids), "two generated files share a guid"
    assert not set(guids) & theirs


def test_every_file_and_folder_of_the_editor_has_a_meta():
    """`biome generate` never writes a .meta, and the files around it (the driver and the ports of the chat turn, step 7.3) need one like the rest."""
    editor = _PLUGIN / "Editor"
    missing = [p.relative_to(_PLUGIN).as_posix() for p in sorted(editor.rglob("*"))
               if not p.name.endswith(".meta") and not p.name.startswith(".") and not p.with_name(p.name + ".meta").is_file()]
    assert missing == [], f"no .meta for: {missing}"
