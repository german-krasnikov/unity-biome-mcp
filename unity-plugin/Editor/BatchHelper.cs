using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class BatchHelper
    {
        // Depth counter, not a bool: a nested `batch` command must not reset the flag
        // (and fire Physics.Sync) while the outer batch is still running. Sync once, at depth 0.
        private static int _batchDepth;
        internal static bool InBatch => _batchDepth > 0;

        // Testable seam — delegates to CommandRouter.IsCompiling so tests can inject false.
        internal static Func<bool> IsCompiling = () => CommandRouter.IsCompiling();

        public static string Execute(string commandsText, string onError, int timeoutMs = 25000, bool atomic = false)
        {
            var commands = ParseLines(commandsText);
            var sb = new StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool stopped = false;
            int okCount = 0, errCount = 0, timeoutCount = 0;

            _batchDepth++;
            // Only outermost atomic batch opens a named undo group.
            bool isAtomicRoot = atomic && _batchDepth == 1;
            int gid = -1;
            if (isAtomicRoot)
                gid = UndoGroupHelper.OpenNamedGroup("MCP Atomic Batch");

            // Returns true when caller should break out of the op loop.
            bool AtomicFail(int opIndex)
            {
                errCount++;
                if (!atomic && onError != "stop") return false;
                stopped = true;
                if (isAtomicRoot && UndoGroupHelper.CanRevert(gid))
                {
                    UndoGroupHelper.RevertToBeforeGroup(gid);
                    if (opIndex > 0)
                        sb.AppendLine($"ATOMIC_ROLLBACK: reverted ops 0..{opIndex - 1}");
                    else
                        sb.AppendLine("ATOMIC_ROLLBACK: op 0 failed, nothing to revert");
                }
                return atomic; // break only in atomic mode
            }

            try
            {

            for (int i = 0; i < commands.Count; i++)
            {
                if (stopped)
                {
                    sb.AppendLine($"[{i}] skip");
                    continue;
                }

                if (sw.ElapsedMilliseconds > Math.Max(timeoutMs, 1000))
                {
                    sb.AppendLine($"[{i}] TIMEOUT: batch deadline reached after {sw.ElapsedMilliseconds / 1000.0:F1}s");
                    for (int j = i + 1; j < commands.Count; j++)
                        sb.AppendLine($"[{j}] skip");
                    timeoutCount = commands.Count - i;
                    if (atomic) AtomicFail(i);
                    break;
                }

                var (cmd, argsJson) = commands[i];

                // Async-only commands cannot run inside batch (structural check — also
                // catches ask_user, which the old hardcoded name list here used to miss).
                if (!CommandRegistry.IsBatchable(cmd))
                {
                    sb.AppendLine($"[{i}] err: '{cmd}' requires async dispatch, not supported in batch");
                    if (AtomicFail(i)) break; else continue;
                }

                // Play Mode guards
                if (EditorApplication.isPlaying && CommandRegistry.IsMutating(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' is mutating, skipped in Play Mode");
                    if (AtomicFail(i)) break; else continue;
                }
                if (!EditorApplication.isPlaying && CommandRegistry.IsRuntime(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' is runtime-only, skipped outside Play Mode");
                    if (AtomicFail(i)) break; else continue;
                }

                // Compile guard
                if (IsCompiling() && CommandRegistry.IsMutating(cmd)
                    && !CommandRouter.IsAllowedDuringCompile(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' skipped during compilation");
                    if (AtomicFail(i)) break; else continue;
                }

                // Tool enabled check
                if (!CommandRouter.IsAlwaysAllowed(cmd) && !MCPSettings.IsToolEnabled(cmd))
                {
                    sb.AppendLine($"[{i}] err: Tool '{cmd}' disabled");
                    if (AtomicFail(i)) break; else continue;
                }

                // Validate schema before execution
                var validationErr = CommandValidator.Validate(cmd, argsJson);
                if (validationErr != null)
                {
                    sb.AppendLine($"[{i}] err: {validationErr}");
                    if (AtomicFail(i)) break; else continue;
                }

                try
                {
                    var result = CommandRouter.ExecuteCommand(cmd, argsJson);
                    if (result != "ok")
                        sb.AppendLine($"[{i}] {result}");
                    okCount++;
                }
                catch (Exception e)
                {
                    sb.AppendLine($"[{i}] err: {e.Message}");
                    if (AtomicFail(i)) break;
                }
            }

            // Summary line
            var summary = errCount > 0 || timeoutCount > 0
                ? $"ok:{okCount} err:{errCount}" + (timeoutCount > 0 ? $" timeout:{timeoutCount}" : "")
                : $"ok:{okCount}";
            sb.Append(summary);

            } // end try
            finally
            {
                if (isAtomicRoot)
                    // CollapseUndoOperations on an already-reverted/empty group is a Unity no-op — safe to call unconditionally.
                    UndoGroupHelper.CloseNamedGroup(gid);

                // Only the outermost batch flushes physics — once, after all nested ops settle.
                if (--_batchDepth == 0 && !EditorApplication.isPlaying)
                {
                    Physics.SyncTransforms();
                    Physics2D.SyncTransforms();
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        internal static List<(string cmd, string argsJson)> ParseLines(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(text)) return result;

            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var parsed = ParseLine(trimmed);
                if (!string.IsNullOrEmpty(parsed.cmd))
                    result.Add(parsed);
            }

            return result;
        }

        internal static (string cmd, string argsJson) ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return (null, null);

            // First word is command
            var firstSpace = line.IndexOf(' ');
            if (firstSpace == -1)
            {
                // Command with no args
                return (line, "{}");
            }

            var cmd = line.Substring(0, firstSpace);
            var rest = line.Substring(firstSpace + 1).Trim();

            // Parse key=value pairs into JSON
            var args = ParseKeyValuePairs(rest);
            var argsJson = BuildJsonObject(args);

            return (cmd, argsJson);
        }

        private static Dictionary<string, string> ParseKeyValuePairs(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;

            int i = 0;
            while (i < text.Length)
            {
                // Skip whitespace
                while (i < text.Length && text[i] == ' ')
                    i++;

                if (i >= text.Length) break;

                // Find key (up to =)
                var keyStart = i;
                while (i < text.Length && text[i] != '=')
                    i++;

                if (i >= text.Length) break;

                var key = text.Substring(keyStart, i - keyStart).Trim();
                i++; // skip =

                // Parse value
                var value = ParseValue(text, ref i);
                result[key] = value;
            }

            return result;
        }

        private static string ParseValue(string text, ref int i)
        {
            if (i >= text.Length) return "";

            // Quoted value
            if (text[i] == '"')
            {
                i++; // skip opening quote
                var sb = new StringBuilder();
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        switch (text[i])
                        {
                            case '"':  sb.Append('"');  break;
                            case '\\': sb.Append('\\'); break;
                            case 'n':  sb.Append('\n'); break;
                            case 'r':  sb.Append('\r'); break;
                            case 't':  sb.Append('\t'); break;
                            default:   sb.Append('\\'); sb.Append(text[i]); break;
                        }
                    }
                    else sb.Append(text[i]);
                    i++;
                }
                if (i < text.Length) i++; // skip closing quote
                while (i < text.Length && text[i] == ' ') i++; // skip trailing space
                return sb.ToString();
            }

            // Parenthesized value e.g. (0, 6.8, 0)
            if (text[i] == '(')
            {
                var pStart = i;
                int depth = 0;
                bool closed = false;
                while (i < text.Length)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') { depth--; if (depth == 0) { i++; closed = true; break; } }
                    i++;
                }
                if (!closed)
                    throw new Exception($"Unclosed '(' in value. Vectors like (0,0,0) must be on one line. Got: {text.Substring(pStart)}");
                while (i < text.Length && text[i] == ' ') i++;
                return text.Substring(pStart, i - pStart).TrimEnd();
            }

            // Unquoted value: consume until " identifier=" boundary or end-of-string
            var start = i;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    // Peek ahead: is the next non-space sequence "word=" ?
                    int j = i + 1;
                    while (j < text.Length && text[j] == ' ') j++;
                    int k = j;
                    while (k < text.Length && text[k] != '=' && text[k] != ' ') k++;
                    if (k < text.Length && text[k] == '=' && k > j)
                        break; // next key=value pair starts here
                }
                i++;
            }
            return text.Substring(start, i - start).TrimEnd();
        }

        private static string BuildJsonObject(Dictionary<string, string> args)
        {
            if (args.Count == 0) return "{}";
            var sb = new StringBuilder("{");
            bool first = true;

            foreach (var kvp in args)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(kvp.Key).Append("\":");

                sb.Append("\"").Append(JsonHelper.EscapeJson(kvp.Value)).Append("\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

    }
}
