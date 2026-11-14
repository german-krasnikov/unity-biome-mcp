// Bridges BackendKind (generated, dsl/data/backends.data-contract, UI-5/Q20) to the generated Backend
// rows. Get() mirrors a dictionary-or-null lookup rather than throwing, so an out-of-range BackendKind
// behaves like the old switch statements' default arm.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal static class BackendTable
    {
        private static readonly Dictionary<BackendKind, Backend> _byKind = Build();

        private static Dictionary<BackendKind, Backend> Build()
        {
            var map = new Dictionary<BackendKind, Backend>();
            foreach (var row in BackendRows.All)
                map[row.Kind] = row;
            return map;
        }

        internal static Backend Get(BackendKind kind) =>
            _byKind.TryGetValue(kind, out var row) ? row : null;
    }
}
