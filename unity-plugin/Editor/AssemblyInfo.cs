using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.View")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
// grants Chat.Tests access to JsonHelper parse helpers
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.View")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Worker.DomainReloadHarness")]
