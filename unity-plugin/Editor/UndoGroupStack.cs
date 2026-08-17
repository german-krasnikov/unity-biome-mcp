// Session-global stack of per-turn Undo group IDs for MCP undo_last command.
// Chat assembly pushes group IDs here after each turn; CommandRouter pops + reverts via MCP.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    public static class UndoGroupStack
    {
        private static readonly List<(int groupId, int mutations)> _groups = new List<(int, int)>(16);

        // Replaceable in tests to avoid calling Unity Undo API.
        internal static Action<int> RevertAction = UndoGroupHelper.RevertToBeforeGroup;

        public static void Push(int groupId, int mutations = 0) =>
            _groups.Add((groupId, mutations));

        public static void Clear() => _groups.Clear();

        public static string RevertLast(int count = 1)
        {
            count = Math.Min(count, _groups.Count);
            if (count <= 0) return "nothing to undo";
            int totalMutations = 0;
            for (int i = _groups.Count - 1; i >= _groups.Count - count; i--)
            {
                RevertAction(_groups[i].groupId);
                totalMutations += _groups[i].mutations;
            }
            _groups.RemoveRange(_groups.Count - count, count);
            return $"reverted {count} turn(s) ({totalMutations} mutation(s))";
        }

        // For tests: expose count without leaking internals.
        internal static int Count => _groups.Count;
    }
}
