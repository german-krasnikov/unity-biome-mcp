using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UnityMCP.Editor
{
    internal sealed class AssemblySourceObservation
    {
        internal string Mismatch = "";
        internal string Limitation = "";
        internal string MismatchedCompilerSource = "";
        internal int CheckedSources;
        internal int ExpectedSources;
        internal bool PdbPaired;
        internal string Token => !string.IsNullOrEmpty(Mismatch) ? "stale" :
            !string.IsNullOrEmpty(Limitation) ? "unknown(" + Limitation + ")" : "fresh";
    }

    // Source freshness only. This does not certify compiler flags, generators or all
    // project inputs. Unknown coverage is diagnostic; an actual checksum mismatch is not.
    internal static class AssemblySourceFreshness
    {
        internal static string SourceAssetPath(string physicalSource, string project,
            IReadOnlyDictionary<string, string> packageRoots)
        {
            if (string.IsNullOrEmpty(physicalSource)) return "";
            var source = Path.GetFullPath(physicalSource);
            var assets = Path.GetFullPath(Path.Combine(project, "Assets")) + Path.DirectorySeparatorChar;
            if (source.StartsWith(assets, StringComparison.Ordinal))
                return "Assets/" + source.Substring(assets.Length).Replace('\\', '/');
            foreach (var package in packageRoots.OrderByDescending(pair => pair.Value.Length))
            {
                var root = Path.GetFullPath(package.Value).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (source.StartsWith(root, StringComparison.Ordinal))
                    return package.Key + "/" + source.Substring(root.Length).Replace('\\', '/');
            }
            return "";
        }

        internal static AssemblySourceObservation Inspect(string output, IEnumerable<string> sources,
            Func<string, string> resolveDocument, string membershipLimitation = "")
        {
            var expected = new HashSet<string>((sources ?? Array.Empty<string>()).Select(Path.GetFullPath), StringComparer.Ordinal);
            var result = new AssemblySourceObservation { ExpectedSources = expected.Count, Limitation = membershipLimitation };
            if (!File.Exists(output)) { result.Limitation = "missing-output"; return result; }
            if (expected.Any(source => !File.Exists(source)))
            {
                result.Mismatch = "compiler source is missing";
                return result;
            }
            if (!File.Exists(Path.ChangeExtension(output, ".pdb")))
            {
                result.Limitation = "missing-pdb";
                return result;
            }
            try
            {
                using var pdb = File.OpenRead(Path.ChangeExtension(output, ".pdb"));
                using var module = ModuleDefinition.ReadModule(output, new ReaderParameters
                {
                    InMemory = true, ReadSymbols = true, ThrowIfSymbolsAreNotMatching = true,
                    SymbolReaderProvider = new PortablePdbReaderProvider(), SymbolStream = pdb
                });
                result.PdbPaired = module.HasSymbols;
                if (!result.PdbPaired) { result.Limitation = "unpaired-pdb"; return result; }
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var checkedSources = new HashSet<string>(StringComparer.Ordinal);
                foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                foreach (var point in method.DebugInformation.SequencePoints)
                {
                    var document = point.Document;
                    if (document == null || !visited.Add(document.Url)) continue;
                    string path;
                    try { path = Path.GetFullPath(resolveDocument(document.Url)); }
                    catch { continue; } // Generated/unresolvable documents do not prove a physical source.
                    if (!File.Exists(path)) continue;
                    using HashAlgorithm hash = document.HashAlgorithm == DocumentHashAlgorithm.SHA1 ? SHA1.Create() :
                        document.HashAlgorithm == DocumentHashAlgorithm.SHA256 ? SHA256.Create() : null;
                    if (hash == null) continue;
                    using var stream = File.OpenRead(path);
                    if (!hash.ComputeHash(stream).SequenceEqual(document.Hash))
                    {
                        result.Mismatch = "PDB source checksum differs: " + document.Url;
                        if (expected.Contains(path)) result.MismatchedCompilerSource = path;
                        return result;
                    }
                    if (expected.Contains(path)) checkedSources.Add(path);
                }
                result.CheckedSources = checkedSources.Count;
                if (result.ExpectedSources == 0 || result.CheckedSources != result.ExpectedSources)
                    result.Limitation = "partial-source-coverage";
            }
            catch (SymbolsNotMatchingException error) { result.Mismatch = "DLL/PDB mismatch: " + error.Message; }
            catch (Exception) { result.Limitation = "unreadable-pdb"; }
            return result;
        }
    }
}
