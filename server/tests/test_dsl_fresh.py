"""Tier 0 of the freshness of what dsl/ generates, without Node: every generated file carries the sha256 of the source it was printed from.

It catches "the catalog (or a decision, or the chat turn) was edited and scripts/dsl.sh generate was not run". The banner states the digest of the source and nothing
of the body, so a hand edit of a generated file that keeps its banner, a change of the construct, a contract or the examples, and the machine rows of the examples
(dsl/decisions/*.json, which have no banner) are not seen here: test_dsl_content_lock.py is the tier 0 that sees them (dsl/content-lock.json, which scripts/dsl.sh
generate refreshes). That the locked files are what the compiler prints is what tier 1 finds: scripts/dsl.sh check (generate --check, needs Node and the biome-dsl
checkout of the TOOLCHAIN line).
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
# GENERATE <name> feature generate --feature {dir}/<f>.feature ... --out {dir}/<dir> [--tests-out {dir}/<dir>] ...: the files of both directories carry the digest of the feature
_FEATURE = re.compile(r"^GENERATE\s+\S+\s+feature generate\s+--feature\s+\{dir\}/(\S+)(.*)$", re.MULTILINE)
_FEATURE_DIR = re.compile(r"--(?:out|tests-out)\s+\{dir\}/(\S+)")


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


def feature_steps(project: str, dsl: Path = DSL) -> list[tuple[Path, Path]]:
    """(.feature source, generated file) of every file that the .biome-files manifest of the --out and --tests-out directory of a `feature generate` step owns."""
    steps = []
    for source, flags in _FEATURE.findall(project):
        for directory in _FEATURE_DIR.findall(flags):
            manifest = (dsl / directory / ".biome-files").resolve()
            names = [line.split(" ", 1)[1] for line in manifest.read_text(encoding="utf-8").splitlines()] if manifest.is_file() else [manifest.name]
            steps += [(dsl / source, manifest.with_name(name)) for name in names]
    return steps


def test_every_generated_file_was_printed_from_the_current_catalog():
    steps = render_steps((DSL / "biome.project").read_text(encoding="utf-8"))
    assert steps, "dsl/biome.project has no whole-file render step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_every_generated_decision_was_printed_from_its_current_logic():
    steps = logic_steps((DSL / "biome.project").read_text(encoding="utf-8"))
    assert steps, "dsl/biome.project has no logic generate step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_every_generated_chat_turn_file_was_printed_from_its_current_feature():
    steps = feature_steps((DSL / "biome.project").read_text(encoding="utf-8"))
    assert steps, "dsl/biome.project has no feature generate step"
    assert stale_outputs(steps) == [], "run scripts/dsl.sh generate"


def test_a_feature_step_owns_the_files_of_its_manifest_and_a_missing_one_is_stale(tmp_path):
    (tmp_path / "out").mkdir()
    feature = tmp_path / "a.feature"
    feature.write_text("FEATURE a\nEND\n", encoding="utf-8")
    (tmp_path / "out" / ".biome-files").write_text("0 A.cs\n0 B.cs\n", encoding="utf-8")
    (tmp_path / "out" / "A.cs").write_text(f"// Generated\n// source-sha256={source_sha256(feature)}\n", encoding="utf-8")
    project = "GENERATE a feature generate --feature {dir}/a.feature --uses elm --lang cs --flat --out {dir}/out\n"
    steps = feature_steps(project, tmp_path)
    assert [out.name for _, out in steps] == ["A.cs", "B.cs"]
    assert stale_outputs(steps) == ["B.cs"]


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
