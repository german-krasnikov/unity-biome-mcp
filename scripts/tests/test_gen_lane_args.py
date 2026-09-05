"""C16: scripts/gen_lane_args.py -- lane-driven pytest -m / NUnit filter generator.

`pytest <lane>` must print byte-identically what today's CI hand-types (the
regression oracle C17 replaces those literals against). `nunit <lane>` emits
`--category`/`--assembly` flags valid for A22/A23's `run_unity_tests.py`.
An unknown lane exits non-zero with a message on stderr and prints nothing
to stdout -- never a silent empty expression fed into a shell command.

Runs in the standard scripts/tests lane: no Unity, no network, reads two
tracked JSON files only (Tests/biome-test-lanes.json, Tests/taxonomy-map.json).
"""
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import gen_lane_args  # noqa: E402


def test_pytest_pr_python_core_is_byte_identical_to_ci_python_yml(capsys):
    # Real ci-python.yml:72,81 literal today. NOTE: the plan's C16 prose
    # names "not live and not monkey" (2 exclusions) but the file on disk
    # (and C15's own lane filter: exclude_capabilities=["live","monkey","slow"])
    # excludes 3 -- "slow" was added to the CI literal after that prose was
    # drafted. The generator must match the real file, not the stale prose.
    assert gen_lane_args.main(["pytest", "pr-python-core"]) == 0
    assert capsys.readouterr().out == "not live and not monkey and not slow\n"


def test_pytest_nightly_full_is_byte_identical_to_nightly_yml(capsys):
    # Real nightly.yml:34 literal today -- deliberately does NOT exclude
    # monkey/slow (see Tests/biome-test-lanes.json's own note on this lane).
    assert gen_lane_args.main(["pytest", "nightly-full"]) == 0
    assert capsys.readouterr().out == "not live\n"


def test_pytest_master_conformance_matches_ci_sonar_and_conformance_yml(capsys):
    # Serves as the regression oracle for C17's ci-sonar.yml site (byte-
    # identical to both ci-sonar.yml and the deferred ci-conformance.yml).
    assert gen_lane_args.main(["pytest", "master-conformance"]) == 0
    assert capsys.readouterr().out == "not live and not monkey\n"


def test_unknown_lane_exits_nonzero_and_prints_nothing_to_stdout(capsys):
    exit_code = gen_lane_args.main(["pytest", "totally-bogus-lane"])
    captured = capsys.readouterr()
    assert exit_code != 0
    assert captured.out == ""  # never a silent empty expression on stdout
    assert "totally-bogus-lane" in captured.err


def test_nunit_pr_unity_core_is_empty_because_that_lane_is_unfiltered(capsys):
    # pr-unity-core mirrors unity-tests.yml's unfiltered EditMode run today
    # (exclude_capabilities=[], layers=[]) -- proves the real-file wiring
    # path end to end, even though it has nothing to filter yet.
    assert gen_lane_args.main(["nunit", "pr-unity-core"]) == 0
    assert capsys.readouterr().out == "\n"


def test_build_nunit_flags_maps_exclude_capabilities_to_category_and_skips_null():
    # Synthetic filter/dimensions: no current lane exercises this branch
    # (see above), so this proves the mapping directly against pure
    # functions rather than waiting for a future lane to populate it.
    filter_ = {"exclude_capabilities": ["monkey", "live"], "layers": []}
    dimensions = {
        "monkey": {"csharp_category": "Stress"},
        "live": {"csharp_category": None},  # no C# equivalent -- must be skipped
    }
    flags = gen_lane_args.build_nunit_flags(filter_, dimensions)
    assert flags == ["--category", "!^Stress$"]


def test_build_nunit_flags_maps_layers_to_assembly_flags():
    filter_ = {"exclude_capabilities": [], "layers": ["UnityMCP.Editor.Chat.Tests.View"]}
    flags = gen_lane_args.build_nunit_flags(filter_, {})
    assert flags == ["--assembly", "UnityMCP.Editor.Chat.Tests.View"]
