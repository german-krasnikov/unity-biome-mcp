"""P1-20 six-cell matrix: CI qualification harness fixture.

Promoted from the local P0-80 evidence generator
(`/private/tmp/biome-p080/harness_files.py`, proven across two local
final-SHA product cycles — see
Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §6 P0-80).

`target_body`/`mono_meta`/`new_guid` are pure string templates — no I/O.
`install_fixture`/`validate_installed_fixture` write into a *disposable
worker* only, mirroring `run_unity_domain_reload_acceptance.py`'s
`install_worker_fixture`/`validate_installed_fixture` — never the tracked
`unity-test-project/Assets/`.

Runs in the standard `scripts/tests` lane: no Unity, no network, hermetic
tmp_path only.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import gauntlet.fsr_qualification_fixture as fixture

# ---------------------------------------------------------------------------
# target_body — the only file the product ever writes to during a cell
# ---------------------------------------------------------------------------

@pytest.mark.parametrize(
    "kind,expected_return",
    [("v0", "return 0;"), ("v1", "return 1;"), ("v2", "return 2;"), ("v3", "return 3;")],
)
def test_target_body_numeric_kind_returns_matching_literal(kind: str, expected_return: str):
    body = fixture.target_body(kind)
    assert expected_return in body
    assert "public sealed class FastReloadTarget" in body
    assert "public int Compute()" in body


def test_target_body_invalid_kind_uses_non_body_only_closure():
    """'invalid' must be an out-of-scope shape (a closure), not just a
    different number — this cell asserts a rejected mutation, not merely a
    different accepted one."""
    body = fixture.target_body("invalid")
    assert "System.Func<int>" in body
    assert "=>" in body


def test_target_body_is_single_method_no_new_members():
    """Non-goal guard (§1.2): body-only, one method, no new fields/types."""
    body = fixture.target_body("v2")
    assert body.count("public int Compute()") == 1
    assert body.count("public sealed class FastReloadTarget") == 1
    assert not any(keyword in body for keyword in ("interface ", "struct ", "enum "))


def test_mono_meta_embeds_exact_guid():
    text = fixture.mono_meta("0123456789abcdef0123456789abcdef")
    assert "guid: 0123456789abcdef0123456789abcdef" in text
    assert "MonoImporter:" in text


def test_new_guid_is_unique_and_hex():
    first = fixture.new_guid()
    second = fixture.new_guid()
    assert first != second
    assert len(first) == 32
    int(first, 16)  # raises if not hex


# ---------------------------------------------------------------------------
# install_fixture / validate_installed_fixture — disposable-worker only
# ---------------------------------------------------------------------------

def _worker(tmp_path: Path) -> Path:
    worker = tmp_path / "worker"
    (worker / "Assets").mkdir(parents=True)
    return worker


def test_install_fixture_writes_target_holder_and_instrumentation(tmp_path: Path):
    worker = _worker(tmp_path)

    fixture.install_fixture(worker)

    target = worker / fixture.REL_TARGET
    holder = worker / fixture.REL_HOLDER
    instrumentation = worker / fixture.REL_INSTR
    assert target.is_file()
    assert holder.is_file()
    assert instrumentation.is_file()
    assert "return 0;" in target.read_text(encoding="utf-8")
    assert "SourcePatchHarnessHolder" in holder.read_text(encoding="utf-8")
    assert "CycleInstrumentation" in instrumentation.read_text(encoding="utf-8")


def test_install_fixture_writes_one_meta_per_file_with_distinct_guids(tmp_path: Path):
    worker = _worker(tmp_path)

    fixture.install_fixture(worker)

    metas = [
        (worker / (fixture.REL_TARGET + ".meta")).read_text(encoding="utf-8"),
        (worker / (fixture.REL_HOLDER + ".meta")).read_text(encoding="utf-8"),
        (worker / (fixture.REL_INSTR + ".meta")).read_text(encoding="utf-8"),
    ]
    guids = [line.split("guid: ")[1].strip() for text in metas for line in text.splitlines() if line.startswith("guid: ")]
    assert len(guids) == 3
    assert len(set(guids)) == 3


def test_install_fixture_holder_and_instrumentation_match_tracked_source(tmp_path: Path):
    """Holder/Instrumentation are never mutated by the product — the
    installed copy must be byte-identical to the tracked, reviewed fixture
    source, never the pure-Python template re-derived independently (that
    would let the tracked .cs and the installer silently drift)."""
    worker = _worker(tmp_path)

    fixture.install_fixture(worker)

    holder_tracked = (fixture.FIXTURE_DIR / "SourcePatchHarnessHolder.cs").read_bytes()
    instrumentation_tracked = (fixture.FIXTURE_DIR / "Editor" / "CycleInstrumentation.cs").read_bytes()
    assert (worker / fixture.REL_HOLDER).read_bytes() == holder_tracked
    assert (worker / fixture.REL_INSTR).read_bytes() == instrumentation_tracked


def test_validate_installed_fixture_passes_after_install(tmp_path: Path):
    worker = _worker(tmp_path)
    fixture.install_fixture(worker)

    fixture.validate_installed_fixture(worker)  # must not raise


def test_validate_installed_fixture_raises_when_holder_tampered(tmp_path: Path):
    worker = _worker(tmp_path)
    fixture.install_fixture(worker)
    (worker / fixture.REL_HOLDER).write_text("tampered", encoding="utf-8")

    with pytest.raises(fixture.FsrQualificationFixtureError):
        fixture.validate_installed_fixture(worker)


def test_validate_installed_fixture_raises_when_target_missing(tmp_path: Path):
    worker = _worker(tmp_path)
    fixture.install_fixture(worker)
    (worker / fixture.REL_TARGET).unlink()

    with pytest.raises(fixture.FsrQualificationFixtureError):
        fixture.validate_installed_fixture(worker)
