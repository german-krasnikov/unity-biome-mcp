// Generated from dsl/data/transport.data-contract, dsl/data/transport.core-data
// source-sha256=76c7e5f2e0bbbc1f8c267f63dd263aa1903a1cad09b2f93c4a6f37b54d72c365
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class Transport
    {
        public readonly int MaxClients;
        public readonly int MainBindMaxAttempts;
        public readonly int ChatBindMaxAttempts;
        public Transport(int maxClients, int mainBindMaxAttempts, int chatBindMaxAttempts)
        {
            this.MaxClients = maxClients;
            this.MainBindMaxAttempts = mainBindMaxAttempts;
            this.ChatBindMaxAttempts = chatBindMaxAttempts;
        }
    }

    public static class TransportRows
    {
        public static readonly Transport[] All =
        {
            new Transport(maxClients: 8, mainBindMaxAttempts: 6, chatBindMaxAttempts: 3),
        };
    }
}
