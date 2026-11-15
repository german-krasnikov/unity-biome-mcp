// Generated from dsl/data/servername.data-contract, dsl/data/servername.core-data
// source-sha256=25f7016f3657ad2eab6e41486c3cb42e62b6e0b5dd46ec3a693ffacec8f6b76f
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class ServerIdentity
    {
        public readonly string ServerName;
        public ServerIdentity(string serverName)
        {
            this.ServerName = serverName;
        }
    }

    public static class ServerIdentityRows
    {
        public static readonly ServerIdentity[] All =
        {
            new ServerIdentity(serverName: "unity-biome-mcp"),
        };
    }
}
