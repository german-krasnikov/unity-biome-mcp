using System.Collections.Generic;

namespace UnityMCP.Playtest.Core
{
    public static class StringDistance
    {
        /// <summary>Returns closest match within maxDist edits, or null.</summary>
        public static string ClosestMatch(string input, IEnumerable<string> candidates, int maxDist = 3)
        {
            if (string.IsNullOrEmpty(input)) return null;
            string best = null;
            int bestDist = maxDist + 1;
            foreach (var c in candidates)
            {
                var d = Levenshtein(input, c);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        public static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) row[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                int prev = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int temp = row[j];
                    row[j] = a[i - 1] == b[j - 1]
                        ? prev
                        : 1 + Min(prev, row[j], row[j - 1]);
                    prev = temp;
                }
            }
            return row[b.Length];
        }

        private static int Min(int a, int b, int c) =>
            a < b ? (a < c ? a : c) : (b < c ? b : c);
    }
}
