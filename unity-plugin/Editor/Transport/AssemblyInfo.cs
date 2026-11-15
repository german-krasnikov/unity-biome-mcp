using System.Runtime.CompilerServices;

// ClientSlot/ConnectionSnapshot/ClientActivityState/PortResolver/DormantInfo/DormantBridgeScanner
// stay `internal` (Phase 2.4, boundary-hardening) -- internal visibility is per-assembly, not
// per-namespace, so every assembly whose source directly calls these types needs an explicit
// grant here, even though they keep their original `namespace UnityMCP.Editor`.
// UnityMCP.Editor: MCPServer.cs (new ClientSlot(), PortResolver.*), ClientConnectionHandler.cs
// (ClientSlot parameter), PortFileManager.cs (PortResolver.*).
[assembly: InternalsVisibleTo("UnityMCP.Editor")]
// UnityMCP.Editor.Wizard: MCPStatusWindow.cs uses ConnectionSnapshot/DormantInfo/DormantBridgeScanner.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard")]
// UnityMCP.Editor.Tests: ClientSlotMetadataTests, PortResolverTests(+ReadOnly/Reload),
// DormantBridgeScannerTests, ConnectionStabilityTests, HttpGarbageProbeTests, MCPServerSplitTests,
// MCPServerStartGuardTests, PortFileManagerTests(+CachedPath), ClientConnectionHandlerDispatcherSeamTests.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
