"""P1-20 six-cell matrix: CI qualification harness fixture.

Generates/installs the P0-80 retained-object mutation target into a
*disposable worker only* — never `unity-test-project/Assets/`. See
Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §6 P0-80
and §7 P1-20.

`FastReloadTarget.cs` (a plain POCO, not MonoBehaviour-derived — a
MonoBehaviour-derived mutation target has no on-disk script path for FSR's
dynamic-assembly patch type to resolve against, which trips a native
`gpath.c` assertion in the underlying mono runtime) is the only file the
product ever writes to; its body is regenerated per phase by `target_body`.
`SourcePatchHarnessHolder.cs` (retained MonoBehaviour) and
`Editor/CycleInstrumentation.cs` (evidence writer) are tracked, byte-frozen
fixture source under `scripts/fixtures/fsr_qualification/` and are installed
verbatim, never regenerated — this is what
`test_install_fixture_holder_and_instrumentation_match_tracked_source`
guards against drifting.
"""
import uuid
from pathlib import Path

FIXTURE_DIR = Path(__file__).resolve().parents[1] / "fixtures" / "fsr_qualification"

REL_TARGET = "Assets/SourcePatchHarness/FastReloadTarget.cs"
REL_HOLDER = "Assets/SourcePatchHarness/SourcePatchHarnessHolder.cs"
REL_INSTR = "Assets/SourcePatchHarness/Editor/CycleInstrumentation.cs"


class FsrQualificationFixtureError(RuntimeError):
    pass


def new_guid() -> str:
    return uuid.uuid4().hex


def mono_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "MonoImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def target_body(kind: str) -> str:
    """kind: 'v0'..'v4' (return N;) or 'invalid' (closure — non body-only,
    out of the hard supported scope per §1.2).

    Plain POCO (sealed class) — matches the proven local W2/P0-80 shape.
    """
    if kind == "invalid":
        stmt = "System.Func<int> f = () => 3; return f();"
    else:
        n = kind[1:]  # 'v2' -> '2'
        stmt = f"return {n};"
    return (
        "using System.Runtime.CompilerServices;\n\n"
        "namespace UnityMCP.Worker.SourcePatchHarness\n"
        "{\n"
        "    // P1-20 CI qualification target — body-only mutated across the\n"
        "    // cell scenario. Do not add fields/methods/attributes; only\n"
        "    // Compute()'s body changes. See SourcePatchHarnessHolder.cs\n"
        "    // (never mutated) for the retained Unity Editor object this\n"
        "    // class is instantiated from.\n"
        "    public sealed class FastReloadTarget\n"
        "    {\n"
        "        [MethodImpl(MethodImplOptions.NoInlining)]\n"
        f"        public int Compute() {{ {stmt} }}\n"
        "    }\n"
        "}\n"
    )


def install_fixture(worker: Path) -> None:
    """Write the three harness files + fresh .meta into a disposable worker.

    Target starts at 'v0'. Holder/Instrumentation are copied byte-identical
    from the tracked fixture source. Never touches a non-disposable project."""
    target = worker / REL_TARGET
    holder = worker / REL_HOLDER
    instrumentation = worker / REL_INSTR
    for path in (target, holder, instrumentation):
        path.parent.mkdir(parents=True, exist_ok=True)

    target.write_text(target_body("v0"), encoding="utf-8")
    (worker / (REL_TARGET + ".meta")).write_text(mono_meta(new_guid()), encoding="utf-8")

    holder.write_bytes((FIXTURE_DIR / "SourcePatchHarnessHolder.cs").read_bytes())
    (worker / (REL_HOLDER + ".meta")).write_text(mono_meta(new_guid()), encoding="utf-8")

    instrumentation.write_bytes((FIXTURE_DIR / "Editor" / "CycleInstrumentation.cs").read_bytes())
    (worker / (REL_INSTR + ".meta")).write_text(mono_meta(new_guid()), encoding="utf-8")


def validate_installed_fixture(worker: Path) -> None:
    """Byte-check Holder/Instrumentation against tracked source; presence-
    check the target (its body legitimately varies across cell phases)."""
    target = worker / REL_TARGET
    holder = worker / REL_HOLDER
    instrumentation = worker / REL_INSTR
    if not target.is_file():
        raise FsrQualificationFixtureError(f"Fixture target is missing: {target}")

    holder_tracked = (FIXTURE_DIR / "SourcePatchHarnessHolder.cs").read_bytes()
    if not holder.is_file() or holder.read_bytes() != holder_tracked:
        raise FsrQualificationFixtureError(
            f"Installed holder is absent or changed: {holder}"
        )

    instrumentation_tracked = (FIXTURE_DIR / "Editor" / "CycleInstrumentation.cs").read_bytes()
    if not instrumentation.is_file() or instrumentation.read_bytes() != instrumentation_tracked:
        raise FsrQualificationFixtureError(
            f"Installed instrumentation is absent or changed: {instrumentation}"
        )


__all__ = [
    "FIXTURE_DIR",
    "REL_TARGET",
    "REL_HOLDER",
    "REL_INSTR",
    "FsrQualificationFixtureError",
    "new_guid",
    "mono_meta",
    "target_body",
    "install_fixture",
    "validate_installed_fixture",
]
