using System;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class UndoGroupHelper
    {
        private static int _currentGroup = -1;

        // T19: injectable seam so tests can spy on revert calls without real Undo stack.
        internal static Action<int> RevertToGroupAction = id => Undo.RevertAllDownToGroup(id);

        public static void BeginGroup(string name)
        {
            if (_currentGroup >= 0)
                Undo.CollapseUndoOperations(_currentGroup);

            Undo.SetCurrentGroupName(name);
            _currentGroup = Undo.GetCurrentGroup();
        }

        public static void EndGroup()
        {
            if (_currentGroup >= 0)
            {
                Undo.CollapseUndoOperations(_currentGroup);
                _currentGroup = -1;
            }
        }

        public static void SetCommandFallback(string command)
        {
            Undo.SetCurrentGroupName($"MCP: {command}");
        }

        // ── Per-turn / per-batch named group primitive (F6 / F27) ──────────────

        public static int OpenNamedGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }

        public static void CloseNamedGroup(int groupId)
        {
            Undo.CollapseUndoOperations(groupId);
        }

        public static void RevertToBeforeGroup(int groupId)
        {
            RevertToGroupAction(groupId);
        }

        /// <summary>True if groupId is a non-negative id. Does NOT verify the group still exists in Unity's undo stack (stale after domain reload).</summary>
        public static bool CanRevert(int groupId) => groupId >= 0;

        /// <summary>Read-only: current open undo group id (-1 if none open).</summary>
        public static int CurrentGroupId => _currentGroup;
    }
}
