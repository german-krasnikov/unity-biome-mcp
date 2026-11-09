"""One-off migration: insert GUARD/WIRE flag lines into dsl/tools.biome from the golden registration census
(server/tests/golden/registration.txt, A0), so the generated unity-plugin/Editor/Generated/ToolMeta.g.cs can state
the six flags a tools.biome block never carried before A1 (Plans/DSL/A1-DESIGN.md Sec 7).

For every TOOL/INTERNAL block in tools.biome whose name is also in golden, a GUARD/WIRE line is inserted directly
after that block's HEAD line for each golden field that is "1": always/compile/playok -> GUARD, special/notbatch/
noundo -> WIRE. Every other line of tools.biome is left byte-for-byte untouched -- this is a migration script, run
once and its output committed, not wired into dsl/biome.project.

Usage (from the repository root):
  python scripts/populate_registration_flags.py
"""
import re
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SERVER_TESTS = _REPO_ROOT / "server" / "tests"
for _extra in (_SERVER_TESTS, _REPO_ROOT / "server" / "src", _REPO_ROOT / "scripts"):
    if str(_extra) not in sys.path:
        sys.path.insert(0, str(_extra))

from test_registration_golden import _parse  # noqa: E402  (path set up above)

_GOLDEN = _SERVER_TESTS / "golden" / "registration.txt"
_TOOLS_BIOME = _REPO_ROOT / "dsl" / "tools.biome"
_HEAD = re.compile(r"^(TOOL|INTERNAL) (\S+) ")

# golden field -> the GUARD/WIRE line to insert, in the fixed order A1-DESIGN.md Sec 7 asks for (GUARDs before WIREs).
_FLAG_MAP = [("always", "GUARD always"), ("compile", "GUARD compile"), ("playok", "GUARD play_ok"),
             ("special", "WIRE special"), ("notbatch", "WIRE not_batch"), ("noundo", "WIRE no_undo")]


def _golden() -> dict[str, dict[str, str]]:
    lines = [line for line in _GOLDEN.read_text(encoding="utf-8").splitlines() if line and not line.startswith("#")]
    return dict(_parse(line) for line in lines)


def main() -> None:
    golden = _golden()
    out: list[str] = []
    covered = inserted = 0
    for line in _TOOLS_BIOME.read_text(encoding="utf-8").splitlines(keepends=True):
        out.append(line)
        match = _HEAD.match(line)
        if not match:
            continue
        fields = golden.get(match.group(2))
        if fields is None:
            continue
        covered += 1
        flags = [f"  {flag}\n" for golden_field, flag in _FLAG_MAP if fields.get(golden_field) == "1"]
        out.extend(flags)
        inserted += len(flags)
    _TOOLS_BIOME.write_text("".join(out), encoding="utf-8")
    divergent = 31  # A1-DESIGN.md Sec 4: pre-existing ARG-shape drift, not touched by this script
    print(f"{covered}/{len(golden)} covered, {inserted} flag lines inserted, {covered - divergent} clean, "
          f"{divergent} known-divergent, {len(golden) - covered} not yet in catalog")


if __name__ == "__main__":
    main()
