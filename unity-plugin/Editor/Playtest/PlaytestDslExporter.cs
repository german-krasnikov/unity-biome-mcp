// Pure static DSL builder — no Unity API calls, fully NUnit-testable.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class PlaytestDslExporter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static readonly IReadOnlyList<StepType> SelectableTypes = new[]
        {
            StepType.Move, StepType.Teleport, StepType.Wait, StepType.WaitUntil,
            StepType.Assert, StepType.AssertConsoleClean, StepType.Log, StepType.Section,
            StepType.Invoke, StepType.TimeScale, StepType.Monitor, StepType.Set,
            StepType.Click, StepType.Invariant, StepType.Capture,
            StepType.AssertCaptured, StepType.AssertNear,
        };

        /// <summary>Build a full DSL script from a list of visual steps.</summary>
        public static string Export(List<VisualStep> steps, bool globalAbort)
        {
            var sb = new StringBuilder();
            if (globalAbort) sb.AppendLine("ABORT_ON_FAIL");
            foreach (var step in steps)
            {
                if (!IsSupportedType(step.type) && string.IsNullOrEmpty(step.rawLine)) continue;
                sb.AppendLine(StepToDsl(step));
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>Convert one VisualStep to its DSL line(s). Prepends DESC if description set.</summary>
        public static string StepToDsl(VisualStep step)
        {
            var prefix = !string.IsNullOrEmpty(step.description) ? $"DESC {step.description}\n" : "";
            return prefix + StepLine(step);
        }

        static string StepLine(VisualStep s) => s.type switch
        {
            StepType.Move         => string.IsNullOrEmpty(s.path)
                                         ? $"MOVE TO {Vec(s.position)}"
                                         : $"MOVE {s.path} TO {Vec(s.position)}",
            StepType.Teleport     => $"TELEPORT {s.path} {Vec(s.position)}",
            StepType.Wait         => $"WAIT {F(s.delay)}",
            StepType.WaitUntil    => WaitUntilLine(s),
            StepType.Assert       => $"ASSERT {s.query} {s.op} {QuoteIfSpace(s.value)}",
            StepType.AssertConsoleClean => string.IsNullOrEmpty(s.message)
                                               ? "ASSERT_CONSOLE_CLEAN"
                                               : $"ASSERT_CONSOLE_CLEAN IGNORE \"{s.message}\"",
            StepType.Log          => $"LOG {s.message}",
            StepType.Section      => $"SECTION \"{s.message}\"",
            StepType.Invoke       => InvokeLine(s),
            StepType.TimeScale    => $"TIMESCALE {F(s.delay)}",
            StepType.Monitor      => $"MONITOR {s.query}",
            StepType.Set          => $"SET {s.path} {s.component} {s.method} {s.args}",
            StepType.Click        => s.delay > 0
                                         ? $"CLICK {s.path} WAIT {F(s.delay)}"
                                         : $"CLICK {s.path}",
            StepType.Invariant    => $"INVARIANT {s.query} {s.op} {QuoteIfSpace(s.value)}",
            StepType.Capture      => $"CAPTURE {s.message} {s.query}",
            StepType.AssertCaptured => AssertCapturedLine(s),
            StepType.AssertNear   => $"ASSERT_NEAR {s.path} {s.value} {F(s.delay)}",
            _                     => s.rawLine
        };

        static string AssertCapturedLine(VisualStep s)
        {
            var line = $"ASSERT_CAPTURED {s.message} {s.op}";
            if (!string.IsNullOrEmpty(s.args))  line += $" {s.args}";
            if (!string.IsNullOrEmpty(s.value)) line += $" {s.value}";
            return line;
        }

        static string WaitUntilLine(VisualStep s)
        {
            var line = $"WAIT_UNTIL {s.query} {s.op} {QuoteIfSpace(s.value)} TIMEOUT {F(s.timeout)}";
            return s.abortOnFail ? line + " ABORT" : line;
        }

        static string QuoteIfSpace(string v) => v != null && v.Contains(' ') ? $"\"{v}\"" : v;

        static string InvokeLine(VisualStep s)
        {
            var line = $"INVOKE {s.path} {s.component} {s.method}";
            return string.IsNullOrEmpty(s.args) ? line : line + $" {s.args}";
        }

        static string Vec(Vector3 v) => $"{F(v.x)},{F(v.y)},{F(v.z)}";
        static string F(float v)     => v.ToString("G", Inv);

        /// <summary>Convert a parsed PlaytestStep back to a VisualStep (for Load roundtrip).</summary>
        public static VisualStep FromParsed(PlaytestStep p)
        {
            if (!IsSupportedType(p.Type))
            {
                var copy = p.ShallowClone();
                var source = (p.RawLine ?? "").Trim();
                if (PlaytestParser.SigilRegex.IsMatch(source) &&
                    !string.IsNullOrEmpty(p.ExpandedRawLine) &&
                    !string.Equals(source, p.ExpandedRawLine, StringComparison.Ordinal))
                {
                    // VAL/PATH_PREFIX directives are parse-time metadata, not visual
                    // steps. Preserve semantics by exporting their resolved line.
                    copy.RawLine = p.ExpandedRawLine;
                }
                return new VisualStep(copy);
            }
            return new VisualStep(p);
        }

        internal static bool IsSupportedType(StepType t) => SelectableTypes.Contains(t);
    }
}
