using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.TestRuns
{
    internal sealed class TestRunAssemblyFingerprintResult
    {
        internal string Fingerprint = "";
        internal string MainAssemblyPath = "";
        internal string LatestMainSourcePath = "";
        internal string MainAssemblyWriteUtc = "";
        internal string LatestMainSourceWriteUtc = "";
        internal int AssemblyCount;
        internal string Error = "";
        internal bool IsCoherent => string.IsNullOrEmpty(Error);
    }

    /// <summary>
    /// Fingerprints every UnityMCP product assembly plus UTF's currently loaded
    /// EditMode test assemblies and their compiled dependency closure. Loaded
    /// outputs are MVID-correlated and every Unity-reported source is hashed.
    /// </summary>
    internal static class TestRunAssemblyFingerprint
    {
        private const string MainAssemblyName = "UnityMCP.Editor";
        private static readonly HashSet<string> TestFrameworkAssemblies =
            new HashSet<string>(StringComparer.Ordinal)
        {
            "nunit.framework",
            "UnityEngine.TestRunner",
            "Unity.PerformanceTesting"
        };

        internal static TestRunAssemblyFingerprintResult Capture(string projectRoot)
        {
            var result = new TestRunAssemblyFingerprintResult();
            try
            {
                var project = Path.GetFullPath(projectRoot ?? "");
                var allCompiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                    .OrderBy(assembly => assembly.name, StringComparer.Ordinal)
                    .ToArray();
                var loadedByName = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => new
                    {
                        Assembly = assembly,
                        Name = assembly.GetName().Name
                    })
                    .Where(item => !string.IsNullOrEmpty(item.Name))
                    .GroupBy(item => item.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key,
                        group => group.Select(item => item.Assembly).ToArray(),
                        StringComparer.Ordinal);
                var compiledByName = allCompiled.ToDictionary(
                    assembly => assembly.name, StringComparer.Ordinal);
                var dependencies = allCompiled.ToDictionary(
                    assembly => assembly.name,
                    assembly => (assembly.assemblyReferences ?? Array.Empty<Assembly>())
                        .Select(reference => reference.name).ToArray(),
                    StringComparer.Ordinal);
                var testReferenceCache =
                    new Dictionary<System.Reflection.Assembly, bool>();
                var testReferenced = new HashSet<string>(
                    loadedByName.Where(pair => pair.Value.Any(assembly =>
                            ReferencesTestFramework(
                                assembly,
                                loadedByName,
                                testReferenceCache,
                                new HashSet<System.Reflection.Assembly>())))
                        .Select(pair => pair.Key),
                    StringComparer.Ordinal);
                var loadedCompilerOutputs = new HashSet<string>(
                    allCompiled.Where(assembly => HasLoadedCompilerOutput(
                            assembly, project, loadedByName))
                        .Select(assembly => assembly.name),
                    StringComparer.Ordinal);
                var selectedNames = SelectInventoryNames(
                    compiledByName.Keys,
                    loadedCompilerOutputs,
                    testReferenced,
                    dependencies);
                var compiled = selectedNames
                    .Select(name => compiledByName[name])
                    .OrderBy(assembly => assembly.name, StringComparer.Ordinal)
                    .ToArray();
                if (!compiledByName.ContainsKey(MainAssemblyName))
                    throw new InvalidDataException(
                        "UnityMCP.Editor compiler output is absent");
                var descriptors = new List<string>(compiled.Length);

                foreach (var assembly in compiled)
                {
                    var outputPath = ResolveOutputPath(project, assembly.outputPath);
                    if (!File.Exists(outputPath))
                        throw new FileNotFoundException(
                            $"compiled assembly '{assembly.name}' is missing", outputPath);
                    loadedByName.TryGetValue(assembly.name, out var candidates);
                    var loaded = (candidates ?? Array.Empty<System.Reflection.Assembly>())
                        .FirstOrDefault(candidate => PathsEqual(
                            SafeAssemblyLocation(candidate), outputPath));
                    if (loaded == null && candidates != null && candidates.Length > 0)
                        throw new InvalidDataException(
                            $"loaded assembly '{assembly.name}' does not match compiler output " +
                            outputPath);

                    var diskMvid = ReadModuleVersionId(outputPath);
                    if (loaded != null && loaded.ManifestModule.ModuleVersionId != diskMvid)
                        throw new InvalidDataException(
                            $"loaded assembly '{assembly.name}' MVID " +
                            $"{loaded.ManifestModule.ModuleVersionId:N} " +
                            $"does not match on-disk MVID {diskMvid:N}");

                    var sources = (assembly.sourceFiles ?? Array.Empty<string>())
                        .Select(path => ResolveSourcePath(project, path))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();
                    if (sources.Length == 0)
                        throw new InvalidDataException(
                            $"compiled assembly '{assembly.name}' has no source inventory");

                    var assemblyWrite = File.GetLastWriteTimeUtc(outputPath);
                    var latestSource = "";
                    var latestSourceWrite = DateTime.MinValue;
                    var sourceDescriptors = new StringBuilder();
                    foreach (var source in sources)
                    {
                        if (!File.Exists(source))
                            throw new FileNotFoundException(
                                $"source for '{assembly.name}' is missing", source);
                        var sourceWrite = File.GetLastWriteTimeUtc(source);
                        if (sourceWrite > latestSourceWrite)
                        {
                            latestSource = source;
                            latestSourceWrite = sourceWrite;
                        }
                        sourceDescriptors.Append(NormalizePath(source)).Append('=')
                            .Append(HashFile(source)).Append('\n');
                    }
                    if (assemblyWrite < latestSourceWrite)
                        throw new InvalidDataException(
                            $"compiled assembly '{assembly.name}' is older than source " +
                            latestSource);

                    var sourceHash = HashText(sourceDescriptors.ToString());
                    descriptors.Add(
                        assembly.name + "|mvid=" + diskMvid.ToString("N") +
                        "|dll=" + HashFile(outputPath) +
                        "|sources=" + sourceHash +
                        "|source_count=" + sources.Length);

                    if (!string.Equals(assembly.name, MainAssemblyName,
                            StringComparison.Ordinal))
                        continue;
                    result.MainAssemblyPath = outputPath;
                    result.LatestMainSourcePath = latestSource;
                    result.MainAssemblyWriteUtc = assemblyWrite.ToString("O");
                    result.LatestMainSourceWriteUtc = latestSourceWrite.ToString("O");
                }

                if (string.IsNullOrEmpty(result.MainAssemblyPath))
                    throw new InvalidDataException(
                        "UnityMCP.Editor was not selected for build fingerprinting");

                result.AssemblyCount = descriptors.Count;
                result.Fingerprint = "sha256=" +
                    HashText(string.Join("\n", descriptors)) +
                    ";assemblies=" + descriptors.Count;
            }
            catch (Exception error)
            {
                result.Error = "assembly fingerprint failed: " + error.Message;
            }
            return result;
        }

        internal static Guid ReadModuleVersionId(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (ReadUInt16(reader, 0) != 0x5A4D)
                throw new InvalidDataException("assembly has no DOS header");
            var peOffset = ReadInt32(reader, 0x3C);
            if (ReadUInt32(reader, peOffset) != 0x00004550)
                throw new InvalidDataException("assembly has no PE header");

            var coff = peOffset + 4;
            var sectionCount = ReadUInt16(reader, coff + 2);
            var optionalSize = ReadUInt16(reader, coff + 16);
            var optional = coff + 20;
            var magic = ReadUInt16(reader, optional);
            var dataDirectories = magic == 0x20B
                ? optional + 112
                : magic == 0x10B
                    ? optional + 96
                    : throw new InvalidDataException("assembly has an unknown PE format");
            var cliRva = ReadUInt32(reader, dataDirectories + 14 * 8);
            var sections = optional + optionalSize;
            var cliOffset = RvaToOffset(reader, cliRva, sections, sectionCount);
            var metadataRva = ReadUInt32(reader, cliOffset + 8);
            var metadata = RvaToOffset(reader, metadataRva, sections, sectionCount);
            if (ReadUInt32(reader, metadata) != 0x424A5342)
                throw new InvalidDataException("assembly has no CLI metadata root");

            var versionLength = ReadInt32(reader, metadata + 12);
            var position = Align4(metadata + 16 + versionLength);
            var streamCount = ReadUInt16(reader, position + 2);
            position += 4;
            long tables = -1;
            long guidHeap = -1;
            for (var i = 0; i < streamCount; i++)
            {
                var offset = ReadUInt32(reader, position);
                position += 8; // offset + size
                var nameStart = position;
                var nameBytes = new List<byte>();
                byte value;
                while ((value = ReadByte(reader, position++)) != 0)
                    nameBytes.Add(value);
                var name = Encoding.ASCII.GetString(nameBytes.ToArray());
                position = Align4(position);
                if (name == "#~" || name == "#-") tables = metadata + offset;
                if (name == "#GUID") guidHeap = metadata + offset;
                if (position <= nameStart)
                    throw new InvalidDataException("invalid metadata stream header");
            }
            if (tables < 0 || guidHeap < 0)
                throw new InvalidDataException("assembly metadata has no tables or GUID heap");

            var heapSizes = ReadByte(reader, tables + 6);
            var valid = ReadUInt64(reader, tables + 8);
            if ((valid & 1UL) == 0)
                throw new InvalidDataException("assembly metadata has no Module table");
            var rowCounts = tables + 24;
            var presentTables = CountBits(valid);
            var rows = rowCounts + presentTables * 4L;
            var stringIndexSize = (heapSizes & 0x01) != 0 ? 4 : 2;
            var guidIndexSize = (heapSizes & 0x02) != 0 ? 4 : 2;
            var mvidIndexOffset = rows + 2 + stringIndexSize;
            var mvidIndex = guidIndexSize == 4
                ? ReadUInt32(reader, mvidIndexOffset)
                : ReadUInt16(reader, mvidIndexOffset);
            if (mvidIndex == 0)
                throw new InvalidDataException("assembly Module table has no MVID");

            stream.Position = guidHeap + (mvidIndex - 1L) * 16L;
            var bytes = reader.ReadBytes(16);
            if (bytes.Length != 16)
                throw new EndOfStreamException("assembly MVID is truncated");
            return new Guid(bytes);
        }

        private static bool IsProductAssembly(string name) =>
            !string.IsNullOrEmpty(name) &&
            (name.StartsWith("UnityMCP.", StringComparison.Ordinal) ||
             string.Equals(name, "UnityMCP", StringComparison.Ordinal));

        internal static string[] SelectInventoryNames(
            IEnumerable<string> compiledNames,
            ISet<string> loadedCompilerOutputs,
            ISet<string> testReferenced,
            IReadOnlyDictionary<string, string[]> dependencies)
        {
            var compiled = new HashSet<string>(
                compiledNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(compiled.Where(name =>
                IsProductAssembly(name) ||
                (loadedCompilerOutputs != null &&
                 loadedCompilerOutputs.Contains(name) &&
                 testReferenced != null && testReferenced.Contains(name))));
            while (pending.Count > 0)
            {
                var name = pending.Pop();
                if (!compiled.Contains(name) || !selected.Add(name)) continue;
                if (dependencies == null ||
                    !dependencies.TryGetValue(name, out var referenced)) continue;
                foreach (var dependency in referenced ?? Array.Empty<string>())
                    if (!selected.Contains(dependency)) pending.Push(dependency);
            }
            return selected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        internal static string ResolveSourcePath(
            string projectRoot,
            string path,
            Func<string, string> packageRootResolver = null)
        {
            if (string.IsNullOrEmpty(path)) return "";
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            var assetPath = path.Replace('\\', '/');
            const string packagesPrefix = "Packages/";
            if (!assetPath.StartsWith(packagesPrefix, StringComparison.Ordinal))
                return Path.GetFullPath(Path.Combine(projectRoot, assetPath));

            var separator = assetPath.IndexOf('/', packagesPrefix.Length);
            if (separator < 0)
                throw new InvalidDataException(
                    "package source path has no package-relative component: " + path);
            var packageAssetPath = assetPath.Substring(0, separator);
            var packageRoot = packageRootResolver?.Invoke(packageAssetPath);
            if (string.IsNullOrEmpty(packageRoot))
            {
                var package = PackageInfo.FindForAssetPath(assetPath) ??
                    PackageInfo.FindForAssetPath(packageAssetPath);
                packageRoot = package?.resolvedPath;
            }
            if (string.IsNullOrEmpty(packageRoot))
                throw new InvalidDataException(
                    "cannot resolve package source path: " + path);
            return Path.GetFullPath(Path.Combine(
                packageRoot, assetPath.Substring(separator + 1)));
        }

        private static string ResolveOutputPath(string projectRoot, string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        }

        // Mirrors UTF 1.6 EditorLoadedTestAssemblyProvider's test-reference
        // criterion, while keeping the implementation on public APIs.
        private static bool ReferencesTestFramework(
            System.Reflection.Assembly assembly,
            IReadOnlyDictionary<string, System.Reflection.Assembly[]> loadedByName,
            IDictionary<System.Reflection.Assembly, bool> cache,
            ISet<System.Reflection.Assembly> visiting)
        {
            if (cache.TryGetValue(assembly, out var cached)) return cached;
            if (!visiting.Add(assembly)) return false;
            var referencesFramework = assembly.GetReferencedAssemblies().Any(reference =>
                TestFrameworkAssemblies.Contains(reference.Name) ||
                (loadedByName.TryGetValue(reference.Name, out var candidates) &&
                 candidates.Any(candidate => ReferencesTestFramework(
                     candidate, loadedByName, cache, visiting))));
            visiting.Remove(assembly);
            cache[assembly] = referencesFramework;
            return referencesFramework;
        }

        private static bool HasLoadedCompilerOutput(
            Assembly assembly,
            string projectRoot,
            IReadOnlyDictionary<string, System.Reflection.Assembly[]> loadedByName)
        {
            if (!loadedByName.TryGetValue(assembly.name, out var candidates)) return false;
            var outputPath = ResolveOutputPath(projectRoot, assembly.outputPath);
            return candidates.Any(candidate => PathsEqual(
                SafeAssemblyLocation(candidate), outputPath));
        }

        private static string SafeAssemblyLocation(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly?.Location ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path) =>
            Path.GetFullPath(path).Replace('\\', '/');

        private static string HashFile(string path)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(path);
            return ToHex(algorithm.ComputeHash(stream));
        }

        private static string HashText(string value)
        {
            using var algorithm = SHA256.Create();
            return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static long RvaToOffset(
            BinaryReader reader,
            uint rva,
            long sectionHeaders,
            int sectionCount)
        {
            for (var i = 0; i < sectionCount; i++)
            {
                var section = sectionHeaders + i * 40L;
                var virtualSize = ReadUInt32(reader, section + 8);
                var virtualAddress = ReadUInt32(reader, section + 12);
                var rawSize = ReadUInt32(reader, section + 16);
                var rawPointer = ReadUInt32(reader, section + 20);
                var size = Math.Max(virtualSize, rawSize);
                if (rva >= virtualAddress && rva < virtualAddress + size)
                    return rawPointer + (rva - virtualAddress);
            }
            throw new InvalidDataException($"RVA 0x{rva:X8} is outside PE sections");
        }

        private static int CountBits(ulong value)
        {
            var count = 0;
            while (value != 0)
            {
                count += (int)(value & 1UL);
                value >>= 1;
            }
            return count;
        }

        private static long Align4(long value) => (value + 3L) & ~3L;

        private static byte ReadByte(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadByte();
        }

        private static ushort ReadUInt16(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadUInt16();
        }

        private static uint ReadUInt32(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadUInt32();
        }

        private static int ReadInt32(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadInt32();
        }

        private static ulong ReadUInt64(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadUInt64();
        }
    }
}
