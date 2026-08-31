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
        // it." P0-50 landed it — this assertion deliberately flipped from
        // "not registered" to "registered, internal-shaped, not batchable" per
        // the P0-40-era comment this replaces. Do not delete this test.
        [Test]
        public void CommandRegistry_SourcePatchWriteCommand_RegisteredInternalAndNotBatchable()
        {
            var exists = CommandRegistry.TryGetContract("source_patch_write",
                out var required, out _, out var isFreeForm);
            Assert.IsTrue(exists, "source_patch_write must be registered by P0-50.");
            Assert.IsFalse(isFreeForm, "source_patch_write must declare a structured contract.");
            CollectionAssert.Contains(required, "path");
            CollectionAssert.Contains(required, "content");
            Assert.IsTrue(CommandRegistry.HasAsyncHandler("source_patch_write", out _),
                "source_patch_write must be registered via RegisterAsync.");
            Assert.IsFalse(CommandRegistry.IsBatchable("source_patch_write"),
                "source_patch_write must be unreachable from batch (§3.2).");
        }

        // §6 P0-70: "editor mutation_mode" schema/ToolSpec/C# parity — the C#
        // contract must declare "enable" as optional so the Python wrapper's
        // tri-state bool can cross the wire.
        [Test]
        public void CommandRegistry_Editor_ContractDeclaresEnableOptional()
        {
            var exists = CommandRegistry.TryGetContract("editor", out _, out var optional, out var isFreeForm);
            Assert.IsTrue(exists, "editor must be registered.");
            Assert.IsFalse(isFreeForm, "editor must declare a structured contract.");
            CollectionAssert.Contains(optional, "enable");
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
