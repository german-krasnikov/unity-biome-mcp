"""Tier 0 of the freshness of what dsl/ generates, without Node: every generated file carries the sha256 of the source it was printed from.

It catches "the catalog (or a decision) was edited and scripts/dsl.sh generate was not run". A hand edit of a generated file, a change
of the construct or a contract, and the machine rows of the examples (dsl/decisions/*.json, which have no banner) are what tier 1
finds: scripts/dsl.sh check (generate --check, needs Node and the biome-dsl checkout of the TOOLCHAIN line).
"""
import hashlib
import re
from pathlib import Path

DSL = Path(__file__).resolve().parents[2] / "dsl"
# GENERATE <name> render {dir}/<source> <target> {dir}/<out>: a whole file starts with the banner, a region (out#region) does not
_RENDER = re.compile(r"^GENERATE\s+\S+\s+render\s+\{dir\}/(\S+)\s+\S+\s+\{dir\}/([^\s#]+)$", re.MULTILINE)
# GENERATE <name> logic generate --source {dir}/<m>.logic --contract ... --target "<lang> {dir}/<out> [AS <name>] [LANG <v>]"...
_LOGIC = re.compile(r"^GENERATE\s+\S+\s+logic generate\s+--source\s+\{dir\}/(\S+)\s+--contract\s+\S+((?:\s+--target\s+\"[^\"]*\")+)\s*$",
                    re.MULTILINE)
_TARGET = re.compile(r'--target\s+"\w+\s+\{dir\}/([^\s"]+)')


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


def render_steps(project: str) -> list[tuple[Path, Path]]:
    """(source, output) of every whole-file render step of a biome.project text."""
    return [(DSL / source, (DSL / out).resolve()) for source, out in _RENDER.findall(project)]


def logic_steps(project: str) -> list[tuple[Path, Path]]:
    """(.logic source, generated file) of every target of every `logic generate` step of a biome.project text."""
    return [(DSL / source, (DSL / out).resolve()) for source, targets in _LOGIC.findall(project) for out in _TARGET.findall(targets)]


def test_every_generated_file_was_printed_from_the_current_catalog():
    steps = render_steps((DSL / "biome.project").read_text(encoding="utf-8"))
    assert steps, "dsl/biome.project has no whole-file render step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_every_generated_decision_was_printed_from_its_current_logic():
    steps = logic_steps((DSL / "biome.project").read_text(encoding="utf-8"))
    assert steps, "dsl/biome.project has no logic generate step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_the_logic_steps_of_a_project_name_their_sources_and_every_target():
    project = (
        'GENERATE m logic generate --source {dir}/decisions/m.logic --contract {dir}/decisions/m.contract'
        ' --target "py {dir}/../server/m_logic.py" --target "cs {dir}/../unity/M.g.cs AS A.M LANG 9"\n'
        'GENERATE m-rows logic examples --source {dir}/decisions/m.examples --logic {dir}/decisions/m.logic --out {dir}/decisions/m.json\n'
        'GENERATE t render {dir}/tools.biome pyspec {dir}/../server/t.py\n')
    assert [(source.name, out.name) for source, out in logic_steps(project)] == [("m.logic", "m_logic.py"), ("m.logic", "M.g.cs")]
    assert [(source.name, out.name) for source, out in render_steps(project)] == [("tools.biome", "t.py")]


def test_an_edited_source_or_a_missing_output_is_stale(tmp_path):
    source, out = tmp_path / "tools.biome", tmp_path / "out.py"
    source.write_bytes(b"\xef\xbb\xbfTOOL a\r\nEND\r\n")
    assert source_sha256(source) == hashlib.sha256(b"TOOL a\nEND\n").hexdigest()  # no BOM, LF line ends
    out.write_text(f"# Generated from dsl/tools.biome\n# source-sha256={source_sha256(source)}\n", encoding="utf-8")
    assert stale_outputs([(source, out)]) == []
    source.write_bytes(b"TOOL b\nEND\n")
    assert stale_outputs([(source, out), (source, tmp_path / "missing.py")]) == ["out.py", "missing.py"]
