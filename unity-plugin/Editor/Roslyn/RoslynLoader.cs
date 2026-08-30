using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
namespace UnityMCP.Editor
{
    internal enum RoslynPairSelection { None, Valid, Invalid }
    internal sealed class RoslynAssemblySnapshot
    {
        internal readonly Assembly Assembly;
        internal readonly AssemblyName Identity;
        internal readonly AssemblyName[] References;
        internal readonly string Location;
        internal readonly Guid Mvid;
        internal readonly bool LocationExists;
        internal RoslynAssemblySnapshot(
            Assembly assembly, AssemblyName identity, AssemblyName[] references,
            string location, Guid mvid, bool locationExists)
        {
            Assembly = assembly; Identity = identity;
            References = references ?? Array.Empty<AssemblyName>();
            Location = location; Mvid = mvid; LocationExists = locationExists;
        }
    }
    internal static class RoslynLoader
    {
        private const string CoreName = "Microsoft.CodeAnalysis";
        private const string CompilerName = "Microsoft.CodeAnalysis.CSharp";
        private const string MicrosoftToken = "31bf3856ad364e35";
        private static readonly object Gate = new object();
        private static Assembly _core;
        private static Assembly _compiler;
        private static Guid _coreMvid;
        private static Guid _compilerMvid;
        internal static Assembly RoslynCore => _core;
        internal static Assembly RoslynCompiler => _compiler;

        internal static bool EnsureRoslyn()
        {
            lock (Gate)
            {
                RoslynAssemblySnapshot core, compiler;
                var selection = SelectLoadedPair(GetLoadedSnapshots(), out core, out compiler);
                string fallbackDirectory = null;
                Assembly loadedCore = null, loadedCompiler = null;
                if (selection == RoslynPairSelection.None)
                {
                    fallbackDirectory = GetFallbackDirectory();
                    if (string.IsNullOrEmpty(fallbackDirectory))
                        return FailClosed();
                    try
                    {
                        loadedCore = LoadFrom(Path.Combine(fallbackDirectory, CoreName + ".dll"));
                        loadedCompiler = LoadFrom(Path.Combine(fallbackDirectory, CompilerName + ".dll"));
                    }
                    catch
                    {
                        return FailClosed();
                    }
                    // LoadFrom may bind another binary; trust only the resulting graph.
                    selection = SelectLoadedPair(GetLoadedSnapshots(), out core, out compiler);
                }

                if (selection != RoslynPairSelection.Valid)
                    return FailClosed();
                if (fallbackDirectory != null && !IsExactFallbackResult(
                        core, compiler, loadedCore, loadedCompiler, fallbackDirectory))
                    return FailClosed();
                if (ReferenceEquals(_core, core.Assembly) &&
                    ReferenceEquals(_compiler, compiler.Assembly) &&
                    _coreMvid == core.Mvid && _compilerMvid == compiler.Mvid)
                    return true;
                if (!ProbeCompilerPair(core.Assembly, compiler.Assembly))
                    return FailClosed();

                // Publish only after identity and executable ABI validation.
                _core = core.Assembly; _compiler = compiler.Assembly;
                _coreMvid = core.Mvid; _compilerMvid = compiler.Mvid;
                return true;
            }
        }
        internal static RoslynPairSelection SelectLoadedPair(
            IReadOnlyList<RoslynAssemblySnapshot> assemblies,
            out RoslynAssemblySnapshot core, out RoslynAssemblySnapshot compiler)
        {
            core = null;
            compiler = null;
            if (assemblies == null)
                return RoslynPairSelection.Invalid;

            var cores = assemblies.Where(a => a?.Identity?.Name == CoreName).ToArray();
            var compilers = assemblies.Where(a => a?.Identity?.Name == CompilerName).ToArray();
            if (cores.Length == 0 && compilers.Length == 0)
                return RoslynPairSelection.None;
            if (cores.Length != 1 || compilers.Length != 1)
                return RoslynPairSelection.Invalid;
            core = cores[0];
            compiler = compilers[0];
            return IsCompatiblePair(core, compiler)
                ? RoslynPairSelection.Valid
                : RoslynPairSelection.Invalid;
        }
        internal static string SelectFallbackDirectory(
            IEnumerable<string> candidates,
            Func<string, bool> directoryExists,
            Func<string, bool> fileExists)
        {
            if (candidates == null || directoryExists == null || fileExists == null)
                return null;
            foreach (var directory in candidates.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                if (!directoryExists(directory))
                    continue;
                if (fileExists(Path.Combine(directory, CoreName + ".dll")) &&
                    fileExists(Path.Combine(directory, CompilerName + ".dll")))
                    return directory;
            }
            return null;
        }
        private static bool IsCompatiblePair(
            RoslynAssemblySnapshot core,
            RoslynAssemblySnapshot compiler)
        {
            if (core.Assembly == null || compiler.Assembly == null ||
                ReferenceEquals(core.Assembly, compiler.Assembly) ||
                !IsMicrosoftIdentity(core.Identity, CoreName) ||
                !IsMicrosoftIdentity(compiler.Identity, CompilerName) ||
                core.Identity.Version == null ||
                !core.Identity.Version.Equals(compiler.Identity.Version) ||
                core.Mvid == Guid.Empty || compiler.Mvid == Guid.Empty ||
                !core.LocationExists || !compiler.LocationExists ||
                !TryGetCanonicalDirectory(core.Location, out var coreDirectory) ||
                !TryGetCanonicalDirectory(compiler.Location, out var compilerDirectory) ||
                !string.Equals(coreDirectory, compilerDirectory, PathComparison()))
                return false;

            var references = compiler.References
                .Where(reference => reference?.Name == CoreName).ToArray();
            return references.Length == 1 && string.Equals(
                core.Identity.FullName, references[0].FullName, StringComparison.Ordinal);
        }
        private static bool IsMicrosoftIdentity(AssemblyName identity, string expectedName)
        {
            return identity != null &&
                   identity.Name == expectedName &&
                   string.IsNullOrEmpty(identity.CultureName) &&
                   Token(identity) == MicrosoftToken;
        }
        private static string Token(AssemblyName identity)
        {
            var token = identity.GetPublicKeyToken();
            return token == null ? string.Empty :
                string.Concat(token.Select(value => value.ToString("x2")));
        }

        private static bool TryGetCanonicalDirectory(string location, out string directory)
        {
            directory = null;
            if (string.IsNullOrWhiteSpace(location) || !Path.IsPathRooted(location))
                return false;
            try
            {
                directory = Path.GetDirectoryName(Path.GetFullPath(location));
                return !string.IsNullOrEmpty(directory);
            }
            catch { return false; }
        }

        private static StringComparison PathComparison() =>
            Environment.OSVersion.Platform == PlatformID.Win32NT
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static bool IsExactFallbackResult(
            RoslynAssemblySnapshot core, RoslynAssemblySnapshot compiler,
            Assembly loadedCore, Assembly loadedCompiler, string directory) =>
            ReferenceEquals(core.Assembly, loadedCore) &&
            ReferenceEquals(compiler.Assembly, loadedCompiler) &&
            IsExactFallbackPath(core.Location, directory, CoreName + ".dll") &&
            IsExactFallbackPath(compiler.Location, directory, CompilerName + ".dll");

        private static bool IsExactFallbackPath(
            string location, string directory, string fileName)
        {
            if (!TryGetCanonicalDirectory(location, out var actualDirectory) ||
                !TryGetCanonicalDirectory(Path.Combine(directory, fileName),
                    out var expectedDirectory))
                return false;
            return string.Equals(actualDirectory, expectedDirectory, PathComparison()) &&
                   string.Equals(Path.GetFileName(location), fileName,
                       StringComparison.Ordinal);
        }

        private static IReadOnlyList<RoslynAssemblySnapshot> GetLoadedSnapshots()
        {
#if UNITY_INCLUDE_TESTS
            if (TestOpsOverride?.Snapshots != null)
                return TestOpsOverride.Snapshots();
#endif
            var snapshots = new List<RoslynAssemblySnapshot>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName identity;
                try { identity = assembly.GetName(); }
                catch { continue; }
                if (identity.Name != CoreName && identity.Name != CompilerName)
                    continue;

                try
                {
                    var location = assembly.Location;
                    snapshots.Add(new RoslynAssemblySnapshot(assembly, identity,
                        assembly.GetReferencedAssemblies(), location,
                        assembly.ManifestModule.ModuleVersionId,
                        !string.IsNullOrEmpty(location) && File.Exists(location)));
                }
                catch
                {
                    snapshots.Add(new RoslynAssemblySnapshot(
                        assembly, identity, Array.Empty<AssemblyName>(), null,
                        Guid.Empty, false));
                }
            }
            return snapshots;
        }

        private static string GetFallbackDirectory()
        {
#if UNITY_INCLUDE_TESTS
            if (TestOpsOverride?.Fallback != null)
                return TestOpsOverride.Fallback();
#endif
            var root = EditorApplication.applicationContentsPath;
            var candidates = new[]
            {
                Path.Combine(root, "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"),
                Path.Combine(root, "Resources", "Scripting", "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"),
                Path.Combine(root, "Resources", "Scripting", "DotNetSdkRoslyn"),
                Path.Combine(root, "DotNetSdkRoslyn")
            };
            return SelectFallbackDirectory(candidates, Directory.Exists, File.Exists);
        }

        private static Assembly LoadFrom(string path)
        {
#if UNITY_INCLUDE_TESTS
            if (TestOpsOverride?.Load != null)
                return TestOpsOverride.Load(path);
#endif
            return Assembly.LoadFrom(path);
        }

        private static bool ProbeCompilerPair(Assembly core, Assembly compiler)
        {
#if UNITY_INCLUDE_TESTS
            if (TestOpsOverride?.Probe != null)
                return TestOpsOverride.Probe(core, compiler);
#endif
            try
            {
                var syntaxTreeBase = core.GetType("Microsoft.CodeAnalysis.SyntaxTree", false);
                var metadataReference = core.GetType("Microsoft.CodeAnalysis.MetadataReference", false);
                var outputKind = core.GetType("Microsoft.CodeAnalysis.OutputKind", false);
                var syntaxTree = compiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", false);
                var compilation = compiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation", false);
                var compilationOptions = compiler.GetType(
                    "Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions", false);
                if (syntaxTreeBase == null || metadataReference == null || outputKind == null ||
                    syntaxTree == null || compilation == null || compilationOptions == null)
                    return false;

                var parse = FindOptionalMethod(syntaxTree, "ParseText",
                    BindingFlags.Public | BindingFlags.Static, typeof(string));
                if (parse == null) return false;
                var parsed = parse.Invoke(null, OptionalArguments(
                    parse.GetParameters(), "internal sealed class RoslynLoaderProbe {}"));
                if (parsed == null || !syntaxTreeBase.IsInstanceOfType(parsed))
                    return false;

                var createReference = FindOptionalMethod(metadataReference, "CreateFromFile",
                    BindingFlags.Public | BindingFlags.Static, typeof(string));
                if (createReference == null || string.IsNullOrEmpty(typeof(object).Assembly.Location))
                    return false;
                var reference = createReference.Invoke(null, OptionalArguments(
                    createReference.GetParameters(), typeof(object).Assembly.Location));
                if (reference == null || !metadataReference.IsInstanceOfType(reference))
                    return false;

                var optionConstructor = compilationOptions.GetConstructors()
                    .Where(constructor => CanInvokeWithFirst(
                        constructor.GetParameters(), outputKind))
                    .OrderBy(constructor => constructor.GetParameters().Length)
                    .FirstOrDefault();
                if (optionConstructor == null) return false;
                var dllKind = Enum.Parse(outputKind, "DynamicallyLinkedLibrary");
                var options = optionConstructor.Invoke(OptionalArguments(
                    optionConstructor.GetParameters(), dllKind));

                var trees = Array.CreateInstance(syntaxTreeBase, 1);
                trees.SetValue(parsed, 0);
                var references = Array.CreateInstance(metadataReference, 1);
                references.SetValue(reference, 0);
                var createCompilation = compilation
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.Name == "Create")
                    .FirstOrDefault(method => method.GetParameters().Length == 4 &&
                        method.GetParameters()[0].ParameterType == typeof(string));
                if (createCompilation == null) return false;
                var compiled = createCompilation.Invoke(null,
                    new object[] { "RoslynLoaderProbe", trees, references, options });
                if (compiled == null) return false;

                var emit = FindOptionalMethod(compiled.GetType(), "Emit",
                    BindingFlags.Public | BindingFlags.Instance, typeof(Stream));
                if (emit == null) return false;
                using (var stream = new MemoryStream())
                {
                    var result = emit.Invoke(compiled,
                        OptionalArguments(emit.GetParameters(), stream));
                    var success = result?.GetType().GetProperty(
                        "Success", BindingFlags.Public | BindingFlags.Instance);
                    return success != null && success.PropertyType == typeof(bool) &&
                           (bool)success.GetValue(result);
                }
            }
            catch { return false; }
        }

        private static bool CanInvokeWithFirst(ParameterInfo[] parameters, Type firstType)
        {
            return parameters.Length > 0 && parameters[0].ParameterType == firstType &&
                   parameters.Skip(1).All(parameter => parameter.HasDefaultValue);
        }

        private static MethodInfo FindOptionalMethod(
            Type type, string name, BindingFlags flags, Type firstType) =>
            type.GetMethods(flags)
                .Where(method => method.Name == name &&
                    CanInvokeWithFirst(method.GetParameters(), firstType))
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();

        private static object[] OptionalArguments(ParameterInfo[] parameters, object first)
        {
            var arguments = new object[parameters.Length];
            arguments[0] = first;
            for (var index = 1; index < parameters.Length; index++)
                arguments[index] = parameters[index].DefaultValue;
            return arguments;
        }

        private static bool FailClosed()
        {
            _core = null; _compiler = null;
            _coreMvid = Guid.Empty; _compilerMvid = Guid.Empty;
            return false;
        }

#if UNITY_INCLUDE_TESTS
        internal sealed class TestOps
        {
            internal Func<IReadOnlyList<RoslynAssemblySnapshot>> Snapshots;
            internal Func<string> Fallback;
            internal Func<string, Assembly> Load;
            internal Func<Assembly, Assembly, bool> Probe;
        }
        internal static TestOps TestOpsOverride;

        internal static Action BeginTestIsolation()
        {
            lock (Gate)
            {
                var core = _core; var compiler = _compiler;
                var coreMvid = _coreMvid; var compilerMvid = _compilerMvid;
                var ops = TestOpsOverride;
                return () =>
                {
                    lock (Gate)
                    {
                        _core = core; _compiler = compiler;
                        _coreMvid = coreMvid; _compilerMvid = compilerMvid;
                        TestOpsOverride = ops;
                    }
                };
            }
        }

        internal static void ResetForTests()
        {
            lock (Gate)
            {
                FailClosed();
                TestOpsOverride = null;
            }
        }
#endif
    }
}
