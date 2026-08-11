// Myers O(ND) shortest-edit-script diff. Pure C#, no UnityEngine deps.
// CRLF normalised to LF before comparison. Files > 80 lines → TwoBlock (IsLargeFile).
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal enum DiffLineKind { Context = 0, Added = 1, Removed = 2 }

    internal readonly struct DiffLine
    {
        public readonly DiffLineKind Kind;
        public readonly string       Text;
        public DiffLine(DiffLineKind kind, string text) { Kind = kind; Text = text; }
    }

    internal readonly struct TextDiffResult
    {
        public readonly DiffLine[] Lines;
        public readonly bool       IsLargeFile;
        public readonly bool       AllContext;

        public TextDiffResult(DiffLine[] lines, bool allContext)
        {
            Lines       = lines;
            IsLargeFile = false;
            AllContext  = allContext;
        }

        // Sentinel for files that exceed the line threshold.
        public static readonly TextDiffResult TwoBlock =
            new TextDiffResult(Array.Empty<DiffLine>(), false, isLargeFile: true);

        private TextDiffResult(DiffLine[] lines, bool allContext, bool isLargeFile)
        {
            Lines       = lines;
            IsLargeFile = isLargeFile;
            AllContext  = allContext;
        }
    }

    internal static class TextDiffEngine
    {
        private const int LineThreshold = 80;

        public static TextDiffResult Compute(string oldText, string newText)
        {
            var a = Split(oldText);
            var b = Split(newText);

            if (a.Length > LineThreshold || b.Length > LineThreshold)
                return TextDiffResult.TwoBlock;

            var lines = Myers(a, b);
            bool allContext = true;
            foreach (var l in lines)
                if (l.Kind != DiffLineKind.Context) { allContext = false; break; }

            return new TextDiffResult(lines, allContext);
        }

        // ── Myers shortest edit script ──────────────────────────────────────────
        private static DiffLine[] Myers(string[] a, string[] b)
        {
            int n = a.Length, m = b.Length;
            if (n == 0 && m == 0) return Array.Empty<DiffLine>();

            int max = n + m;
            // v[k] stores the furthest x reached along diagonal k.
            // Offset by max so negative indices work.
            var v = new int[2 * max + 2];
            // Trace: snapshot of v after each edit distance d.
            var trace = new List<int[]>();

            for (int d = 0; d <= max; d++)
            {
                var snap = new int[2 * max + 2];
                Array.Copy(v, snap, v.Length);
                trace.Add(snap);

                for (int k = -d; k <= d; k += 2)
                {
                    int idx = k + max;
                    int x;
                    if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                        x = v[idx + 1];       // move down (insert)
                    else
                        x = v[idx - 1] + 1;  // move right (delete)

                    int y = x - k;
                    // Extend along diagonal (matching lines)
                    while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                    v[idx] = x;

                    if (x >= n && y >= m)
                    {
                        // Found shortest edit path; backtrack to build diff.
                        return Backtrack(trace, a, b, d, max);
                    }
                }
            }
            return Array.Empty<DiffLine>(); // unreachable
        }

        private static DiffLine[] Backtrack(List<int[]> trace, string[] a, string[] b, int d, int max)
        {
            var result = new List<DiffLine>();
            int x = a.Length, y = b.Length;

            for (int dist = d; dist > 0; dist--)
            {
                // trace[dist] = v BEFORE step dist = v AFTER step dist-1.
                // That is the correct "previous" state for reconstructing the move at step dist.
                var prev = trace[dist];
                int k    = x - y;
                int idx  = k + max;

                int prevK;
                if (k == -dist || (k != dist && prev[idx - 1] < prev[idx + 1]))
                    prevK = k + 1;   // came from down (insert)
                else
                    prevK = k - 1;   // came from right (delete)

                int prevX = prev[prevK + max];
                int prevY = prevX - prevK;

                // Diagonal snake (context lines, in reverse)
                while (x > prevX && y > prevY)
                {
                    result.Add(new DiffLine(DiffLineKind.Context, a[x - 1]));
                    x--; y--;
                }

                if (x == prevX)
                    result.Add(new DiffLine(DiffLineKind.Added, b[y - 1]));    // y moved: insertion
                else
                    result.Add(new DiffLine(DiffLineKind.Removed, a[x - 1]));  // x moved: deletion

                x = prevX; y = prevY;
            }

            // Remaining context at the top
            while (x > 0 && y > 0) { result.Add(new DiffLine(DiffLineKind.Context, a[x - 1])); x--; y--; }

            result.Reverse();
            return result.ToArray();
        }

        private static string[] Split(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Replace("\r\n", "\n").Split('\n');
        }
    }
}
