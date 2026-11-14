using System.Runtime.CompilerServices;

// MainThreadDispatcher and AtomicFile stay `internal` (Phase 4 Task B extraction out
// of UnityMCP.Editor) -- internal visibility is per-assembly, not per-namespace, so
// every assembly whose source directly calls either type needs an explicit grant here,
// even though both keep their original `namespace UnityMCP.Editor`.
[assembly: InternalsVisibleTo("UnityMCP.Editor")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard")]
