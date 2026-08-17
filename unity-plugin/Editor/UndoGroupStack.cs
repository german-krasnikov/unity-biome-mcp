// Session-global stack of per-turn Undo group IDs for MCP undo_last command.
// Chat assembly pushes group IDs here after each turn; CommandRouter pops + reverts via MCP.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    public static class UndoGroupStack
    {
        private static readonly List<(int groupId, int mutations, string[] assets)> _groups
            = new List<(int, int, string[])>(16);

        // Staged asset paths for the current in-flight operation; captured by next Push().
        private static readonly List<string> _staged = new List<string>(4);

        // Replaceable in tests to avoid calling Unity Undo API.
        internal static Action<int> RevertAction = UndoGroupHelper.RevertToBeforeGroup;

        /// <summary>Stage an asset path so Push() can warn when RevertLast() can't undo it.</summary>
        public static void StageAsset(string path) => _staged.Add(path);

        /// <summary>Clear the staged list without pushing a group (use on error paths).</summary>
        public static void ClearStaged() => _staged.Clear();

        public static void Push(int groupId, int mutations = 0)
        {
            var assets = _staged.Count > 0 ? _staged.ToArray() : Array.Empty<string>();
            _staged.Clear();
            _groups.Add((groupId, mutations, assets));
        }

        public static void Clear()
        {
            _groups.Clear();
            _staged.Clear();
        }

        public static string RevertLast(int count = 1)
        {
            count = Math.Min(count, _groups.Count);
            if (count <= 0) return "nothing to undo";
            int totalMutations = 0;
            var allAssets = new List<string>();
            for (int i = _groups.Count - 1; i >= _groups.Count - count; i--)
            {
                RevertAction(_groups[i].groupId);
                totalMutations += _groups[i].mutations;
                if (_groups[i].assets != null)
                    allAssets.AddRange(_groups[i].assets);
            }
            _groups.RemoveRange(_groups.Count - count, count);
            var result = $"reverted {count} turn(s) ({totalMutations} mutation(s))";
            if (allAssets.Count > 0)
                result += $"\nwarn: {allAssets.Count} asset file(s) not reverted: {string.Join(", ", allAssets)}";
            return result;
        }

        // For tests: expose counts without leaking internals.
        internal static int Count => _groups.Count;
        internal static int StagedCount => _staged.Count;
    }
}
