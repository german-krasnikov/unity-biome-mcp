using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    // C5: Errors persisted to SessionState so they survive domain reload.
    // In-memory list is wiped on reload; SessionState key survives.
    [InitializeOnLoad]
    public static class CompileErrorCapture
    {
        static readonly List<string> _errors = new();
        const int MaxErrors = 50;
        const string SessionKey = "MCP_CompileErrors";
        const string AsmKeyPrefix = "MCP_CompileErrors_";

        // per-asmdef: assemblyPath → error messages
        static readonly Dictionary<string, List<string>> _asmErrors = new();

        static CompileErrorCapture()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        static void OnCompilationStarted(object obj)
        {
            _errors.Clear();
            _asmErrors.Clear();
            // C5: clear SessionState on new compile start so stale errors don't persist across compiles
            SessionState.EraseString(SessionKey);
        }

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            var asmName = Path.GetFileNameWithoutExtension(assemblyPath);
            var asmList = new List<string>();

            foreach (var msg in messages)
            {
                if (msg.type != CompilerMessageType.Error) continue;
                if (_errors.Count < MaxErrors)
                {
                    var line = $"{msg.file}:{msg.line}:{msg.column}: {msg.message}";
                    _errors.Add(line);
                    asmList.Add(line);
                }
            }

            if (asmList.Count > 0)
                _asmErrors[asmName] = asmList;

            // C5: write SessionState ONCE per assembly finish (after all messages processed).
            // Never write per-message — single write is the sharp-edge mitigation.
            SessionState.SetString(SessionKey, BuildErrorText(_errors));
            if (asmList.Count > 0)
                SessionState.SetString(AsmKeyPrefix + asmName, BuildErrorText(asmList));
        }

        public static bool HasErrors() => _errors.Count > 0
            || !string.IsNullOrEmpty(SessionState.GetString(SessionKey, ""))
               && SessionState.GetString(SessionKey, "") != "No compilation errors";

        public static string GetErrors()
        {
            // C5: if in-memory list is empty (e.g. after domain reload), fall back to SessionState
            if (_errors.Count > 0)
                return BuildErrorText(_errors);
            var persisted = SessionState.GetString(SessionKey, "");
            return string.IsNullOrEmpty(persisted) ? "No compilation errors" : persisted;
        }

        // C5: per-asmdef error retrieval by assembly path
        public static string GetErrorsForAssembly(string assemblyPath)
        {
            var asmName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (_asmErrors.TryGetValue(asmName, out var list) && list.Count > 0)
                return BuildErrorText(list);
            var persisted = SessionState.GetString(AsmKeyPrefix + asmName, "");
            return string.IsNullOrEmpty(persisted) ? "No compilation errors" : persisted;
        }

        public static void Clear()
        {
            _errors.Clear();
            _asmErrors.Clear();
            SessionState.EraseString(SessionKey);
        }

        static string BuildErrorText(List<string> errors)
        {
            if (errors.Count == 0) return "No compilation errors";
            var sb = new StringBuilder();
            sb.AppendLine($"{errors.Count} compilation error(s):");
            foreach (var e in errors) sb.AppendLine(e);
            return sb.ToString().TrimEnd();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: inject a compile error without real compilation.</summary>
        public static void InjectForTest(string msg)
        {
            if (_errors.Count < MaxErrors)
                _errors.Add(msg);
            // Also persist to SessionState so reload-survival can be tested
            SessionState.SetString(SessionKey, BuildErrorText(_errors));
        }

        /// <summary>Test seam: simulate domain reload (clears in-memory, SessionState survives).</summary>
        public static void SimulateDomainReload() => _errors.Clear();
#endif
    }
}
