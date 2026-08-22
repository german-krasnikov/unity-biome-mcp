using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public enum SecurityLevel { Standard = 0, AllowAll = 1, Strict = 2 }

    internal static class CodeExecutor
    {
        // ── Security tier 1: blocked in Standard and Strict (bypassed by AllowAll) ───
        private static readonly string[] BlockedAlways = {
            "System.Diagnostics.Process", "System.IO.File", "System.IO.Directory",
            "System.IO.Stream", "FileStream", "StreamWriter", "StreamReader",
            "System.IO.Path", "System.Net.", "WebClient", "HttpClient",
            "Assembly.Load", "AppDomain", "DllImport",
            "System.Reflection.Assembly", "Type.GetType", ".GetMethod(",
            "GetRuntimeMethod", "DynamicInvoke",
            "System.Threading", "System.Runtime.InteropServices",
            "Environment.GetEnvironmentVariable",
            "System.Reflection.Emit", "DynamicMethod", "ILGenerator", "OpCodes",
            "Activator", "System.Linq.Expressions.Expression",
            "GetMethods(", "CreateDelegate", "GetTypes(", "GetMembers(",
            "GetConstructors(", ".Assembly",
            "Environment.Exit", "Environment.SetEnvironmentVariable",
            "using System.Diagnostics", "using System.IO", "using System.Net",
            "using System.Reflection",
            "EditorApplication.Exit", "Application.Quit", "Environment.FailFast",
            "AssetDatabase.ExportPackage", "AssetDatabase.ImportPackage",
            "EditorApplication.OpenProject", "ProjectWindowUtil",
            "= System.IO", "= System.Diagnostics", "= System.Net", "= System.Reflection",
            "CSharpCodeProvider", "CodeDomProvider", "CompileAssemblyFrom",
            "InvokeMember(",
            "EditorApplication.isPlaying", "EditorApplication.isPaused",
            "FileUtil.",
        };

        // ── Security tier 2: blocked in Standard and Strict, allowed in AllowAll
        private static readonly string[] BlockedReflectionAccess = {
            ".GetValue(", ".SetValue(", ".Invoke(",
        };

        // ── Security tier 3: blocked in Strict only ───────────────────────────
        private static readonly string[] BlockedStrictReflection = {
            "GetField(", "GetProperty(", "GetFields(", "GetProperties(",
        };

        // Pre-computed per-level arrays (avoid allocation per call)
        private static readonly string[] _scanStandard      = BlockedAlways.Concat(BlockedReflectionAccess).ToArray();
        private static readonly string[] _scanStrict        = _scanStandard.Concat(BlockedStrictReflection).ToArray();
        private static readonly string[] _scanStandardDense = Densify(_scanStandard);
        private static readonly string[] _scanStrictDense   = Densify(_scanStrict);

        // Word-boundary check for extern/unsafe — substring scan would block identifiers like "externalRef"
        private static readonly System.Text.RegularExpressions.Regex _wordBoundaryBlocked =
            new System.Text.RegularExpressions.Regex(
                @"\bextern\b|\bunsafe\b",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string[] Densify(string[] arr) =>
            arr.Select(b => System.Text.RegularExpressions.Regex.Replace(b, @"\s+", "")).ToArray();

        private static readonly System.Collections.Generic.Dictionary<string, string> _securityHints =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { ".GetValue(",       "Use SerializedObject.FindProperty().floatValue / stringValue / etc." },
                { ".SetValue(",       "Use sp.floatValue = v; sp.serializedObject.ApplyModifiedProperties()" },
                { "GetField(",        "Use SerializedObject.FindProperty(\"fieldName\") instead" },
                { "GetProperty(",     "Use SerializedObject or typeof(T).GetProperties() (plural, allowed)" },
                { "System.Threading", "Use EditorCoroutineUtility or async void for deferred work" },
                { "System.Net.",      "Network access not allowed in execute_code" },
                { "System.IO.File",   "File access not allowed — use AssetDatabase APIs instead" },
            };

        private const string Usings =
            "using UnityEngine; using UnityEditor; using System; using System.Linq; using System.Collections.Generic;" +
            " using Object = UnityEngine.Object;";

        // Delegates to RoslynLoader — single source for Roslyn DLL state.
        private static Assembly _roslynCompiler => RoslynLoader.RoslynCompiler;
        private static Assembly _roslynCore     => RoslynLoader.RoslynCore;
        private static int _compilationCount;

        // ── persist_as probe (lazy, cached) ──────────────────────────────────
        private static bool? _hasCreateFromStream;
        private static MethodInfo _createFromStream;

        /// <summary>Detects whether Roslyn ships CreateFromStream(Stream) — cached after first call.
        /// Returns true → Path A (zero disk I/O); false → Path B (temp DLL fallback).</summary>
        internal static bool ProbeCreateFromStream()
        {
            if (_hasCreateFromStream.HasValue) return _hasCreateFromStream.Value;
            if (!RoslynLoader.EnsureRoslyn())
            {
                _hasCreateFromStream = false;
                return false;
            }
            var metaRefType = _roslynCore.GetType("Microsoft.CodeAnalysis.MetadataReference");
            _createFromStream = metaRefType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateFromStream"
                    && m.GetParameters().Length > 0
                    && typeof(Stream).IsAssignableFrom(m.GetParameters()[0].ParameterType));
            _hasCreateFromStream = _createFromStream != null;
            return _hasCreateFromStream.Value;
        }

        public static string Execute(string code, string undoLabel, string persistAs = null)
        {
            SecurityScan(code);
            var wrapped = WrapIfBareCode(code);
            EnsureRoslyn();

            if (!string.IsNullOrEmpty(persistAs))
            {
                ProbeCreateFromStream();
                var (asm, bytes) = CompileToBytes(wrapped);
                HeldTypeStore.Register(persistAs, bytes);
                var hasRun = asm.GetTypes()
                    .Any(t => t.GetMethod("Run",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic) != null);
                if (!hasRun)
                    return $"persisted:{persistAs} ({HeldTypeStore.Count} held)";
                return RunWithUndo(asm, undoLabel);
            }

            return RunWithUndo(Compile(wrapped), undoLabel);
        }

        // Returns Assembly only — no byte[] allocation for the common non-persist path.
        private static Assembly Compile(string code)
        {
            var (asm, _) = CompileToBytes(code);
            return asm;
        }

        /// <summary>Clears all held assembly bytes and removes any Path B DLL files from disk.</summary>
        internal static void ClearHeld()
        {
            HeldTypeStore.Clear();
            var hotDir = Path.Combine(Application.dataPath, "..", "Library", "UnityMCP", "HotTypes");
            if (!Directory.Exists(hotDir)) return;
            foreach (var f in Directory.GetFiles(hotDir, "*.dll"))
                try { File.Delete(f); } catch { /* ignore locked files */ }
        }

        // Exposed for tests
        internal static string WrapIfBareCode(string code)
        {
            if (code.Contains("class ") || code.Contains("namespace "))
                return code;

            // bare `return;` at brace-depth 0 → `return null;` for object Run()
            // ponytail: brace counter is tricked by { in string literals; add string-strip pass if needed
            code = ReplaceTopLevelReturns(code);

            // Wave 2 #5: hoist namespace using directives above the class wrapper.
            // Matches `using System.Text;` (uppercase first char after `using `, no `=` before `;`).
            // Does NOT match: `using var x = ...` (lowercase v), `using (x)` (lowercase v + parens),
            // `using Object = UnityEngine.Object;` (has `=` — already in Usings, user shouldn't write it).
            // SecurityScan already blocked `using System.IO/Net/Reflection/Diagnostics`.
            var usingPattern = new System.Text.RegularExpressions.Regex(
                @"^\s*using\s+[A-Za-z_][\w.]+\s*;",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            var extraUsings = string.Join(" ",
                usingPattern.Matches(code)
                            .Cast<System.Text.RegularExpressions.Match>()
                            .Select(m => m.Value.Trim()));
            if (extraUsings.Length > 0)
                code = usingPattern.Replace(code, "");
            var allUsings = extraUsings.Length > 0 ? $"{Usings} {extraUsings}" : Usings;

            // Trailing "return null;" guarantees every code path returns (fixes CS0161 for
            // bare statements with no return). #pragma suppresses the resulting CS0162
            // "unreachable code" warning when the user's snippet already has its own return.
            return $"{allUsings}\n" +
                   "public static class __MCPScript { public static object Run() {\n" +
                   "#pragma warning disable 162\n" +
                   $"{code}\n" +
                   "return null;\n" +
                   "#pragma warning restore 162\n" +
                   "} }";
        }

        // Depth-aware return; → return null; rewriter.
        // Only replaces `return;` at brace-depth 0 (top-level of snippet), leaving
        // `return;` inside local void functions intact.
        internal static string ReplaceTopLevelReturns(string code)
        {
            var sb = new System.Text.StringBuilder(code.Length);
            int depth = 0, i = 0;
            while (i < code.Length)
            {
                char c = code[i];
                if (c == '{') { depth++; sb.Append(c); i++; continue; }
                if (c == '}') { depth--; sb.Append(c); i++; continue; }
                // At depth 0 only: check for `return` (word-boundary) followed by whitespace/`;`
                bool wordBoundary = i == 0 || (!char.IsLetterOrDigit(code[i - 1]) && code[i - 1] != '_');
                if (depth == 0 && wordBoundary && c == 'r' && i + 5 < code.Length
                    && code[i + 1] == 'e' && code[i + 2] == 't'
                    && code[i + 3] == 'u' && code[i + 4] == 'r'
                    && code[i + 5] == 'n')
                {
                    int j = i + 6;
                    while (j < code.Length && (code[j] == ' ' || code[j] == '\t' || code[j] == '\n' || code[j] == '\r')) j++;
                    if (j < code.Length && code[j] == ';')
                    {
                        sb.Append("return null;");
                        i = j + 1;
                        continue;
                    }
                }
                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        internal static void SecurityScan(string code) =>
            SecurityScan(code, MCPSettings.GetSecurityLevel());

        internal static void SecurityScan(string code, SecurityLevel level)
        {
            if (level == SecurityLevel.AllowAll) return;
            var stripped = StripComments(code);
            var dense = System.Text.RegularExpressions.Regex.Replace(stripped, @"\s+", "");
            var (patterns, densePatterns) = level switch
            {
                SecurityLevel.Strict => (_scanStrict,    _scanStrictDense),
                _                    => (_scanStandard,  _scanStandardDense),
            };
            for (int i = 0; i < patterns.Length; i++)
            {
                if (dense.IndexOf(densePatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _securityHints.TryGetValue(patterns[i], out var hint);
                    var suffix = hint != null ? $" Suggestion: {hint}" : " Only UnityEngine/UnityEditor APIs allowed.";
                    throw new InvalidOperationException(
                        $"Security [{level}]: blocked pattern '{patterns[i]}'.{suffix}");
                }
            }
            // Word-boundary check for keywords that substring scan cannot handle safely
            var wbMatch = _wordBoundaryBlocked.Match(stripped);
            if (wbMatch.Success)
                throw new InvalidOperationException(
                    $"Security [{level}]: blocked keyword '{wbMatch.Value}'. Only UnityEngine/UnityEditor APIs allowed.");
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

        internal static (Assembly asm, byte[] bytes) CompileToBytes(string code)
        {
            if (_compilationCount >= 200)
                Debug.LogWarning($"{BiomeLabel.Tag} execute_code: 200+ compilations — assembly leak risk in Mono. Consider restarting Unity.");
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

            var asmBytes = ms.ToArray();
            return (Assembly.Load(asmBytes), asmBytes);
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

            // Append held assembly bytes as additional MetadataReferences
            var allHeld = HeldTypeStore.GetAll();
            if (allHeld.Count == 0) return refList;

            var extList = Array.CreateInstance(metaRefType, assemblies.Length + allHeld.Count);
            Array.Copy(refList, extList, assemblies.Length);
            int idx = assemblies.Length;

            if (ProbeCreateFromStream())
            {
                // Path A: zero disk I/O — wrap bytes in MemoryStream
                foreach (var bytes in allHeld.Values)
                    extList.SetValue(
                        _createFromStream.Invoke(null, BuildInvokeArgs(_createFromStream, new MemoryStream(bytes))),
                        idx++);
            }
            else
            {
                // Path B: write DLLs to Library/UnityMCP/HotTypes/ and use CreateFromFile
                var hotDir = Path.Combine(Application.dataPath, "..", "Library", "UnityMCP", "HotTypes");
                Directory.CreateDirectory(hotDir);
                foreach (var kv in allHeld)
                {
                    var path = Path.Combine(hotDir, SanitizeLabel(kv.Key) + ".dll");
                    if (!File.Exists(path) || !BytesEqual(File.ReadAllBytes(path), kv.Value))
                        File.WriteAllBytes(path, kv.Value);
                    extList.SetValue(
                        createFromFileMethod.Invoke(null, BuildInvokeArgs(createFromFileMethod, path)),
                        idx++);
                }
            }

            return extList;
        }

        private static string SanitizeLabel(string label)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in label)
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            var s = sb.ToString();
            if (s.Length > 64) s = s.Substring(0, 64);
            return s.Length == 0 ? "held" : s;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
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

        internal static bool IsAllowedAssembly(Assembly a) => IsAllowedAssembly(a.GetName().Name);

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
            Debug.LogError($"{BiomeLabel.Tag} execute_code compile error: {errorMessage}");
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
                throw new InvalidOperationException(
                    $"No static Run() in {type.FullName}. Add: public static object Run() {{ ... return result; }}");
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
