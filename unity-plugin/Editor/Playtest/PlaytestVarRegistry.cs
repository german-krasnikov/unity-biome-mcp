using System;
using System.Collections.Generic;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    /// <summary>Delegate for reading a runtime value — injected for testability.</summary>
    internal delegate string ReadValueFn(string path, string comp, string field);

    /// <summary>
    /// Holds VAR bindings and resolves $name sigils at runtime.
    /// Created from ParseResult.VarDefs after Parse(). One instance per Run().
    /// </summary>
    internal class PlaytestVarRegistry
    {
        readonly ReadValueFn _readValue;

        // name → (path, comp, field) — parsed from @query on Register
        readonly Dictionary<string, (string path, string comp, string field)> _bindings =
            new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        // C03 — name (without '$') → value captured by a prior `MCP ... INTO $name` step.
        // Separate from _bindings: a captured value has no Unity path|comp|field to re-read,
        // it is a fixed string snapshotted at capture time.
        readonly Dictionary<string, string> _captured =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool HasAny => _bindings.Count > 0;

        /// <summary>Stores a value captured by `MCP ... INTO $name` (name without '$').</summary>
        public void SetCaptured(string name, string value) => _captured[name] = value;

        /// <summary>
        /// Resolves an exact `$name` sigil (the whole string, not embedded text) against a
        /// previously captured value. Returns false for anything else, including a query
        /// that merely contains a sigil — ASSERT/WAIT keep resolving those as Unity queries.
        /// </summary>
        public bool TryGetCaptured(string query, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(query)) return false;
            var m = PlaytestParser.SigilRegex.Match(query);
            if (!m.Success || m.Value != query) return false;
            return _captured.TryGetValue(m.Groups[1].Value, out value);
        }

        /// <param name="readValue">Delegate for reading Unity values. Null = use PlaytestRunner.ReadValue.</param>
        public PlaytestVarRegistry(ReadValueFn readValue = null)
        {
            _readValue = readValue;
        }

        /// <summary>Register a VAR binding. atQuery must start with @ (e.g. "@/Player|Health|hp").</summary>
        public void Register(string name, string atQuery)
        {
            var q = atQuery.TrimStart('@');
            var parts = q.Split('|');
            if (parts.Length < 3)
                throw new ArgumentException($"VAR '{name}': query must have pipe-separated path|comp|field (got '{atQuery}')");
            _bindings[name] = (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
        }

        /// <summary>Expand all $name refs in text using live Unity values.</summary>
        public string ExpandVars(string text)
        {
            if (string.IsNullOrEmpty(text) || !HasAny) return text;
            return PlaytestParser.SigilRegex.Replace(text, m => {
                var name = m.Groups[1].Value;
                if (!_bindings.TryGetValue(name, out var b)) return m.Value; // unknown — leave intact
                var fn = _readValue ?? DefaultReadValue;
                try
                {
                    return fn(b.path, b.comp, b.field);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"VAR ${name}: {e.Message}", e);
                }
            });
        }

        /// <summary>
        /// Expand $name sigils in a reference field (path/query) with the stored path string,
        /// NOT the live runtime value. Keeps WAIT_UNTIL query stable across polling ticks.
        /// </summary>
        public string ExpandVarRef(string text)
        {
            if (string.IsNullOrEmpty(text) || !HasAny) return text;
            return PlaytestParser.SigilRegex.Replace(text, m => {
                var name = m.Groups[1].Value;
                if (!_bindings.TryGetValue(name, out var b)) return m.Value;
                return b.path + "|" + b.comp + "|" + b.field;
            });
        }

        /// <summary>Return a ShallowClone of step with all string fields VAR-expanded.</summary>
        public PlaytestStep ExpandStep(PlaytestStep step)
        {
            if (!HasAny) return step;
            var s = step.ShallowClone();
            // Reference fields: expand to path string (stable across ticks)
            s.Path        = ExpandVarRef(s.Path);
            s.Query       = ExpandVarRef(s.Query);
            // Value fields: expand to live runtime value (re-evaluated each tick)
            s.Value       = ExpandVars(s.Value);
            s.Component   = ExpandVars(s.Component);
            s.Method      = ExpandVars(s.Method);
            s.Args        = ExpandVars(s.Args);
            s.Message     = ExpandVars(s.Message);
            s.RawPosition = ExpandVars(s.RawPosition);
            if (s.Queries != null)
                s.Queries = Array.ConvertAll(s.Queries, ExpandVarRef);
            if (s.BatchValues != null)
                s.BatchValues = Array.ConvertAll(s.BatchValues, ExpandVars);
            return s;
        }

        static string DefaultReadValue(string path, string comp, string field)
            => PlaytestRunner.ReadValue(path, comp, field);
    }
}
