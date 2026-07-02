using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal enum StepType { Move, Wait, WaitUntil, Assert, AssertConsoleClean, Snapshot, Invoke, Set, Log, TimeScale, Teleport, AssertBatch, AssertNear, Capture, AssertCaptured, Invariant, AssertConserved, Simulate, Monitor, TraceFlow, AssertCta, Click }

    internal class PlaytestStep
    {
        public StepType Type;
        public string Path;
        public Vector3 Position;
        public float Delay;
        public string Query;
        public string Op;
        public string Value;
        public float Timeout = 5f;
        public string Component;
        public string Method;
        public string Args;
        public string Message;
        public string[] Queries;
        public string RawLine;
        public string[] BatchOps;
        public string[] BatchValues;
        public string SimulatorName;
    }

    internal static class PlaytestParser
    {
        public static List<PlaytestStep> Parse(string script)
        {
            var steps = new List<PlaytestStep>();
            var lines = script.Split('\n');

            // First pass: collect ALIAS definitions
            var aliases = new Dictionary<string, string>();
            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.Trim();
                if (!trimmed.StartsWith("ALIAS ", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = trimmed.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3) aliases[parts[1]] = parts[2];
            }

            // Second pass: parse commands, applying alias substitution
            for (int i = 0; i < lines.Length; i++)
            {
                var rawLine = lines[i];
                // Apply alias substitutions
                foreach (var kv in aliases)
                    rawLine = ReplaceWholeWord(rawLine, kv.Key, kv.Value);

                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var cmd = tokens[0].ToUpperInvariant();
                if (cmd == "ALIAS") continue; // skip alias definitions

                var step = new PlaytestStep { RawLine = line };

                switch (cmd)
                {
                    case "MOVE":
                        step.Type = StepType.Move;
                        int toIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TO");
                        if (toIdx < 0 || toIdx + 1 >= tokens.Length)
                            throw new ArgumentException("MOVE syntax: MOVE [path] TO x,y,z");
                        step.Path = toIdx > 1 ? tokens[1] : null;
                        var posStr = tokens[toIdx + 1];
                        var f = ValueParser.ParseFloats(posStr, 3);
                        step.Position = new Vector3(f[0], f[1], f[2]);
                        break;

                    case "WAIT":
                        step.Type = StepType.Wait;
                        step.Delay = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        break;

                    case "WAIT_UNTIL":
                        step.Type = StepType.WaitUntil;
                        step.Query = tokens[1]; step.Op = tokens[2]; step.Value = tokens[3];
                        var tiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (tiIdx >= 0) step.Timeout = float.Parse(tokens[tiIdx + 1], CultureInfo.InvariantCulture);
                        break;

                    case "ASSERT":
                        step.Type = StepType.Assert;
                        step.Query = tokens[1]; step.Op = tokens[2]; step.Value = tokens[3];
                        break;

                    case "ASSERT_CONSOLE_CLEAN":
                        step.Type = StepType.AssertConsoleClean;
                        // ASSERT_CONSOLE_CLEAN IGNORE "pattern1", "pattern2"
                        if (tokens.Length > 1 && tokens[1].ToUpperInvariant() == "IGNORE")
                        {
                            var rest = string.Join(" ", tokens, 2, tokens.Length - 2);
                            step.Queries = rest.Split(',')
                                .Select(p => p.Trim().Trim('"'))
                                .Where(p => !string.IsNullOrEmpty(p))
                                .ToArray();
                        }
                        break;

                    case "ASSERT_BATCH":
                        step.Type = StepType.AssertBatch;
                        var batchQueries = new List<string>();
                        var batchOps = new List<string>();
                        var batchValues = new List<string>();
                        i++;
                        bool batchFoundEnd = false;
                        while (i < lines.Length)
                        {
                            var bRaw = lines[i];
                            foreach (var kv in aliases)
                                bRaw = ReplaceWholeWord(bRaw, kv.Key, kv.Value);
                            var bLine = bRaw.Trim();
                            if (bLine.ToUpperInvariant() == "END") { batchFoundEnd = true; break; }
                            if (!string.IsNullOrEmpty(bLine) && !bLine.StartsWith("#"))
                            {
                                var bt = bLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (bt.Length >= 4 && bt[0].ToUpperInvariant() == "ASSERT")
                                {
                                    batchQueries.Add(bt[1]);
                                    batchOps.Add(bt[2]);
                                    batchValues.Add(bt[3]);
                                }
                            }
                            i++;
                        }
                        if (!batchFoundEnd)
                            throw new ArgumentException("ASSERT_BATCH block missing END terminator");
                        step.Queries = batchQueries.ToArray();
                        step.BatchOps = batchOps.ToArray();
                        step.BatchValues = batchValues.ToArray();
                        break;

                    case "ASSERT_NEAR":
                        // ASSERT_NEAR /A /B threshold
                        step.Type = StepType.AssertNear;
                        step.Path = tokens[1];
                        step.Value = tokens[2];
                        step.Delay = float.Parse(tokens[3], CultureInfo.InvariantCulture);
                        break;

                    case "TELEPORT":
                        // TELEPORT /path x,y,z
                        step.Type = StepType.Teleport;
                        step.Path = tokens[1];
                        var tf = ValueParser.ParseFloats(tokens[2], 3);
                        step.Position = new Vector3(tf[0], tf[1], tf[2]);
                        break;

                    case "SNAPSHOT":
                        step.Type = StepType.Snapshot;
                        step.Queries = string.Join(" ", tokens, 1, tokens.Length - 1).Split(',');
                        break;

                    case "INVOKE":
                        step.Type = StepType.Invoke;
                        step.Path = tokens[1]; step.Component = tokens[2]; step.Method = tokens[3];
                        step.Args = tokens.Length > 4 ? tokens[4] : "";
                        break;

                    case "SET":
                        step.Type = StepType.Set;
                        step.Path = tokens[1]; step.Component = tokens[2]; step.Method = tokens[3]; step.Args = tokens[4];
                        break;

                    case "LOG":
                        step.Type = StepType.Log;
                        step.Message = string.Join(" ", tokens, 1, tokens.Length - 1);
                        break;

                    case "TIMESCALE":
                        step.Type = StepType.TimeScale;
                        step.Delay = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        break;

                    case "CAPTURE":
                        // CAPTURE label query
                        step.Type = StepType.Capture;
                        step.Message = tokens[1];
                        step.Query = tokens[2];
                        break;

                    case "ASSERT_CAPTURED":
                        // ASSERT_CAPTURED label MODE [subOp value]
                        step.Type = StepType.AssertCaptured;
                        step.Message = tokens[1];
                        step.Op = tokens[2];
                        if (tokens.Length >= 5) { step.Args = tokens[3]; step.Value = tokens[4]; }
                        break;

                    case "INVARIANT":
                        // INVARIANT query op value
                        step.Type = StepType.Invariant;
                        step.Query = tokens[1];
                        step.Op = tokens[2];
                        step.Value = tokens[3];
                        break;

                    case "ASSERT_CONSERVED":
                    {
                        // ASSERT_CONSERVED SUM q1 + q2 [+ q3...] == CONSTANT OVER duration
                        step.Type = StepType.AssertConserved;
                        var queries = new List<string>();
                        int ti = 1; // skip SUM
                        if (ti < tokens.Length && tokens[ti].ToUpperInvariant() == "SUM") ti++;
                        // collect query names until == or OVER keyword
                        while (ti < tokens.Length)
                        {
                            var t = tokens[ti];
                            if (t == "+" || t.ToUpperInvariant() == "==" || t.ToUpperInvariant() == "CONSTANT" || t.ToUpperInvariant() == "OVER") { ti++; continue; }
                            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) { ti++; continue; }
                            // OVER keyword precedes duration
                            if (tokens[ti - 1].ToUpperInvariant() == "OVER") break;
                            queries.Add(t);
                            ti++;
                        }
                        step.Queries = queries.ToArray();
                        // find OVER duration
                        var overIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (overIdx >= 0 && overIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[overIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "SIMULATE":
                    {
                        // SIMULATE name [DURATION n] [TIMESCALE n] [TARGET "path"] [FREQUENCY n]
                        step.Type = StepType.Simulate;
                        step.SimulatorName = tokens[1];
                        var durIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "DURATION");
                        if (durIdx >= 0) step.Timeout = float.Parse(tokens[durIdx + 1], CultureInfo.InvariantCulture);
                        var tsIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMESCALE");
                        if (tsIdx >= 0) step.Delay = float.Parse(tokens[tsIdx + 1], CultureInfo.InvariantCulture);
                        var tgtIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TARGET");
                        if (tgtIdx >= 0) step.Path = tokens[tgtIdx + 1].Trim('"');
                        var freqIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FREQUENCY");
                        if (freqIdx >= 0) step.Value = tokens[freqIdx + 1];
                        break;
                    }

                    case "MONITOR":
                    {
                        // MONITOR name  OR  MONITOR STOP
                        step.Type = StepType.Monitor;
                        if (tokens.Length > 1 && tokens[1].ToUpperInvariant() != "STOP")
                            step.Query = tokens[1];
                        break;
                    }

                    case "TRACE_FLOW":
                    {
                        // TRACE_FLOW FROM /path1 TO /path2 FIELD fieldName TIMEOUT n
                        step.Type = StepType.TraceFlow;
                        var fromIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FROM");
                        if (fromIdx >= 0) step.Path = tokens[fromIdx + 1];
                        var toIdxTf = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TO");
                        if (toIdxTf >= 0) step.Query = tokens[toIdxTf + 1];
                        var fieldIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FIELD");
                        if (fieldIdx >= 0) step.Method = tokens[fieldIdx + 1];
                        var tfTiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (tfTiIdx >= 0) step.Timeout = float.Parse(tokens[tfTiIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "ASSERT_CTA":
                    {
                        // ASSERT_CTA VISIBLE  OR  ASSERT_CTA CLICKABLE
                        step.Type = StepType.AssertCta;
                        step.Op = tokens.Length > 1 ? tokens[1].ToUpperInvariant() : "VISIBLE";
                        break;
                    }

                    case "CLICK":
                    case "TAP":
                    {
                        if (tokens.Length < 2)
                            throw new ArgumentException("CLICK requires object path");
                        step.Type = StepType.Click;
                        step.Path = tokens[1];
                        int waitIdx = Array.FindIndex(tokens, 2, t => t.ToUpperInvariant() == "WAIT");
                        if (waitIdx >= 0 && waitIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[waitIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    default:
                        throw new ArgumentException($"Unknown command: {cmd}");
                }
                steps.Add(step);
            }
            return steps;
        }

        // Replace whole-word occurrences of 'word' in a line (avoids partial matches)
        static string ReplaceWholeWord(string line, string word, string replacement)
        {
            int idx = 0;
            while ((idx = line.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                bool startOk = idx == 0 || !char.IsLetterOrDigit(line[idx - 1]) && line[idx - 1] != '_';
                bool endOk = idx + word.Length >= line.Length ||
                             !char.IsLetterOrDigit(line[idx + word.Length]) && line[idx + word.Length] != '_';
                if (startOk && endOk)
                {
                    line = line.Substring(0, idx) + replacement + line.Substring(idx + word.Length);
                    idx += replacement.Length;
                }
                else idx++;
            }
            return line;
        }

        internal static (string path, string comp, string field) ResolveQuery(string query, PlaytestConfig config)
        {
            if (config != null)
            {
                var alias = config.FindAlias(query);
                if (alias != null) return (alias.path, alias.component, alias.field);
            }
            var parts = query.Split('|');
            if (parts.Length >= 3) return (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
            if (parts.Length == 2) return (parts[0].Trim(), parts[1].Trim(), "");
            return (query, "", "");
        }

        internal static bool Compare(string actual, string op, string expected)
        {
            if (float.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var aF) &&
                float.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var eF))
            {
                return op switch
                {
                    "==" => Math.Abs(aF - eF) < 0.001f,
                    "!=" => Math.Abs(aF - eF) >= 0.001f,
                    ">" => aF > eF,
                    ">=" => aF >= eF,
                    "<" => aF < eF,
                    "<=" => aF <= eF,
                    _ => throw new ArgumentException($"Unknown operator: {op}")
                };
            }
            return op switch
            {
                "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "contains" => actual?.Contains(expected) == true,
                _ => throw new ArgumentException($"Operator '{op}' requires numeric values")
            };
        }
    }
}
