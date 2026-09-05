"""A16: cache Unity's per-project `Library/{Artifacts,ArtifactDB,
SourceAssetDB,ScriptAssemblies,StateCache}` directories to speed up repeated
EditMode runs, but never `Library/UnityMCP/**` — that subtree is run-specific
MCP-plugin durable state; caching it would leak state across CI runs.

Deviation from the plan's literal "both files" wording, documented not
assumed: ci-conformance.yml's `hosted-disposable-unity` job never runs Unity
against `unity-test-project` directly. `scripts/create_unity_test_worker.py`
`create_worker()` builds a throwaway worker under `$RUNNER_TEMP` by copying
only Assets/Packages/ProjectSettings from the source project — it never
copies `Library` (by design: a disposable worker starts with a clean asset
database every run; see its own `test_create_unity_test_worker.py`, which
only ever asserts a `Library/LastSceneManagerSetup.txt` marker file, never a
copy-from-source). Caching `unity-test-project/Library/**` in that job would
restore into a directory the Editor never reads there — paid download time,
zero speedup. So the cache is added only to unity-tests.yml's `test` job,
which runs Unity directly against `unity-test-project`.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked workflow files only.
"""
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
UNITY_TESTS_WORKFLOW = WORKFLOWS_DIR / "unity-tests.yml"

REQUIRED_LIBRARY_SUBPATHS = (
    "unity-test-project/Library/Artifacts",
    "unity-test-project/Library/ArtifactDB",
    "unity-test-project/Library/SourceAssetDB",
    "unity-test-project/Library/ScriptAssemblies",
    "unity-test-project/Library/StateCache",
)


def _job_steps(workflow_path: Path, job_name: str) -> list[dict]:
    data = yaml.safe_load(workflow_path.read_text(encoding="utf-8"))
    return data["jobs"][job_name]["steps"]


def _cache_steps(steps: list[dict]) -> list[dict]:
    return [s for s in steps if s.get("uses", "").startswith("actions/cache@")]


def test_unity_tests_test_job_has_a_library_cache_step():
    steps = _job_steps(UNITY_TESTS_WORKFLOW, "test")
    cache_steps = _cache_steps(steps)
    library_caches = [
        s for s in cache_steps if "Library" in s.get("with", {}).get("path", "")
    ]
    assert len(library_caches) == 1, (
        f"expected exactly 1 Library cache step in unity-tests.yml jobs.test, "
        f"found {len(library_caches)}"
    )
    path = library_caches[0]["with"]["path"]
    for subpath in REQUIRED_LIBRARY_SUBPATHS:
        assert subpath in path, f"missing {subpath!r} in Library cache path: {path!r}"


def test_unity_library_cache_never_includes_unitymcp_subtree():
    # Global safety net: no cache step's `path`, in any workflow, may cache
    # Library/UnityMCP — run-specific durable state that must never persist
    # across CI runs.
    offenders = []
    for workflow_path in sorted(WORKFLOWS_DIR.glob("*.yml")):
        data = yaml.safe_load(workflow_path.read_text(encoding="utf-8"))
        for job_name, job in (data.get("jobs") or {}).items():
            for step in job.get("steps", []):
                if not step.get("uses", "").startswith("actions/cache@"):
                    continue
                path = step.get("with", {}).get("path", "")
                if "Library/UnityMCP" in path:
                    offenders.append(f"{workflow_path.name}:{job_name}")
    assert not offenders, f"Library/UnityMCP cached in: {offenders}"
