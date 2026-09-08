// Minimal compile-contract stubs of the FastScriptReload surface the
// adapter (adapter-cache/*.cs, Phase C1) binds against. Signatures are
// reverse-engineered from actual call sites in
// adapter-cache/BiomeFsrSourcePatchProvider.cs and
// adapter-cache/BiomeFsrAutomaticModesGuard.cs -- not from the FSR source
// itself (not vendored here). Each seam behaviour is switchable per test
// via a static hook/field so tests drive Apply() through every outcome
// (Applied/Rejected/Uncertain) without touching production adapter code.
// See Plans/MUTATION-REGRESSION-MODULE.md Phase C1.
using System;
using System.Collections.Generic;
using System.Reflection;
using ImmersiveVRTools.Runtime.Common;

namespace FastScriptReload.Editor.Compilation
{
    public sealed class CompileResult
    {
        public bool IsError { get; set; }
        public Assembly CompiledAssembly { get; set; }
    }

    public static class DynamicAssemblyCompiler
    {
        // Test-driven: each test assigns the hook it needs, then resets it
        // to null in teardown (see AdapterContractFixtureBase).
        public static Func<List<string>, UnityMainThreadDispatcher, CompileResult> CompileHook;

        public static CompileResult Compile(List<string> filesToRecompile, UnityMainThreadDispatcher dispatcher)
        {
            if (CompileHook == null)
            {
                throw new InvalidOperationException(
                    "DynamicAssemblyCompiler.CompileHook not set by test -- every test that reaches " +
                    "compilation must assign it.");
            }

            return CompileHook(filesToRecompile, dispatcher);
        }
    }

    public static class ProjectTypeCache
    {
        public static Dictionary<string, Type> AllTypesInNonDynamicGeneratedAssemblies { get; } = new();
    }
}

namespace FastScriptReload.Runtime
{
    public sealed class ExactTargetPatchResult
    {
        public bool Applied { get; set; }
    }

    public sealed class AssemblyChangesLoader
    {
        public static readonly AssemblyChangesLoader Instance = new();

        public Func<Assembly, MethodInfo, ExactTargetPatchResult> DetourHook;

        public ExactTargetPatchResult DynamicallyUpdateSingleMethodForCreatedAssembly(
            Assembly compiledAssembly, MethodInfo existingMethod)
        {
            if (DetourHook == null)
            {
                throw new InvalidOperationException(
                    "AssemblyChangesLoader.DetourHook not set by test -- every test that reaches " +
                    "the detour step must assign it.");
            }

            return DetourHook(compiledAssembly, existingMethod);
        }
    }
}

namespace FastScriptReload.Editor
{
    // Six preference switches BiomeFsrAutomaticModesGuard forces on load.
    // Not exercised by the AdapterContract test matrix (only
    // BiomeFsrSourcePatchProvider.Apply is under test); these fields exist
    // solely so BiomeFsrAutomaticModesGuard.cs compiles alongside it.
    public static class FastScriptReloadPreference
    {
        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition EnableAutoReloadForChangedFiles = new();

        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition EnableOnDemandReload = new();

        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition WatchOnlySpecified = new();

        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition EnableExperimentalEditorHotReloadSupport = new();

        [Obsolete("Matches the real FSR field's Obsolete marker referenced under #pragma warning disable 0618.")]
        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition EnableCustomFileWatcher = new();

        public static readonly ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
            .ToggleProjectEditorPreferenceDefinition StopShowingAutoReloadEnabledDialogBox = new();
    }
}
