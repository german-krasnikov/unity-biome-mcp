using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Playtest.Core
{
    // D05a — lookahead-heavy per-command step builders (extracted from the switch inside
    // BuildSteps in PlaytestParser.Internals.cs, so that file stays under the 750-line split
    // cap too) plus the pre-existing lower-level parse subroutines that both PlaytestParser.cs's
    // Parse() and PlaytestParser.Internals.cs's BuildSteps() call into (tokenizing, VAL/sigil
    // expansion, CALL/MACRO/FOR expansion, INCLUDE resolution, query resolution, value
    // comparison). Zero logic change from the pre-split file — each of the 4 step builders below
    // is the exact original case body, with the shared `pendingLabel = null;` reset relocated to
    // the (uniform, one-line) switch-case wrapper in BuildSteps, since all 4 cases unconditionally
    // reset it at that exact point with no intervening read (proven by an identical full Playtest
    // filter test count before/after).
    public static partial class PlaytestParser
    {
        // ── Lookahead step builders (moved out of BuildSteps's switch, D05a) ────────────

        // Was: PlaytestParser.cs "MOVE_PATH" case.
        internal static void BuildMovePathSteps(string[] tokens, string line, string pendingLabel, List<PlaytestStep> steps)
        {
            // MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]
            var tiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
            float pathTimeout = tiIdx >= 0 ? float.Parse(tokens[tiIdx + 1], CultureInfo.InvariantCulture) : 0f;
            int endIdx = tiIdx >= 0 ? tiIdx : tokens.Length;
            bool firstMove = true;
            for (int ci = 1; ci < endIdx; ci++)
            {
                if (tokens[ci] == ">") continue;
                var moveStep = new PlaytestStep
                {
                    Type = StepType.Move,
                    Timeout = pathTimeout,
                    RawLine = line
                };
                SetPosition(moveStep, tokens[ci]);
                if (firstMove) { moveStep.Label = pendingLabel; firstMove = false; }
                steps.Add(moveStep);
            }
        }

        // Was: PlaytestParser.cs "SWEEP_PATH" case.
        internal static void BuildSweepPathSteps(string[] tokens, string line, string[] lines, ref int i, string pendingLabel, List<PlaytestStep> steps)
        {
            // SWEEP_PATH <charPath> DWELL <n>
            //   x,y,z > x,y,z > ...
            // UNTIL <query> <op> <val> [TIMEOUT n]
            // Parse-time expansion → Move+Wait per waypoint, then WaitUntil
            if (tokens.Length < 4)
                throw new ArgumentException("SWEEP_PATH syntax: SWEEP_PATH <path> DWELL <n>");
            var sweepPath = tokens[1];
            var dwellIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "DWELL");
            if (dwellIdx < 0 || dwellIdx + 1 >= tokens.Length)
                throw new ArgumentException("SWEEP_PATH requires DWELL <seconds>");
            float dwell = float.Parse(tokens[dwellIdx + 1], CultureInfo.InvariantCulture);

            // Read waypoint lines until UNTIL or end-of-input
            var waypointTokens = new List<string>();
            string untilLine = null;
            while (i + 1 < lines.Length)
            {
                var nextTrimmed = lines[i + 1].Trim();
                if (string.IsNullOrEmpty(nextTrimmed) || nextTrimmed.StartsWith("#")) { i++; continue; }
                if (nextTrimmed.StartsWith("UNTIL ", StringComparison.OrdinalIgnoreCase))
                {
                    untilLine = nextTrimmed; i++;
                    break;
                }
                var firstWord = nextTrimmed.Split(new[] { ' ' }, 2, StringSplitOptions.None)[0].ToUpperInvariant();
                if (_DSL_KEYWORDS.Contains(firstWord)) break;
                waypointTokens.AddRange(nextTrimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
                i++;
            }
            if (waypointTokens.Count == 0)
                throw new ArgumentException("SWEEP_PATH: no waypoints found");

            // Emit Move+Wait per waypoint
            bool firstSweep = true;
            foreach (var wt in waypointTokens)
            {
                if (wt == ">") continue;
                var moveStep = new PlaytestStep { Type = StepType.Move, Path = sweepPath, RawLine = line };
                SetPosition(moveStep, wt);
                if (firstSweep) { moveStep.Label = pendingLabel; firstSweep = false; }
                steps.Add(moveStep);
                if (dwell > 0f)
                    steps.Add(new PlaytestStep { Type = StepType.Wait, Delay = dwell, RawLine = line });
            }

            // Emit WaitUntil from UNTIL clause
            if (untilLine != null)
            {
                var ut = untilLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                // ut[0]="UNTIL" ut[1]=query ut[2]=op ut[3]=val [TIMEOUT n]
                if (ut.Length < 4)
                    throw new ArgumentException("SWEEP_PATH UNTIL syntax: UNTIL <query> <op> <val> [TIMEOUT n]");
                var untilStep = new PlaytestStep { Type = StepType.WaitUntil, RawLine = untilLine };
                untilStep.Query = ut[1]; untilStep.Op = ut[2]; untilStep.Value = ut[3];
                var utTiIdx = Array.FindIndex(ut, t => t.ToUpperInvariant() == "TIMEOUT");
                if (utTiIdx >= 0) { untilStep.Timeout = float.Parse(ut[utTiIdx + 1], CultureInfo.InvariantCulture); untilStep.HasExplicitTimeout = true; }
                steps.Add(untilStep);
            }

        }

        // Was: PlaytestParser.cs "COMPLETE_PURCHASE" case.
        internal static void BuildCompletePurchaseSteps(string[] tokens, string line, string[] lines, ref int i, string pendingLabel, List<PlaytestStep> steps)
        {
            // COMPLETE_PURCHASE <path> EXPECT
            //   <q1>,<q2>,...
            // TIMEOUT <n>
            // Parse-time expansion → Invoke + compound WaitUntil
            if (tokens.Length < 2) throw new ArgumentException("COMPLETE_PURCHASE syntax: COMPLETE_PURCHASE <path> EXPECT");
            var cpPath = tokens[1];
            steps.Add(new PlaytestStep
            {
                Type = StepType.Invoke, Path = cpPath,
                Component = "PlacementPurchase", Method = "CompletePurchase",
                Args = "", RawLine = line, Label = pendingLabel
            });

            var expectQueries = new List<string>();
            float cpTimeout = 5f;
            bool cpTimeoutExplicit = false;
            while (i + 1 < lines.Length)
            {
                var nextT = lines[i + 1].Trim();
                if (string.IsNullOrEmpty(nextT) || nextT.StartsWith("#")) { i++; continue; }
                if (nextT.StartsWith("TIMEOUT ", StringComparison.OrdinalIgnoreCase))
                {
                    cpTimeout = float.Parse(nextT.Split(new[] { ' ' }, 2)[1].Trim(), CultureInfo.InvariantCulture);
                    cpTimeoutExplicit = true;
                    i++;
                    break;
                }
                if (nextT.StartsWith("EXPECT ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = nextT.Substring(7).Trim();
                    expectQueries.AddRange(rest.Split(',').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)));
                    i++;
                    continue;
                }
                // Plain comma-separated continuation line
                expectQueries.AddRange(nextT.Split(',').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)));
                i++;
            }

            if (expectQueries.Count > 0)
            {
                var wu = new PlaytestStep { Type = StepType.WaitUntil, Timeout = cpTimeout, HasExplicitTimeout = cpTimeoutExplicit, RawLine = line };
                wu.Query = expectQueries[0]; wu.Op = "=="; wu.Value = "True";
                if (expectQueries.Count > 1)
                {
                    wu.Queries = expectQueries.Skip(1).ToArray();
                    wu.BatchOps = Enumerable.Repeat("==", expectQueries.Count - 1).ToArray();
                    wu.BatchValues = Enumerable.Repeat("True", expectQueries.Count - 1).ToArray();
                    wu.IsOr = false;
                }
                steps.Add(wu);
            }
        }

        // Was: PlaytestParser.cs "INVOKE_REPEAT" case.
        internal static void BuildInvokeRepeatSteps(string[] tokens, string line, string[] lines, ref int i, string pendingLabel, List<PlaytestStep> steps)
        {
            // INVOKE_REPEAT <count> <path> <comp> <method> [args]
            // [EXPECT <query> <op> <val> [TIMEOUT n]]
            // Parse-time expansion → N Invoke steps + optional WaitUntil
            if (tokens.Length < 5)
                throw new ArgumentException("INVOKE_REPEAT syntax: INVOKE_REPEAT <count> <path> <comp> <method> [args]");
            int repeatCount = int.Parse(tokens[1]);
            var irPath = tokens[2]; var irComp = tokens[3]; var irMethod = tokens[4];
            // G2: join all remaining tokens so multi-arg calls are preserved
            var irArgs = tokens.Length > 5 ? string.Join(" ", tokens, 5, tokens.Length - 5) : "";

            bool firstInvoke = true;
            for (int ri = 0; ri < repeatCount; ri++)
            {
                var invStep = new PlaytestStep
                {
                    Type = StepType.Invoke, Path = irPath, Component = irComp,
                    Method = irMethod, Args = irArgs, RawLine = line
                };
                if (firstInvoke) { invStep.Label = pendingLabel; firstInvoke = false; }
                steps.Add(invStep);
            }

            // Read optional EXPECT line
            while (i + 1 < lines.Length)
            {
                var nextT = lines[i + 1].Trim();
                if (string.IsNullOrEmpty(nextT) || nextT.StartsWith("#")) { i++; continue; }
                if (nextT.StartsWith("EXPECT ", StringComparison.OrdinalIgnoreCase))
                {
                    var et = nextT.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (et.Length < 4) throw new ArgumentException("INVOKE_REPEAT EXPECT syntax: EXPECT <query> <op> <val> [TIMEOUT n]");
                    var wu = new PlaytestStep
                    {
                        Type = StepType.WaitUntil, Query = et[1], Op = et[2], Value = et[3], RawLine = nextT
                    };
                    var etTiIdx = Array.FindIndex(et, t => t.ToUpperInvariant() == "TIMEOUT");
                    if (etTiIdx >= 0) { wu.Timeout = float.Parse(et[etTiIdx + 1], CultureInfo.InvariantCulture); wu.HasExplicitTimeout = true; }
                    steps.Add(wu);
                    i++;
                }
                break; // EXPECT is optional; stop after first non-blank non-comment line
            }
        }


        // ── Phase helpers ──────────────────────────────────────────────────────────

        // Phase -1: expand INCLUDE directives recursively; each line carries provenance (file, line number)
        internal static SourcedLine[] ExpandIncludes(string[] lines, int depth, IncludeResolver resolver, string sourceFile = null)
        {
            if (depth > 5) throw new ArgumentException("INCLUDE depth exceeded (max 5)");
            var result = new List<SourcedLine>();
            for (int idx = 0; idx < lines.Length; idx++)
            {
                var line = lines[idx];
                var t = line.Trim();
                if (!t.StartsWith("INCLUDE ", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new SourcedLine { Text = line, File = sourceFile, Line = idx });
                    continue;
                }
                var filename = t.Substring(8).Trim().Trim('"');
                if (filename.Contains("..") || System.IO.Path.IsPathRooted(filename))
                    throw new ArgumentException($"INCLUDE '{filename}': path traversal not allowed");
                // Canonicalize to block symlink traversal outside PlaytestDefs/
                if (resolver == null)
                {
                    var basePath = System.IO.Path.GetFullPath("Assets/PlaytestDefs/").TrimEnd(
                        System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                    var fullPath = System.IO.Path.GetFullPath(
                        System.IO.Path.Combine("Assets/PlaytestDefs/", filename));
                    if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"INCLUDE '{filename}': path outside PlaytestDefs/");
                }
                string content;
                try
                {
                    if (resolver != null)
                        content = resolver(filename);
                    else
                        content = System.IO.File.ReadAllText($"Assets/PlaytestDefs/{filename}");
                }
                catch (Exception e) { throw new ArgumentException($"INCLUDE '{filename}': {e.Message}", e); }
                var included = content.Split('\n');
                result.AddRange(ExpandIncludes(included, depth + 1, resolver, filename));
            }
            return result.ToArray();
        }

        // Phase 0.7: collect VAL definitions with topo-sort cycle detection
        internal static Dictionary<string, string> CollectVals(string[] lines, List<string> warnings = null)
        {
            // First pass: gather raw unexpanded values
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (!t.StartsWith("VAL ", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = t.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    throw new ArgumentException($"VAL syntax: VAL $name value (got '{t}')");
                var firstWord = parts[2].Split(new[] { ' ' }, 2)[0];
                if (_DSL_KEYWORDS.Contains(firstWord))
                    throw new ArgumentException(
                        $"VAL '{parts[1]}': value cannot start with a DSL keyword (got '{firstWord}'). " +
                        $"Tip: wrap in quotes if it's a literal string value.");
                var key = parts[1].TrimStart('$');
                if (warnings != null && raw.ContainsKey(key))
                    warnings.Add($"Alias collision: local VAL ${key} shadows earlier definition (was '{raw[key]}', now '{parts[2]}')");
                raw[key] = parts[2];
            }
            if (raw.Count == 0) return raw;

            // Phase 0.7.1: collect PATH_PREFIX (first occurrence wins), apply to path VAL values
            // PATH_PREFIX applies across all INCLUDE-expanded lines; first occurrence wins.
            string pathPrefix = null;
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (!t.StartsWith("PATH_PREFIX ", StringComparison.OrdinalIgnoreCase)) continue;
                pathPrefix = t.Substring("PATH_PREFIX ".Length).Trim().TrimEnd('/');
                break;
            }
            if (pathPrefix != null)
            {
                foreach (var key in raw.Keys.ToList())
                {
                    if (raw[key].StartsWith("/"))
                        raw[key] = pathPrefix + raw[key];
                }
            }

            // Topo DFS: expand transitively, detect cycles
            var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(string name)
            {
                if (expanded.ContainsKey(name)) return;
                if (!visiting.Add(name))
                    throw new ArgumentException($"VAL cycle detected: ${name}");
                var val = raw[name];
                val = SigilRegex.Replace(val, m => {
                    var dep = m.Groups[1].Value;
                    if (!raw.ContainsKey(dep)) return m.Value; // unknown sigil — leave intact
                    Visit(dep);
                    return expanded[dep];
                });
                expanded[name] = val;
                visiting.Remove(name);
            }

            foreach (var name in raw.Keys) Visit(name);
            return expanded;
        }

        // Phase 0.7: expand $sigils in a single line using collected vals
        internal static string ExpandSigils(string line, Dictionary<string, string> vals)
        {
            if (vals == null || vals.Count == 0) return line;
            return SigilRegex.Replace(line, m => {
                var name = m.Groups[1].Value;
                return vals.TryGetValue(name, out var v) ? v : m.Value; // unknown → leave for VAR
            });
        }

        static List<SourcedLine> ExpandCalls(List<SourcedLine> lines,
            Dictionary<string, (string[] paramNames, SourcedLine[] body)> macros,
            int depth, string[] callStack = null)
        {
            if (depth > 10) throw new ArgumentException("MACRO recursion depth exceeded (max 10)");
            var result = new List<SourcedLine>();
            for (int ei = 0; ei < lines.Count; ei++)
            {
                var sourced = lines[ei];
                var t = sourced.Text.Trim();

                // ── FOR $var IN start..end ──────────────────────────────────────
                if (t.StartsWith("FOR ", StringComparison.OrdinalIgnoreCase))
                {
                    var forParts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (forParts.Length < 4 || !forParts[2].Equals("IN", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"FOR syntax: FOR $var IN start..end (got '{t}')");
                    var iterVar = forParts[1];
                    var rangeParts = forParts[3].Split(new[] { ".." }, StringSplitOptions.None);
                    if (rangeParts.Length != 2 ||
                        !int.TryParse(rangeParts[0], out int rangeStart) ||
                        !int.TryParse(rangeParts[1], out int rangeEnd))
                        throw new ArgumentException($"FOR range must be integer..integer (got '{forParts[3]}')");
                    if ((long)rangeEnd - (long)rangeStart > 10000)
                        throw new ArgumentException($"FOR range too large (max 10000 iterations)");

                    // Collect body until matching END_FOR (handle nested FOR)
                    var forBody = new List<SourcedLine>();
                    ei++;
                    bool foundEndFor = false;
                    int nestDepth = 0;
                    while (ei < lines.Count)
                    {
                        var bt = lines[ei].Text.Trim()
                            .Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (bt.Equals("FOR", StringComparison.OrdinalIgnoreCase)) nestDepth++;
                        else if (bt.Equals("END_FOR", StringComparison.OrdinalIgnoreCase))
                        {
                            if (nestDepth == 0) { foundEndFor = true; break; }
                            nestDepth--;
                        }
                        forBody.Add(lines[ei]);
                        ei++;
                    }
                    if (!foundEndFor) throw new ArgumentException("FOR block missing END_FOR");

                    for (int iter = rangeStart; iter < rangeEnd; iter++)
                    {
                        var forExpanded = new List<SourcedLine>();
                        foreach (var bodyLine in forBody)
                        {
                            var sub = ReplaceWholeWord(bodyLine.Text, iterVar, iter.ToString());
                            forExpanded.Add(new SourcedLine
                            {
                                Text = sub,
                                File = bodyLine.File,
                                Line = bodyLine.Line,
                                MacroStack = bodyLine.MacroStack == null
                                    ? new[] { $"FOR:{iter}" }
                                    : bodyLine.MacroStack.Concat(new[] { $"FOR:{iter}" }).ToArray()
                            });
                        }
                        result.AddRange(ExpandCalls(forExpanded, macros, depth + 1, callStack));
                    }
                    continue;
                }

                // ── CALL macroName [args...] ────────────────────────────────────
                if (!t.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase)) { result.Add(sourced); continue; }
                {
                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts[1];
                    if (!macros.TryGetValue(name, out var macro))
                        throw new ArgumentException($"Unknown macro: {name}");
                    var callArgs = parts.Skip(2).ToArray();
                    if (callArgs.Length < macro.paramNames.Length)
                        throw new ArgumentException($"CALL {name}: expected {macro.paramNames.Length} args, got {callArgs.Length}");

                    var newStack = callStack == null
                        ? new[] { name }
                        : callStack.Concat(new[] { name }).ToArray();

                    var expanded = new List<SourcedLine>();
                    foreach (var bodyLine in macro.body)
                    {
                        var sub = bodyLine.Text;
                        for (int j = 0; j < macro.paramNames.Length; j++)
                            sub = ReplaceWholeWord(sub, macro.paramNames[j], callArgs[j]);
                        expanded.Add(new SourcedLine
                        {
                            Text = sub,
                            File = bodyLine.File,
                            Line = bodyLine.Line,
                            MacroStack = newStack
                        });
                    }
                    result.AddRange(ExpandCalls(expanded, macros, depth + 1, newStack));
                }
            }
            return result;
        }

        // Deferred position: @-expression stored as RawPosition; literal parsed into Position
        static void SetPosition(PlaytestStep step, string token)
        {
            if (token.StartsWith("@"))
                step.RawPosition = token;
            else
            {
                var f = NumericParsing.ParseFloats(token, 3);
                step.Position = new Float3(f[0], f[1], f[2]);
            }
        }

        // Replace whole-word occurrences of 'word' in a line (avoids partial matches)
        internal static string ReplaceWholeWord(string line, string word, string replacement)
        {
            if (string.IsNullOrEmpty(word)) return line;
            int idx = 0;
            while ((idx = line.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                char prevCh = idx > 0 ? line[idx - 1] : '\0';
                bool startOk = idx == 0 || !char.IsLetterOrDigit(prevCh) && prevCh != '$'
                    && (prevCh != '_' || word.Length > 0 && word[0] == '$');
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

        internal static (string path, string comp, string field) ResolveQuery(string query, IAliasSource config)
        {
            if (config != null)
            {
                var alias = config.FindAlias(query);
                if (alias != null) return (alias.Value.Path, alias.Value.Component, alias.Value.Field);
            }
            var parts = query.Split('|');
            if (parts.Length >= 4 &&
                (parts[1].Trim() == "UIDocument" ||
                 parts[1].Trim() == "PanelRenderer" ||
                 parts[1].Trim() == "UI"))
                return (parts[0].Trim(), "UIDocument", parts[2].Trim() + "|" + parts[3].Trim());
            if (parts.Length >= 3) return (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
            if (parts.Length == 2) return (parts[0].Trim(), parts[1].Trim(), "");
            return (query, "", "");
        }

        // Maps |PanelRenderer| and |UI| DSL tokens to |UIDocument| at parse time.
        // PlaytestRunner.Steps.cs contains("|UIDocument|") discriminator stays unchanged.
        internal static string NormalizeUIHostPath(string path)
        {
            if (path == null) return null;
            return path
                .Replace("|PanelRenderer|", "|UIDocument|")
                .Replace("|UI|", "|UIDocument|");
        }

        // ── Tokenizer: bracket/quote-aware split ───────────────────────────────

        public static string[] SplitTokens(string line)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '['))
                { sb.Append(line[++i]); continue; }
                if (c == '"' && depth == 0) { inQuote = !inQuote; }
                else if (c == '[' && !inQuote) { depth++; sb.Append(c); }
                else if (c == ']' && !inQuote && depth > 0) { depth--; sb.Append(c); }
                else if (c == ' ' && !inQuote && depth == 0)
                { if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); } }
                else sb.Append(c);
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens.ToArray();
        }

        // keywords that end the value in ASSERT/WAIT_UNTIL/INVARIANT
        private static readonly HashSet<string> _Ops = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
            { "==", "!=", ">=", "<=", ">", "<", "contains" };
        private static readonly HashSet<string> _StopKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "TIMEOUT", "AS", "AND", "OR", "ABORT" };

        // Returns (query, op, value, nextIdx-after-value).
        // nextIdx is the index of the first stop-keyword (or tokens.Length if none).
        // op == "" when no operator token is found (bool shorthand).
        private static (string q, string op, string v, int next) ParseQOV(string[] tokens, int start)
        {
            for (int i = start + 1; i < tokens.Length; i++)
            {
                if (!_Ops.Contains(tokens[i])) continue;
                var q = string.Join(" ", tokens, start, i - start);
                // Value is at least 1 token; stop-keywords checked from i+2 onward
                // so `ASSERT /x == TIMEOUT` correctly yields value="TIMEOUT"
                int vEnd = i + 2;
                while (vEnd < tokens.Length && !_StopKeywords.Contains(tokens[vEnd].ToUpperInvariant()))
                    vEnd++;
                var v = vEnd > i + 1 ? string.Join(" ", tokens, i + 1, vEnd - i - 1) : "";
                return (q, tokens[i], v, vEnd);
            }
            // No operator found — bool shorthand (path only)
            return (string.Join(" ", tokens, start, tokens.Length - start), "", "", tokens.Length);
        }

        public static bool Compare(string actual, string op, string expected)
        {
            op = op.ToLowerInvariant();
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
