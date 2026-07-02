using System;
using System.Net.Sockets;
using System.Threading;

namespace UnityMCP.Editor
{
    // Per-port client state (ring of up to 4 simultaneous clients).
    // Promoted from a private nested class inside MCPServer (Phase 2, M1) — pure
    // mechanical move, zero logic changes.
    internal sealed class ClientSlot
    {
        internal const int MaxClients = 4;

        private sealed class ClientEntry
        {
            internal volatile TcpClient Client;
            internal volatile CancellationTokenSource Cts;
            internal long Generation;  // Interlocked
        }

        private readonly ClientEntry[] _entries;
        private readonly object _lock = new object();

        internal ClientSlot()
        {
            _entries = new ClientEntry[MaxClients];
            for (int i = 0; i < MaxClients; i++)
                _entries[i] = new ClientEntry();
        }

        private static bool IsSocketAlive(TcpClient client)
        {
            try
            {
                var s = client.Client;
                if (s == null || !s.Connected) return false;
                if (s.Poll(0, SelectMode.SelectRead))
                    return s.Available > 0;
                return true;
            }
            catch { return false; }
        }

        // Returns (index, generation, clientCts) for the new entry
        internal (int index, long generation, CancellationTokenSource clientCts) Add(
            TcpClient client, CancellationToken parentToken)
        {
            lock (_lock)
            {
                // Evict dead connections before looking for a slot
                for (int i = 0; i < MaxClients; i++)
                {
                    var c = _entries[i].Client;
                    if (c != null && !IsSocketAlive(c))
                    {
                        try { _entries[i].Cts?.Cancel(); } catch { }
                        try { c.Close(); } catch { }
                        _entries[i].Client = null;
                        _entries[i].Cts = null;
                    }
                }
                // Find empty slot first
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
                // All full — evict entry 0 (oldest), shift down, add at end
                try { _entries[0].Cts?.Cancel(); } catch { }
                try { _entries[0].Client?.Close(); } catch { }
                for (int i = 0; i < MaxClients - 1; i++)
                {
                    _entries[i].Client = _entries[i + 1].Client;
                    _entries[i].Cts = _entries[i + 1].Cts;
                    _entries[i].Generation = _entries[i + 1].Generation;
                }
                var last = MaxClients - 1;
                var newCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
                var newGen = Interlocked.Increment(ref _entries[last].Generation);
                _entries[last].Client = client;
                _entries[last].Cts = newCts;
                return (last, newGen, newCts);
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
                        if (_entries[i].Client != null && IsSocketAlive(_entries[i].Client)) return true;
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
                    var c = _entries[i].Client;
                    if (c != null && !IsSocketAlive(c)) count++;
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
                    var c = _entries[i].Client;
                    if (c != null && !IsSocketAlive(c))
                    {
                        try { _entries[i].Cts?.Cancel(); } catch { }
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
