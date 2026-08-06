using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal class PlaytestState
    {
        // label → (query, raw string value, numeric value)
        readonly Dictionary<string, (string query, string raw, float value)> _captures = new();

        // ─── FrameSets (CAPTURE_FRAMES) ───
        readonly Dictionary<string, List<string>> _frameSets = new();

        public void InitFrames(string label) => _frameSets[label] = new List<string>();
        public void AddFrame(string label, string path) => _frameSets[label].Add(path);
        public int GetFrameCount(string label) => _frameSets.TryGetValue(label, out var l) ? l.Count : 0;
        public List<string> GetFrames(string label) => _frameSets.TryGetValue(label, out var l) ? l : null;

        // invariants: list of (query, op, expected, rawLine)
        readonly List<(string query, string op, string expected, string rawLine)> _invariants = new();
        public List<string> Violations { get; } = new();

        // conserved trackers: (queries, initialSum, rawLine, duration, startTime)
        readonly List<(string[] queries, float initialSum, string rawLine, float duration, double startTime)> _conserved = new();
        public List<string> ConservedViolations { get; } = new();

        // ─── Capture ───

        public void Capture(string label, string query, string rawValue, float floatValue)
            => _captures[label] = (query, rawValue, floatValue);

        public float GetCapturedValue(string label) => _captures[label].value;

        public string GetCapturedQuery(string label) => _captures[label].query;

        public string GetCapturedRaw(string label) => _captures[label].raw;

        public bool IsChanged(string label, string currentRaw)
            => !string.Equals(GetCapturedRaw(label), currentRaw, StringComparison.OrdinalIgnoreCase);

        // ─── AssertCaptured ───

        /// <summary>Evaluate ASSERT_CAPTURED. currentValue is already read.</summary>
        public bool EvaluateCaptured(string label, string mode, string subOp, string subValue, float currentValue)
        {
            var captured = GetCapturedValue(label);
            switch (mode.ToUpperInvariant())
            {
                case "INCREASED":  return currentValue > captured;
                case "DECREASED":  return currentValue < captured;
                case "UNCHANGED":  return Math.Abs(currentValue - captured) < 0.001f;
                case "INCREASED_BY":
                case "DECREASED_BY":
                    var delta = mode.ToUpperInvariant() == "INCREASED_BY"
                        ? currentValue - captured
                        : captured - currentValue;
                    return PlaytestParser.Compare(delta.ToString(CultureInfo.InvariantCulture), subOp, subValue);
                default:
                    throw new ArgumentException($"Unknown ASSERT_CAPTURED mode: {mode}");
            }
        }

        // ─── Invariant ───

        public void RegisterInvariant(string query, string op, string expected, string rawLine)
            => _invariants.Add((query, op, expected, rawLine));

        /// <summary>Check all invariants. readValue(query) → actual string value.</summary>
        public void CheckInvariants(PlaytestConfig config, int frameCount, Func<string, string> readValue)
        {
            foreach (var (query, op, expected, rawLine) in _invariants)
            {
                try
                {
                    var actual = readValue(query);
                    if (!PlaytestParser.Compare(actual, op, expected))
                        Violations.Add($"[frame {frameCount}] INVARIANT VIOLATED: {rawLine} (actual={actual})");
                }
                catch (Exception e)
                {
                    Violations.Add($"[frame {frameCount}] INVARIANT ERR: {rawLine} — {e.Message}");
                }
            }
        }

        // ─── AssertConserved ───

        public void StartConserved(string[] queries, float duration, PlaytestConfig config,
            Func<string, string> readValue = null, float? expectedSum = null)
        {
            float initialSum;
            if (expectedSum.HasValue)
            {
                initialSum = expectedSum.Value;
            }
            else
            {
                initialSum = 0f;
                if (readValue != null)
                {
                    foreach (var q in queries)
                    {
                        if (float.TryParse(readValue(q), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            initialSum += v;
                    }
                }
            }
            _conserved.Add((queries, initialSum, string.Join("+", queries), duration, EditorApplication.timeSinceStartup));
        }

        /// <summary>Check all conserved trackers. readValue(query) → actual string value.</summary>
        public void CheckConserved(PlaytestConfig config, Func<string, string> readValue)
        {
            for (int i = 0; i < _conserved.Count; i++)
            {
                var (queries, initialSum, rawLine, duration, startTime) = _conserved[i];
                if (duration > 0 && EditorApplication.timeSinceStartup - startTime < duration)
                    continue;
                try
                {
                    float currentSum = 0f;
                    foreach (var q in queries)
                    {
                        if (float.TryParse(readValue(q), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            currentSum += v;
                    }
                    if (Math.Abs(currentSum - initialSum) >= 0.001f)
                        ConservedViolations.Add($"ASSERT_CONSERVED VIOLATED: SUM({rawLine}) changed {initialSum} → {currentSum}");
                }
                catch (Exception e)
                {
                    ConservedViolations.Add($"ASSERT_CONSERVED ERR: {rawLine} — {e.Message}");
                }
            }
        }

        // ─── Report ───

        public string BuildReport()
        {
            if (Violations.Count == 0 && ConservedViolations.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var v in Violations) sb.AppendLine(v);
            foreach (var v in ConservedViolations) sb.AppendLine(v);
            return sb.ToString().TrimEnd();
        }
    }
}
