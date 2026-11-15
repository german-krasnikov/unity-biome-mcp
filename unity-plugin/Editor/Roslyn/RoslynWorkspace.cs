using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnityMCP.Editor.Roslyn
{
    /// <summary>In-process Roslyn compilation workspace for dry-compile diagnostics.
    /// Caches reference list; drops cache on domain reload.</summary>
    [InitializeOnLoad]
    internal static class RoslynWorkspace
    {
        private static Array _cachedRefs;   // MetadataReference[]

        static RoslynWorkspace()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        private static void ClearCache() => _cachedRefs = null;

        /// <summary>Parse + type-check newContent. Returns IEnumerable of Diagnostic (via reflection).</summary>
        public static IEnumerable GetDiagnostics(string fileRelPath, string newContent)
        {
            if (!RoslynLoader.EnsureRoslyn())
                throw new InvalidOperationException("[ROSLYN UNAVAILABLE: DLLs not found]");

            var syntaxTree  = ParseText(newContent);
            var refs        = GetOrBuildRefs();
            var compilation = CreateCompilation(syntaxTree, refs);

            var getDiags = compilation.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "GetDiagnostics" && m.GetParameters().All(p => p.HasDefaultValue))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (getDiags == null)
                throw new InvalidOperationException("Compilation.GetDiagnostics() not found");

            var dp = getDiags.GetParameters();
            var da = new object[dp.Length];
            for (int i = 0; i < dp.Length; i++)
                da[i] = dp[i].DefaultValue;

            return (IEnumerable)getDiags.Invoke(compilation, da);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static object ParseText(string text)
        {
            var syntaxTreeType = RoslynLoader.RoslynCompiler
                .GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
            var parseMethod = syntaxTreeType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "ParseText"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length)
                .First();

            var p    = parseMethod.GetParameters();
            var args = new object[p.Length];
            args[0]  = text;
            for (int i = 1; i < p.Length; i++)
                args[i] = p[i].HasDefaultValue ? p[i].DefaultValue : null;

            return parseMethod.Invoke(null, args);
        }

        private static object CreateCompilation(object syntaxTree, Array refs)
        {
            var compilationType = RoslynLoader.RoslynCompiler
                .GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
            var createMethod = compilationType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Create")
                .OrderByDescending(m => m.GetParameters().Length)
                .First();

            var treesArray = Array.CreateInstance(syntaxTree.GetType(), 1);
            treesArray.SetValue(syntaxTree, 0);

            // OutputKind.DynamicallyLinkedLibrary = 2 — prevents CS5001 "no Main method"
            var outputKindType = RoslynLoader.RoslynCore.GetType("Microsoft.CodeAnalysis.OutputKind");
            var dllKind = Enum.ToObject(outputKindType, 2);
            var optionsType = RoslynLoader.RoslynCompiler
                .GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
            var ctor = optionsType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length).First();
            var ctorP = ctor.GetParameters();
            var ctorArgs = new object[ctorP.Length];
            ctorArgs[0] = dllKind;
            for (int i = 1; i < ctorP.Length; i++)
                ctorArgs[i] = ctorP[i].HasDefaultValue ? ctorP[i].DefaultValue : null;
            var options = ctor.Invoke(ctorArgs);

            var cp   = createMethod.GetParameters();
            var args = new object[cp.Length];
            args[0] = "MCPPreflight";
            if (cp.Length > 1) args[1] = treesArray;
            if (cp.Length > 2) args[2] = refs;
            if (cp.Length > 3) args[3] = options;
            for (int i = 4; i < cp.Length; i++)
                args[i] = cp[i].HasDefaultValue ? cp[i].DefaultValue : null;

            return createMethod.Invoke(null, args);
        }

        private static Array GetOrBuildRefs()
        {
            if (_cachedRefs != null) return _cachedRefs;

            var metaRefType    = RoslynLoader.RoslynCore.GetType("Microsoft.CodeAnalysis.MetadataReference");
            var createFromFile = metaRefType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "CreateFromFile"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length)
                .First();

            var locations = AppDomain.CurrentDomain.GetAssemblies()
                // Reuses CodeExecutor's assembly allowlist — single source of truth
                .Where(CodeExecutor.IsAllowedAssembly)
                .Select(a => { try { return a.Location; } catch { return null; } })
                .Where(loc => !string.IsNullOrEmpty(loc) && File.Exists(loc))
                .Distinct()
                .ToArray();

            var refArray = Array.CreateInstance(metaRefType, locations.Length);
            var fp       = createFromFile.GetParameters();
            for (int i = 0; i < locations.Length; i++)
            {
                var a = new object[fp.Length];
                a[0]  = locations[i];
                for (int j = 1; j < fp.Length; j++)
                    a[j] = fp[j].HasDefaultValue ? fp[j].DefaultValue : null;
                refArray.SetValue(createFromFile.Invoke(null, a), i);
            }

            _cachedRefs = refArray;
            return _cachedRefs;
        }
    }
}
