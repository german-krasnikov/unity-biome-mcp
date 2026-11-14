"""Node-free format guard for dsl/biome.project's TOOLCHAIN pin: it must be exactly one
`^TOOLCHAIN ` line, naming exactly one lowercase hex sha of git's sha-1 length. Catches a
malformed re-pin (wrong length, branch name, uppercase, stray whitespace) before anyone
needs BIOME_DSL_ROOT or Node to notice. It does not check that the pinned sha actually
exists in the toolchain checkout -- that is scripts/dsl.sh check's job (needs Node).
"""
import re
from pathlib import Path

DSL = Path(__file__).resolve().parents[3] / "dsl"
TOOLCHAIN_SHA_LENGTH = 40
_TOOLCHAIN_LINE = re.compile(rf"^TOOLCHAIN [0-9a-f]{{{TOOLCHAIN_SHA_LENGTH}}}$")


def _toolchain_lines(text: str) -> list[str]:
    return [line for line in text.splitlines() if line.startswith("TOOLCHAIN ")]


def test_biome_project_has_exactly_one_toolchain_line_naming_one_lowercase_hex_sha():
    text = (DSL / "biome.project").read_text(encoding="utf-8")
    lines = _toolchain_lines(text)
    assert len(lines) == 1, f"expected exactly one '^TOOLCHAIN ' line, found {len(lines)}: {lines}"
    assert _TOOLCHAIN_LINE.fullmatch(lines[0]), f"not one {TOOLCHAIN_SHA_LENGTH}-char lowercase hex sha: {lines[0]!r}"


def test_toolchain_pattern_rejects_malformed_shas():
    bad_lines = [
        "TOOLCHAIN " + "a" * (TOOLCHAIN_SHA_LENGTH - 1),  # too short
        "TOOLCHAIN " + "a" * (TOOLCHAIN_SHA_LENGTH + 1),  # too long
        "TOOLCHAIN " + "A" * TOOLCHAIN_SHA_LENGTH,  # uppercase
        "TOOLCHAIN " + "a" * TOOLCHAIN_SHA_LENGTH + " ",  # trailing whitespace
        "TOOLCHAIN main",  # a branch name, not a sha
    ]
    for line in bad_lines:
        assert _TOOLCHAIN_LINE.fullmatch(line) is None, f"pattern should reject {line!r}"
