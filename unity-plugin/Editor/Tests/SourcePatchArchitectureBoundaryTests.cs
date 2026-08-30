// P0-30: architecture-denylist guards for the not-yet-built Source Patch
// (FSR-backed body-only mutation) boundary. See
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §3/§6 P0-30.
//
// Nothing guarded here exists yet (no source_patch_write command, no
// provider adapter). These are trip-wires: green today because there is
// nothing to violate. Do not delete a test just because a later task makes
// it "point at nothing" — read the inline P0-xx reference first.
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SourcePatchArchitectureBoundaryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // §3.2: "Python adds at most one internal/direct-only async command
        // source_patch_write ... not MCP-decorated and batch/intent cannot invoke
        // it." Today it must not exist as a registered C# command surface at all.
        //
        // P0-50 WILL make this assertion flip: once source_patch_write is
        // registered, TryGetContract must return true. When that happens, do not
        // delete this test — replace the body with the real contract (registered,
        // AsyncHandler/FileHandler/SpecialDispatch set so IsBatchable is false, per
        // CommandRegistry.IsBatchable's own "unregistered == batchable" default).
        [Test]
        public void CommandRegistry_SourcePatchWriteCommand_NotYetRegistered()
        {
            var exists = CommandRegistry.TryGetContract("source_patch_write",
                out _, out _, out _);
            Assert.IsFalse(exists,
                "source_patch_write must not exist as a public C# command surface " +
                "before P0-50 lands it as an internal/direct-only, non-batchable command.");
        }

        // §3.1: base package references no FSR/Harmony/MonoMod. This checks the
        // LIVE loaded domain (real evidence from the currently open, package-absent
        // Editor), complementing the static asmdef/package.json scan in
        // server/tests/test_source_patch_boundary.py. Mono.Cecil is intentionally
        // excluded — Unity ships its own Cecil-based tooling, and §3.1 explicitly
        // states "Unity-owned Cecil is not rejected."
        [Test]
        public void LoadedAssemblies_DoNotIncludeForbiddenProviderNames()
        {
            var installed = PackageInfo.GetAllRegisteredPackages()
                .Any(p => p.name == "com.handzlikchris.fastscriptreload");
            if (installed)
            {
                Assert.Ignore("provider package installed — installed+OFF is a legal " +
                    "worker state (P0-70/P0-80); this guard protects only the " +
                    "package-absent cell.");
            }

            var offenders = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(n => n.ToLowerInvariant().Contains("fastscriptreload")
                    || n.ToLowerInvariant().Contains("harmony")
                    || n.ToLowerInvariant().Contains("monomod"))
                .ToArray();

            Assert.IsEmpty(offenders,
                $"forbidden provider assembly loaded in a package-absent domain: {string.Join(", ", offenders)}");
        }
    }
}
