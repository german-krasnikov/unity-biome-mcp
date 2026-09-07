"""Negative control for the Phase C1 offline adapter compile-contract
(`unity-plugin/Tests~/MutationAdapterContract/`): proves the contract is a
real sentinel, not a tautology that always passes.

Implemented as a Python/`dotnet build` test (not a nested C# NUnit test) so
the temp copy stays a single flat project (adapter-cache is compiled
directly into AdapterContract.Tests.csproj already -- no ProjectReference
hop needed to reproduce the failure) with one bounded, cleaned-up `dotnet
build` call.

The seam's `SourcePatchApplyOutcome` type is renamed in a copy of
`ISourcePatchProvider.cs`; the pinned adapter sources and stubs are copied
UNMODIFIED. The adapter (`BiomeFsrSourcePatchProvider.cs`) references
`SourcePatchApplyOutcome.Applied/.Rejected/.Uncertain` by that exact name,
so a rename-only copy of the seam breaks the adapter build with a
missing-symbol error -- proving the compile contract is a live sentinel,
not a tautology that always passes.

See Plans/MUTATION-REGRESSION-MODULE.md Phase C1 ("Negative control").
"""
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_PATCH_DIR = REPO_ROOT / "unity-plugin" / "Editor" / "SourcePatch"
CONTRACT_DIR = REPO_ROOT / "unity-plugin" / "Tests~" / "MutationAdapterContract"
ADAPTER_CACHE_DIR = CONTRACT_DIR / "adapter-cache"

SEAM_FILES = ("ISourcePatchProvider.cs", "SourcePatchRequest.cs", "SourcePatchProviderSlot.cs")
ADAPTER_FILES = (
    "BiomeAutomaticModeGuardLogic.cs",
    "BiomeBodyOnlyMethodClassifier.cs",
    "BiomeFsrAutomaticModesGuard.cs",
    "BiomeFsrSourcePatchProvider.cs",
    "BiomeSingleFlightGate.cs",
)
STUB_FILES = ("UnityStubs.cs", "FsrStubs.cs", "ImmersiveStubs.cs")

RENAME_FROM = "SourcePatchApplyOutcome"
RENAME_TO = "SourcePatchApplyOutcome_RENAMED"
BUILD_TIMEOUT_SECONDS = 90
_DRIFT_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.6.0" />
  </ItemGroup>
</Project>
"""


def _requires_adapter_cache():
    if not ADAPTER_CACHE_DIR.is_dir() or not all((ADAPTER_CACHE_DIR / f).is_file() for f in ADAPTER_FILES):
        import pytest

        pytest.skip(
            f"{ADAPTER_CACHE_DIR} missing adapter sources -- run "
            "scripts/gauntlet/fetch_adapter_sources.py first"
        )


def test_seam_rename_breaks_adapter_build(tmp_path):
    """Renaming the seam's SourcePatchApplyOutcome type -- the type the
    pinned adapter binds `Apply(...)`'s return value to -- must break the
    build with a compiler error naming the missing/renamed symbol. If this
    assertion ever goes green on an unmodified seam, the sentinel is dead:
    the compile contract would no longer catch real seam drift."""
    _requires_adapter_cache()
    drift_dir = tmp_path / "seam-drift-copy"
    drift_dir.mkdir()

    for filename in SEAM_FILES:
        original = (SOURCE_PATCH_DIR / filename).read_text(encoding="utf-8")
        if filename == "ISourcePatchProvider.cs":
            assert RENAME_FROM in original, f"sentinel target {RENAME_FROM!r} not found in {filename}"
            original = original.replace(RENAME_FROM, RENAME_TO)
        (drift_dir / filename).write_text(original, encoding="utf-8")

    for filename in ADAPTER_FILES:
        shutil.copyfile(ADAPTER_CACHE_DIR / filename, drift_dir / filename)
    for filename in STUB_FILES:
        shutil.copyfile(CONTRACT_DIR / filename, drift_dir / filename)

    (drift_dir / "Seam.Drift.csproj").write_text(_DRIFT_CSPROJ, encoding="utf-8")

    try:
        result = subprocess.run(
            ["dotnet", "build", "Seam.Drift.csproj", "-c", "Release"],
            cwd=drift_dir,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=BUILD_TIMEOUT_SECONDS,
        )
    finally:
        shutil.rmtree(drift_dir, ignore_errors=True)

    output = result.stdout + result.stderr
    assert result.returncode != 0, f"expected the renamed-seam build to fail; it succeeded:\n{output}"
    assert "CS0246" in output or "CS0117" in output, f"expected a missing-symbol compiler error; got:\n{output}"


if __name__ == "__main__":
    sys.exit(subprocess.call([sys.executable, "-m", "pytest", __file__, "-v"]))
