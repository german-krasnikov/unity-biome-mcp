using System;
using System.Net.Sockets;
using System.Threading;

namespace UnityMCP.Editor
{
    // Per-port client state (fixed slots for up to MaxClients simultaneous clients).
    // Handler registration, not a competing socket read, owns connection liveness.
    internal sealed class ClientSlot
    {
        internal const int MaxClients = 8;

        private sealed class ClientEntry
        {
            internal volatile TcpClient Client;
            internal volatile CancellationTokenSource Cts;
            internal long Generation;  // Interlocked
        }

        private readonly ClientEntry[] _entries;
        private readonly object _lock = new object();
        private int _nextReplacementIndex;

        // Mutable label — updated by set_client_label command after identification.
        internal volatile string Label;

        internal ClientSlot()
        {
            _entries = new ClientEntry[MaxClients];
            for (int i = 0; i < MaxClients; i++)
                _entries[i] = new ClientEntry();
        }

        private static bool IsEntryActive(ClientEntry entry)
        {
            var cts = entry.Cts;
            return entry.Client != null && cts != null && !cts.IsCancellationRequested;
        }

        // Returns (index, generation, clientCts) for the new entry
        internal (int index, long generation, CancellationTokenSource clientCts) Add(
            TcpClient client, CancellationToken parentToken)
        {
            lock (_lock)
            {
                // HandleClientAsync.finally exclusively releases normal entries. Probing
                // Poll/Available here races that handler's read and can misclassify a live
                // socket after it consumes the bytes observed by Poll.
                for (int i = 0; i < MaxClients; i++)
                {
                    if (_entries[i].Client == null)
                    {
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
                        var gen = Interlocked.Increment(ref _entries[i].Generation);
                        _entries[i].Client = client;
                        _entries[i].Cts = cts;
                        return (i, gen, cts);
                    }
                }
                // Capacity is the only eager-eviction boundary. Replace one entry in
                // place: handlers retain (index, generation), so shifting entries would
                // let an older handler's finally block clear a different live client.
                var replacementIndex = _nextReplacementIndex;
                _nextReplacementIndex = (_nextReplacementIndex + 1) % MaxClients;
                var replacement = _entries[replacementIndex];
                var replacedCts = replacement.Cts;
                try { replacedCts?.Cancel(); } catch { }
                try { replacement.Client?.Client?.SetSocketOption(
                        SocketOptionLevel.Socket, SocketOptionName.Linger,
                        new LingerOption(true, 0)); } catch { }
                try { replacement.Client?.Close(); } catch { }
                var newCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
                var newGen = Interlocked.Increment(ref replacement.Generation);
                replacement.Client = client;
                replacement.Cts = newCts;
                return (replacementIndex, newGen, newCts);
            }
        }

        // Called from handler's finally block — only clears if generation matches
        internal void Clear(int index, long generation)
        {
            lock (_lock)
            {
                if (index >= 0 && index < MaxClients &&
                    Interlocked.Read(ref _entries[index].Generation) == generation)
                {
                    _entries[index].Client = null;
                    _entries[index].Cts = null;
                }
            }
        }

        // Safe iteration over all connected clients (e.g. going_away broadcast)
        internal void ForEach(Action<TcpClient> action)
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxClients; i++)
                {
                    var c = _entries[i].Client;
                    if (c != null)
                        try { action(c); } catch { }
                }
            }
        }

        // Cancel + close all entries (teardown)
        internal void DisconnectAll()
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxClients; i++)
                {
                    try { _entries[i].Cts?.Cancel(); } catch { }
                    // Force RST instead of FIN — eliminates TIME_WAIT on Windows.
                    // ExclusiveAddressUse blocks port reuse during TIME_WAIT, causing port drift on reload.
                    // SendGoingAwaySync() is called BEFORE DisconnectAll() so Python gets the frame first.
                    try { _entries[i].Client?.Client?.SetSocketOption(
                            SocketOptionLevel.Socket, SocketOptionName.Linger,
                            new LingerOption(true, 0)); } catch { }
                    try { _entries[i].Client?.Close(); } catch { }
                    _entries[i].Client = null;
                    _entries[i].Cts = null;
                }
            }
        }

        internal bool AnyConnected
        {
            get
            {
                lock (_lock)
                {
                    for (int i = 0; i < MaxClients; i++)
                        if (IsEntryActive(_entries[i])) return true;
                    return false;
                }
            }
        }

        internal int CountPhantoms()
        {
            int count = 0;
            lock (_lock)
            {
                for (int i = 0; i < MaxClients; i++)
                {
                    if (_entries[i].Client != null && !IsEntryActive(_entries[i])) count++;
                }
            }
            return count;
        }

        internal int KillPhantoms()
        {
            int killed = 0;
            lock (_lock)
            {
                for (int i = 0; i < MaxClients; i++)
                {
                    var entry = _entries[i];
                    var c = entry.Client;
                    if (c != null && !IsEntryActive(entry))
                    {
                        try { _entries[i].Cts?.Cancel(); } catch { }
                        try { c.Client?.SetSocketOption(
                                SocketOptionLevel.Socket, SocketOptionName.Linger,
                                new LingerOption(true, 0)); } catch { }
                        try { c.Close(); } catch { }
                        _entries[i].Client = null;
                        _entries[i].Cts = null;
                        killed++;
                    }
                }
            }
            return killed;
        }
    }
}
