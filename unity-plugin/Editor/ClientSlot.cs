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

        // Mutable label — updated by set_client_label command after identification.
        internal volatile string Label;

        internal ClientSlot()
        {
            _entries = new ClientEntry[MaxClients];
            for (int i = 0; i < MaxClients; i++)
                _entries[i] = new ClientEntry();
        }

        private static void CloseClient(TcpClient client)
        {
            try { client.Client?.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.Linger,
                    new LingerOption(true, 0)); } catch { }
            try { client.Close(); } catch { }
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
            if (TryAdd(client, parentToken, out var index, out var generation, out var clientCts))
                return (index, generation, clientCts);
            throw new InvalidOperationException("Client slot capacity exceeded");
        }

        internal bool TryAdd(TcpClient client, CancellationToken parentToken,
            out int index, out long generation, out CancellationTokenSource clientCts)
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
                        index = i;
                        generation = gen;
                        clientCts = cts;
                        return true;
                    }
                }
                index = -1;
                generation = 0;
                clientCts = null;
                return false;
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
                    if (_entries[i].Client != null) CloseClient(_entries[i].Client);
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

        // Non-atomic snapshot: count may be stale by the time the caller acts on it.
        internal int CountActive()
        {
            lock (_lock)
            {
                int count = 0;
                for (int i = 0; i < MaxClients; i++)
                    if (IsEntryActive(_entries[i])) count++;
                return count;
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
                        CloseClient(c);
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
