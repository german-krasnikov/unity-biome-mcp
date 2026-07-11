// Per-turn Undo rollback tracker (F6). Chat asmdef only.
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public sealed class TurnUndoTracker
    {
        private readonly struct TurnRecord
        {
            public readonly int    GroupId;
            public readonly string Label;
            public TurnRecord(int id, string label) { GroupId = id; Label = label; }
        }

        private readonly List<TurnRecord> _turns = new List<TurnRecord>();
        private int  _inflightGroupId = -1;
        private bool _turnInFlight;

        // No longer gates RestoreButton (uses turn index); kept for API compat.
        public int CurrentGeneration { get; private set; }

        public bool HasRestorableGroup => _turns.Count > 0;

        public int TurnCount => _turns.Count;

        // Exposes the in-flight group id so SaveStateBeforeReload can persist it (#12).
        // -1 when no turn is in flight.
        public int InflightGroupId => _inflightGroupId;

        public void OnTurnStart(string label)
        {
            CurrentGeneration++;
            _inflightGroupId = UndoGroupHelper.OpenNamedGroup("Chat: " + label);
            _turnInFlight    = true;
        }

        public void OnTurnEnd()
        {
            if (!_turnInFlight) return;
            UndoGroupHelper.CloseNamedGroup(_inflightGroupId);
            _turns.Add(new TurnRecord(_inflightGroupId, ""));
            UndoGroupStack.Push(_inflightGroupId);
            _inflightGroupId = -1;
            _turnInFlight    = false;
        }

        // Partial mutations are still restorable — treat same as OnTurnEnd.
        public void OnTurnFailed() => OnTurnEnd();

        public void RestoreLastTurn() => RestoreFromIndex(TurnCount - 1);

        /// <summary>
        /// Reverts turns [index..Count-1] in reverse order (cascade), then removes them.
        /// No-op if index is out of range.
        /// </summary>
        public void RestoreFromIndex(int index)
        {
            if (index < 0 || index >= _turns.Count) return;
            // Revert in reverse order: last turn first, down to index.
            for (int i = _turns.Count - 1; i >= index; i--)
                UndoGroupHelper.RevertToBeforeGroup(_turns[i].GroupId);
            _turns.RemoveRange(index, _turns.Count - index);
        }

        // Called on domain reload — in-memory group indices are stale.
        public void Invalidate()
        {
            _turns.Clear();
            UndoGroupStack.Clear();
            _inflightGroupId = -1;
            _turnInFlight    = false;
        }
    }
}
