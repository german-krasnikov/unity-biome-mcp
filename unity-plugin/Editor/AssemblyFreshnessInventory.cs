using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor
{
    // Compiler inventory supplies assembly ownership; physical package paths come from
    // PackageInfo through the existing resolver. Raw membership is only a negative check.
    internal sealed class AssemblyFreshnessInventory
    {
        private readonly string _project;
        private readonly Dictionary<string, string> _packageRoots;
        private readonly HashSet<string> _knownSources = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownDefinitions = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _membership = new Dictionary<string, string>(StringComparer.Ordinal);

        internal AssemblyFreshnessInventory(string project, IEnumerable<Assembly> assemblies)
        {
            _project = project;
            _packageRoots = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .ToDictionary(package => "Packages/" + package.name, package => package.resolvedPath, StringComparer.Ordinal);
            foreach (var assembly in assemblies)
            {
                foreach (var source in assembly.sourceFiles ?? Array.Empty<string>()) _knownSources.Add(Resolve(source));
                var definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!string.IsNullOrEmpty(definition)) _knownDefinitions.Add(Resolve(definition));
            }
        }

        internal string Resolve(string path)
        {
            if (!_paths.TryGetValue(path, out var physical))
                _paths[path] = physical = TestRunAssemblyFingerprint.ResolveSourcePath(_project, path,
                    package => _packageRoots.TryGetValue(package, out var root) ? root : null);
            return physical;
        }

        internal string[] Sources(Assembly assembly) => (assembly.sourceFiles ?? Array.Empty<string>()).Select(Resolve).ToArray();

        internal string MembershipLimitation(Assembly assembly)
        {
            var definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
            var root = string.IsNullOrEmpty(definition) ? Path.Combine(_project, "Assets") : Path.GetDirectoryName(Resolve(definition));
            if (_membership.TryGetValue(root, out var result)) return result;
            try { result = FindUndiscovered(root, _knownSources, _knownDefinitions); }
            catch { result = "unreadable-source-membership"; }
            return _membership[root] = result;
        }

        internal static string[] FindStaleSourceAssets()
        {
            try
            {
                var project = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                var inventory = new AssemblyFreshnessInventory(project, assemblies);
                var assets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var assembly in assemblies)
                {
                    var ownScope = assembly.name.StartsWith("UnityMCP.", StringComparison.Ordinal) ||
                        (assembly.sourceFiles ?? Array.Empty<string>()).Any(path => path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal));
                    if (!ownScope) continue;
                    // Unknown coverage or a loaded-only mismatch cannot select an import.
                    // Inspect exposes this path only for a paired checksum mismatch
                    // whose physical document is a member of this compiler inventory.
                    var output = Path.GetFullPath(Path.IsPathRooted(assembly.outputPath) ? assembly.outputPath : Path.Combine(project, assembly.outputPath));
                    var evidence = AssemblySourceFreshness.Inspect(output, inventory.Sources(assembly), inventory.Resolve);
                    var asset = AssemblySourceFreshness.SourceAssetPath(evidence.MismatchedCompilerSource, project, inventory._packageRoots);
                    if (!string.IsNullOrEmpty(asset)) assets.Add(asset);
                }
                return assets.ToArray();
            }
            catch { return Array.Empty<string>(); } // Discovery uncertainty authorizes no targeted import.
        }

        internal static string FindUndiscovered(string root, ISet<string> knownSources, ISet<string> knownDefinitions)
        {
            foreach (var file in Directory.EnumerateFiles(root))
            {
                var extension = Path.GetExtension(file);
                if (extension == ".cs" && !knownSources.Contains(Path.GetFullPath(file))) return "undiscovered-source";
                if (extension == ".asmdef" && !knownDefinitions.Contains(Path.GetFullPath(file))) return "undiscovered-asmdef";
                // An asmref is not represented by PDB source checksums. Do not claim its
                // current binding is proven from a possibly stale compiler inventory.
                if (extension == ".asmref") return "asmref-coverage";
            }
            foreach (var child in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(child);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal)) continue;
                var result = FindUndiscovered(child, knownSources, knownDefinitions);
                if (!string.IsNullOrEmpty(result)) return result;
            }
            return "";
        }
    }
}
