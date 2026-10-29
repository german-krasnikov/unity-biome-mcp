"""Tier 0 of the freshness of the tool catalog, without Node: every generated file carries the sha256 of the dsl/tools.biome it was printed from.

It catches "the catalog was edited and scripts/dsl.sh generate was not run". A hand edit of a generated file, or a change of the
construct, is what tier 1 finds: scripts/dsl.sh check (generate --check, needs Node and the biome-dsl checkout of the TOOLCHAIN line).
"""
import hashlib
import re
from pathlib import Path

DSL = Path(__file__).resolve().parents[2] / "dsl"
# GENERATE <name> render {dir}/<source> <target> {dir}/<out>: a whole file starts with the banner, a region (out#region) does not
_RENDER = re.compile(r"^GENERATE\s+\S+\s+render\s+\{dir\}/(\S+)\s+\S+\s+\{dir\}/([^\s#]+)$", re.MULTILINE)


def source_sha256(source: Path) -> str:
    """What the banner states: the sha256 of the source text without a BOM and with LF line ends."""
    text = re.sub(r"\r\n?", "\n", source.read_bytes().decode("utf-8").removeprefix("\ufeff"))
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def stale_outputs(steps: list[tuple[Path, Path]]) -> list[str]:
    """The outputs whose banner (line 2) does not state the digest of their source, or that are missing."""
    stale = []
    for source, out in steps:
        lines = out.read_text(encoding="utf-8").splitlines() if out.exists() else []
        if len(lines) < 2 or not lines[1].endswith(f"source-sha256={source_sha256(source)}"):
            stale.append(out.name)
    return stale


def test_every_generated_file_was_printed_from_the_current_catalog():
    steps = [(DSL / source, (DSL / out).resolve()) for source, out in _RENDER.findall((DSL / "biome.project").read_text(encoding="utf-8"))]
    assert steps, "dsl/biome.project has no whole-file render step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_an_edited_source_or_a_missing_output_is_stale(tmp_path):
    source, out = tmp_path / "tools.biome", tmp_path / "out.py"
    source.write_bytes(b"\xef\xbb\xbfTOOL a\r\nEND\r\n")
    assert source_sha256(source) == hashlib.sha256(b"TOOL a\nEND\n").hexdigest()  # no BOM, LF line ends
    out.write_text(f"# Generated from dsl/tools.biome\n# source-sha256={source_sha256(source)}\n", encoding="utf-8")
    assert stale_outputs([(source, out)]) == []
    source.write_bytes(b"TOOL b\nEND\n")
    assert stale_outputs([(source, out), (source, tmp_path / "missing.py")]) == ["out.py", "missing.py"]
