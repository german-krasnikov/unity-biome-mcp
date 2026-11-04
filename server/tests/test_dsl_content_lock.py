"""Tier 0 of the content of what dsl/ generates, without Node: dsl/content-lock.json (scripts/dsl_content_lock.py) pins the sha256 of every DSL source and of every file
printed from it. test_dsl_fresh.py compares a banner with its source and cannot see a hand edit of a body under an intact banner, a changed contract or examples file, or the
machine rows (dsl/decisions/*.json have no banner): a file that is not as scripts/dsl.sh generate left it fails here.
"""
import json

import pytest
from dsl_content_lock import LOCK, REGIONS, digest, discover, generate, locked, stale
from test_dsl_fresh import DSL, feature_steps, logic_steps, render_steps

ROOT = DSL.parent
BANNER = f"// Generated from dsl/a.logic\n// source-sha256={'0' * 64}\n// Edit the source, then run biome generate.\n\n"


def write(root, name, text):
    (root / name).parent.mkdir(parents=True, exist_ok=True)
    (root / name).write_text(text, encoding="utf-8")


def lock(root, *keys):
    write(root, LOCK, json.dumps({"files": {key: digest(root, key) for key in keys}}))


def test_no_generated_file_or_dsl_source_differs_from_the_content_lock():
    bad = stale()
    assert not bad, "stale against dsl/content-lock.json (a generated file edited by hand, or a DSL source edited without generating); run scripts/dsl.sh generate:\n  " + "\n  ".join(bad)


def test_the_lock_names_every_file_that_biome_project_prints():
    project = (DSL / "biome.project").read_text(encoding="utf-8")
    printed = [out for _, out in render_steps(project) + logic_steps(project) + feature_steps(project)] + sorted((DSL / "decisions").glob("*.json"))
    names = [path.relative_to(ROOT).as_posix() for path in printed]
    pinned = locked()
    assert names, "dsl/biome.project prints nothing"
    assert [name for name in names if name not in pinned] == [], "printed by dsl/biome.project but not in the lock; run scripts/dsl.sh generate"


def test_a_hand_edit_of_a_body_under_an_intact_banner_or_of_the_rows_makes_the_file_stale(tmp_path):
    write(tmp_path, "Gen.cs", BANNER + "int a = 1;\n")
    write(tmp_path, "rows.json", '{"expected": 1}\n')
    lock(tmp_path, "Gen.cs", "rows.json")
    assert stale(tmp_path) == []
    (tmp_path / "Gen.cs").write_bytes(b"\xef\xbb\xbf" + (BANNER + "int a = 1;\n").replace("\n", "\r\n").encode())  # a BOM and CRLF are not content
    assert stale(tmp_path) == []
    write(tmp_path, "Gen.cs", BANNER + "int a = 2;\n")
    write(tmp_path, "rows.json", '{"expected": 2}\n')
    assert stale(tmp_path) == ["Gen.cs", "rows.json"]
    (tmp_path / "Gen.cs").unlink()
    assert stale(tmp_path) == ["Gen.cs", "rows.json"]


def test_a_region_is_locked_between_its_markers_and_the_text_around_it_is_not(tmp_path):
    page = "intro\n<!-- biome:begin catalog -->\n| a |\n<!-- biome:end catalog -->\noutro\n"
    write(tmp_path, "index.md", page)
    lock(tmp_path, "index.md#catalog")
    write(tmp_path, "index.md", page.replace("intro", "another intro").replace("outro", "another outro"))
    assert stale(tmp_path) == []
    write(tmp_path, "index.md", page.replace("| a |", "| b |"))
    assert stale(tmp_path) == ["index.md#catalog"]
    write(tmp_path, "index.md", "| a |\n")
    assert stale(tmp_path) == ["index.md#catalog"]


def test_discover_locks_dsl_sources_banner_files_and_manifests_but_not_prose_or_hand_written_code(tmp_path):
    files = {"dsl/a.logic": "RULE\n", "dsl/README.md": "prose\n", LOCK: "{}", "out/A.cs": BANNER + "class A {}\n", "out/.biome-files": "0 A.cs\n",
             "hand/B.cs": "// Generated from nowhere\nclass B {}\n", "hand/c.py": "x = 1\n"}
    for name, text in files.items():
        write(tmp_path, name, text)
    assert discover(tmp_path, [*files, "gone/D.cs"]) == sorted([*REGIONS, "dsl/a.logic", "out/A.cs", "out/.biome-files"])


def test_generate_locks_what_discover_takes_and_never_writes_a_region_that_is_gone(tmp_path):
    write(tmp_path, "dsl/a.logic", "RULE\n")
    with pytest.raises(SystemExit, match="docs/tools/index.md#catalog"):
        generate(tmp_path, ["dsl/a.logic"])
    assert not (tmp_path / LOCK).exists()
    write(tmp_path, "docs/tools/index.md", "<!-- biome:begin catalog -->\n| a |\n<!-- biome:end catalog -->\n")
    assert generate(tmp_path, ["dsl/a.logic"]) == 2
    assert list(locked(tmp_path)) == ["docs/tools/index.md#catalog", "dsl/a.logic"]
    assert stale(tmp_path) == []
