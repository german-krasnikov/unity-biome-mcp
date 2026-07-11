using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class CodeExecutor
    {
        private static readonly string[] Blocked = {
            "System.Diagnostics.Process", "System.IO.File", "System.IO.Directory",
            "System.IO.Stream", "FileStream", "StreamWriter", "StreamReader",
            "System.IO.Path", "System.Net.", "WebClient", "HttpClient",
            "Assembly.Load", "AppDomain", "DllImport", "extern ", "unsafe ",
            "System.Reflection.Assembly", "Type.GetType", ".GetMethod(",
            "GetRuntimeMethod", "DynamicInvoke",
            ".Invoke(", "System.Threading", "System.Runtime.InteropServices",
            "Environment.GetEnvironmentVariable",
            "System.Reflection.Emit", "DynamicMethod", "ILGenerator", "OpCodes",
            "Activator", "System.Linq.Expressions.Expression",
            "GetMethods(", "CreateDelegate", "GetTypes(", "GetMembers(",
            "GetProperties(", "GetFields(", "GetConstructors(", ".Assembly",
            // Singular reflection accessors (bypass via exact field/property name)
            "GetField(", "GetProperty(", "GetValue(", "SetValue(",
            // Block short-name bypass via using-directives and Environment.Exit (auto-using System)
            "Environment.Exit", "Environment.SetEnvironmentVariable",
            "using System.Diagnostics", "using System.IO", "using System.Net",
            "using System.Reflection",
            // Terminate/crash
            "EditorApplication.Exit", "Application.Quit", "Environment.FailFast",
            // Data exfiltration / untrusted import
            "AssetDatabase.ExportPackage", "AssetDatabase.ImportPackage",
            // Project switching / arbitrary asset creation
            "EditorApplication.OpenProject", "ProjectWindowUtil",
            // Using-alias bypass (= NS form not caught by "using NS" entries)
            "= System.IO", "= System.Diagnostics", "= System.Net", "= System.Reflection",
            // Dynamic compilation bypass — attacker could compile+exec arbitrary code
            "CSharpCodeProvider", "CodeDomProvider", "CompileAssemblyFrom",
            // Reflection method dispatch by name — bypasses allowlist
            "InvokeMember(",
            // Play-mode kill switch — could terminate the active session
            "EditorApplication.isPlaying", "EditorApplication.isPaused",
            // UnityEditor file API — bypasses System.IO block
            "FileUtil.",
        };

        // Pre-densified blocked patterns (whitespace stripped) for O(1) SecurityScan matching
        private static readonly string[] BlockedDense =
            Blocked.Select(b => System.Text.RegularExpressions.Regex.Replace(b, @"\s+", "")).ToArray();

        private const string Usings =
            "using UnityEngine; using UnityEditor; using System; using System.Linq; using System.Collections.Generic;";

        // Delegates to RoslynLoader — single source for Roslyn DLL state.
        private static Assembly _roslynCompiler => RoslynLoader.RoslynCompiler;
        private static Assembly _roslynCore     => RoslynLoader.RoslynCore;
        private static int _compilationCount;

        public static string Execute(string code, string undoLabel)
        {
            SecurityScan(code);

            var wrapped = WrapIfBareCode(code);

            EnsureRoslyn();

            var assembly = Compile(wrapped);
            return RunWithUndo(assembly, undoLabel);
        }

        // Exposed for tests
        internal static string WrapIfBareCode(string code)
        {
            if (code.Contains("class ") || code.Contains("namespace "))
                return code;

            // Trailing "return null;" guarantees every code path returns (fixes CS0161 for
            // bare statements with no return). #pragma suppresses the resulting CS0162
            // "unreachable code" warning when the user's snippet already has its own return.
            return $"{Usings}\n" +
                   "public static class __MCPScript { public static object Run() {\n" +
                   "#pragma warning disable 162\n" +
                   $"{code}\n" +
                   "return null;\n" +
                   "#pragma warning restore 162\n" +
                   "} }";
        }

        internal static void SecurityScan(string code)
        {
            var stripped = StripComments(code);
            // Remove all whitespace so comment-split and newline-split bypasses are caught
            var dense = System.Text.RegularExpressions.Regex.Replace(stripped, @"\s+", "");
            for (int i = 0; i < Blocked.Length; i++)
            {
                if (dense.IndexOf(BlockedDense[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException(
                        $"Security: blocked pattern '{Blocked[i]}'. Only UnityEngine/UnityEditor APIs allowed.");
            }
        }

        private static string StripComments(string code)
        {
            // Strip block comments /* ... */ including multiline
            code = System.Text.RegularExpressions.Regex.Replace(
                code, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            // Collapse string literal contents so // inside strings doesn't trigger line-comment strip
            code = System.Text.RegularExpressions.Regex.Replace(code, @"""(?:[^""\\]|\\.)*""", "\"\"");
            // Strip single-line comments // to end of line
            code = System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", "");
            return code;
        }

        private static void EnsureRoslyn()
        {
            if (!RoslynLoader.EnsureRoslyn())
                throw new InvalidOperationException("Roslyn DLLs not found — execute_code unavailable.");
        }

        private static Assembly Compile(string code)
        {
            if (_compilationCount >= 200)
                Debug.LogWarning("[MCP] execute_code: 200+ compilations — assembly leak risk in Mono. Consider restarting Unity.");
            _compilationCount++;

            // ParseText: find overload where first param is string (or SourceText) — use the one
            // that can accept only a string argument by filling remaining optional params with defaults.
            var syntaxTreeType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
            // Find ParseText overload where first param is string (not SourceText)
            var parseMethod = syntaxTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "ParseText"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length) // prefer shortest match
                .FirstOrDefault();

            if (parseMethod == null)
                throw new InvalidOperationException("CSharpSyntaxTree.ParseText(string,...) not found");

            var syntaxTree = parseMethod.Invoke(null, BuildInvokeArgs(parseMethod, code))
                ?? throw new InvalidOperationException("ParseText returned null");

            var refList = BuildReferences();

            // CSharpCompilation.Create — pick overload with (string, IEnumerable<SyntaxTree>, IEnumerable<MetadataRef>, options)
            var compilationType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
            var createMethod = compilationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Create")
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (createMethod == null)
                throw new InvalidOperationException("CSharpCompilation.Create not found");

            var syntaxTreesArray = Array.CreateInstance(syntaxTree.GetType(), 1);
            syntaxTreesArray.SetValue(syntaxTree, 0);

            // Build CSharpCompilationOptions with OutputKind.DynamicallyLinkedLibrary
            var options = BuildCompilationOptions();

            var createParams = createMethod.GetParameters();
            var createArgs = new object[createParams.Length];
            createArgs[0] = "MCPScript";
            if (createParams.Length > 1) createArgs[1] = syntaxTreesArray;
            if (createParams.Length > 2) createArgs[2] = refList;
            if (createParams.Length > 3) createArgs[3] = options;
            for (int i = 4; i < createParams.Length; i++)
                createArgs[i] = createParams[i].HasDefaultValue ? createParams[i].DefaultValue : null;

            var compilation = createMethod.Invoke(null, createArgs);
            if (compilation == null)
                throw new InvalidOperationException("CSharpCompilation.Create returned null");

            // Emit to memory stream — find overload where first param accepts Stream
            using var ms = new MemoryStream();
            var emitMethod = compilation.GetType().GetMethods()
                .Where(m => m.Name == "Emit" && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(Stream))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (emitMethod == null)
                throw new InvalidOperationException("Compilation.Emit not found");

            var emitResult = emitMethod.Invoke(compilation, BuildEmitArgs(ms, emitMethod));

            CheckEmitResult(emitResult);

            return Assembly.Load(ms.ToArray());
        }

        private static object BuildCompilationOptions()
        {
            // OutputKind enum: DynamicallyLinkedLibrary = 2
            var outputKindType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.OutputKind")
                ?? _roslynCore.GetType("Microsoft.CodeAnalysis.OutputKind");
            var outputKindDll = outputKindType != null
                ? Enum.ToObject(outputKindType, 2)  // DynamicallyLinkedLibrary = 2
                : null;

            var optionsType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
            // Find constructor that accepts OutputKind as first parameter
            var ctor = optionsType.GetConstructors()
                .Where(c => c.GetParameters().Length > 0
                    && c.GetParameters()[0].ParameterType == outputKindType)
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null) return null; // fallback: no options
            return ctor.Invoke(BuildInvokeArgs(ctor, outputKindDll));
        }

        // Build invocation args: first param = firstArg, rest = defaults/null
        private static object[] BuildInvokeArgs(MethodBase method, object firstArg)
        {
            var p = method.GetParameters();
            var args = new object[p.Length];
            args[0] = firstArg;
            for (int i = 1; i < p.Length; i++)
                args[i] = p[i].HasDefaultValue ? p[i].DefaultValue : null;
            return args;
        }

        private static object[] BuildEmitArgs(MemoryStream ms, MethodInfo emitMethod)
        {
            var paramCount = emitMethod.GetParameters().Length;
            var args = new object[paramCount];
            args[0] = ms; // first param is always the peStream
            // rest default to null
            return args;
        }

        private static Array BuildReferences()
        {
            var metaRefType = _roslynCore.GetType("Microsoft.CodeAnalysis.MetadataReference");
            if (metaRefType == null)
                throw new InvalidOperationException("MetadataReference type not found in Roslyn core assembly");
            // Find CreateFromFile: pick the overload where first param is string, fewest params
            var createFromFileMethod = metaRefType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "CreateFromFile"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();
            if (createFromFileMethod == null)
                throw new InvalidOperationException("MetadataReference.CreateFromFile(string,...) not found");

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => IsAllowedAssembly(a))
                .Select(a => {
                    try { return a.Location; }
                    catch { return null; }
                })
                .Where(loc => !string.IsNullOrEmpty(loc) && File.Exists(loc))
                .Distinct()
                .ToArray();

            var refList = Array.CreateInstance(metaRefType, assemblies.Length);
            for (int i = 0; i < assemblies.Length; i++)
                refList.SetValue(
                    createFromFileMethod.Invoke(null, BuildInvokeArgs(createFromFileMethod, assemblies[i])), i);

            return refList;
        }

        // Name-based check only — used directly by tests and delegated to from Assembly overload.
        internal static bool IsAllowedAssembly(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "UnityMCP.Editor") return false;
            if (name.StartsWith("UnityMCP") && name.Contains(".Tests")) return false;
            if (name.StartsWith("Microsoft.CodeAnalysis")) return false;
            if (name.StartsWith("Mono.Cecil")) return false;
            return true;
        }

        internal static bool IsAllowedAssembly(Assembly a)
        {
            if (!IsAllowedAssembly(a.GetName().Name)) return false;
            try { return !string.IsNullOrEmpty(a.Location); }
            catch { return false; }
        }

        private static void CheckEmitResult(object emitResult)
        {
            var successProp = emitResult.GetType().GetProperty("Success");
            if ((bool)successProp.GetValue(emitResult)) return;

            var diagnosticsProp = emitResult.GetType().GetProperty("Diagnostics");
            var diagnostics = (System.Collections.IEnumerable)diagnosticsProp.GetValue(emitResult);
            var errors = diagnostics.Cast<object>()
                .Where(d => {
                    var severity = d.GetType().GetProperty("Severity")?.GetValue(d)?.ToString();
                    return severity == "Error";
                })
                .Select(d => d.ToString())
                .ToArray();
            var errorMessage = string.Join("\n", errors);
            Debug.LogError($"[MCP] execute_code compile error: {errorMessage}");
            throw new InvalidOperationException("Compile error:\n" + errorMessage);
        }

        private static string RunWithUndo(Assembly assembly, string undoLabel)
        {
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "__MCPScript")
                       ?? assembly.GetTypes().First();
            var method = type.GetMethod("Run",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoLabel);
            var groupId = Undo.GetCurrentGroup();
            if (method == null)
                throw new InvalidOperationException($"No public/private static Run() method found in {type.FullName}");
            try
            {
                var result = method.Invoke(null, null);
                return result?.ToString() ?? "null";
            }
            finally
            {
                Undo.CollapseUndoOperations(groupId);
            }
        }
    }
}
