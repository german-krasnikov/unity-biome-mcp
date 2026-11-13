"""Parity + ratchet guard for ARG adoption (raw JsonHelper.ExtractString/ParseOpt* -> typed
ToolArgs.g.cs accessors).

ToolArgs.g.cs already generates one typed accessor class per dsl/tools.biome tool (171 of them),
but most call sites in unity-plugin/Editor still extract args via raw JsonHelper.ExtractString/
ParseOpt* calls instead of the generated accessor. Three checks:

1. Ratchet: raw calls outside Generated/ and Tests/ must not exceed a baseline captured when this
   guard was added (arg-adoption-2026-11, branch start). Migrating a file's call sites to the
   typed accessor lowers the measured count; the next migration PR lowers the baseline to match.
   The test only blocks the count from growing.
2. Generation completeness: every non-LOCAL ARG declared in dsl/tools.biome already has a
   matching accessor method in ToolArgs.g.cs (by its WIRE name when WIRE renames the wire key,
   else its own name) -- proving the generator side of ARG adoption needs no further work, only
   call-site migration remains.
3. Adoption ratio: informational only (printed, not gated) -- the fraction of scanned files that
   already have zero raw calls left.

Note: the tool->args source for check 2 is dsl/tools.biome itself, not
tools/generated/catalog_params.py. catalog_params.py (PARAM_DESCRIPTIONS) only lists an ARG when
the DSL gives it an explicit trailing schema-description string; most ARG lines have none, so most
tools would have an empty entry there and the check would prove nothing for them.
"""
import re
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

_ROOT = Path(__file__).resolve().parents[2]
_EDITOR = _ROOT / "unity-plugin" / "Editor"
_TOOLS_BIOME = _ROOT / "dsl" / "tools.biome"
_TOOLARGS_GEN = _EDITOR / "Generated" / "ToolArgs.g.cs"

# Ratchet: raw call count outside Generated/ and Tests/ measured on branch arg-adoption-2026-11.
# Lower this when a migration PR reduces the real count; never raise it to make a test pass.
_BASELINE_MAX_RAW_CALLS = 643

_RAW_CALL_RE = re.compile(r"JsonHelper\.(?:ExtractString|ParseOpt\w+)\(")
_BLOCK_START_RE = re.compile(r"^(?:TOOL|PYTOOL|INTERNAL)\s+(\S+)\s")
_ARG_LINE_RE = re.compile(r"^\s*ARG\s+(\S+)\s")
_WIRE_RE = re.compile(r'\bWIRE\s+"([^"]+)"')
_LOCAL_RE = re.compile(r"\bLOCAL\b")
_GEN_CLASS_RE = re.compile(r"internal static class (\w+)\s*\n\{([^}]*)\}", re.MULTILINE)
_GEN_METHOD_RE = re.compile(r"internal static \S+\s+(\w+)\(string args\)")


def _is_scannable_editor_cs(path: Path) -> bool:
    """Production C# under Editor/: excludes generated output and NUnit test directories.

    Checked by exact path-part match so "TestRuns/" (a production feature directory) and
    "TestSupport/" are not excluded -- only a literal "Tests" directory component is.
    """
    parts = path.parts
    return "Generated" not in parts and "Tests" not in parts


def _raw_calls_by_file() -> dict[Path, int]:
    return {
        path: len(_RAW_CALL_RE.findall(path.read_text(encoding="utf-8")))
        for path in sorted(_EDITOR.rglob("*.cs"))
        if _is_scannable_editor_cs(path)
    }


def test_raw_extract_string_calls_do_not_exceed_ratchet_baseline():
    counts = _raw_calls_by_file()
    assert counts, "no .cs files scanned: this test would pass without checking anything"
    total = sum(counts.values())
    assert total <= _BASELINE_MAX_RAW_CALLS, (
        f"raw JsonHelper.ExtractString/ParseOpt* calls grew to {total}, above the ratchet "
        f"baseline of {_BASELINE_MAX_RAW_CALLS}. Use the typed ToolArgs.g.cs accessor for new "
        "call sites instead of adding more raw calls."
    )


def test_adoption_ratio_reported(capsys):
    counts = _raw_calls_by_file()
    total_files = len(counts)
    migrated = sum(1 for n in counts.values() if n == 0)
    ratio = migrated / total_files * 100
    print(
        f"ARG adoption: {migrated}/{total_files} files migrated ({ratio:.1f}%), "
        f"{sum(counts.values())} raw calls remain"
    )
    out = capsys.readouterr().out
    assert "ARG adoption:" in out
    assert total_files > 0  # the scan itself must find something, or the % above is meaningless


def _parse_tool_args(text: str) -> list[tuple[str, list[tuple[str, bool]]]]:
    """[(tool_name, [(accessor_name, is_local), ...]), ...] in dsl/tools.biome file order.

    accessor_name is the ARG's WIRE override when set, else its own name -- matching
    ToolArgs.g.cs's own rule ("identifier = PascalCase of the wire key").
    """
    blocks: list[tuple[str, list[tuple[str, bool]]]] = []
    cur_name: str | None = None
    cur_args: list[tuple[str, bool]] = []
    for line in text.splitlines():
        start = _BLOCK_START_RE.match(line)
        if start:
            if cur_name is not None:
                blocks.append((cur_name, cur_args))
            cur_name, cur_args = start.group(1), []
            continue
        if cur_name is None:
            continue
        if line.strip() == "END":
            blocks.append((cur_name, cur_args))
            cur_name, cur_args = None, []
            continue
        arg = _ARG_LINE_RE.match(line)
        if not arg:
            continue
        no_quotes = re.sub(r'"[^"]*"', "", line)  # DEFAULT_TEXT/description quotes hide LOCAL
        is_local = bool(_LOCAL_RE.search(no_quotes))
        wire = _WIRE_RE.search(line)
        cur_args.append((wire.group(1) if wire else arg.group(1), is_local))
    return blocks


def _parse_generated_accessors(text: str) -> list[tuple[str, list[str]]]:
    return [(cname, _GEN_METHOD_RE.findall(body)) for cname, body in _GEN_CLASS_RE.findall(text)]


def _to_pascal(wire_name: str) -> str:
    return "".join(part[:1].upper() + part[1:] for part in wire_name.split("_") if part)


def test_every_non_local_arg_in_tools_biome_has_a_generated_accessor():
    blocks = _parse_tool_args(_TOOLS_BIOME.read_text(encoding="utf-8"))
    classes = _parse_generated_accessors(_TOOLARGS_GEN.read_text(encoding="utf-8"))
    assert blocks, "no TOOL/PYTOOL/INTERNAL block parsed: this test would pass vacuously"
    assert len(blocks) == len(classes), (
        f"dsl/tools.biome has {len(blocks)} tool blocks but ToolArgs.g.cs has {len(classes)} "
        "generated classes -- catalog and generated output are out of sync, re-run `biome generate`"
    )

    missing: list[str] = []
    for (tool_name, args), (class_name, methods) in zip(blocks, classes, strict=True):
        expected_class = _to_pascal(tool_name) + "Args"
        if expected_class != class_name:
            missing.append(f"{tool_name}: expected class {expected_class}, got {class_name}")
            continue
        method_set = set(methods)
        for wire_name, is_local in args:
            if is_local:
                continue
            expected_method = _to_pascal(wire_name)
            if expected_method not in method_set:
                missing.append(f"{tool_name}.{wire_name}: expected accessor {class_name}.{expected_method}")

    assert not missing, "ARG(s) with no generated accessor:\n" + "\n".join(missing)
