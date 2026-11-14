using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace UnityMCP.Editor
{
    internal enum ClientActivityState { Active, Idle, Dormant, Closing }

    internal readonly struct ConnectionSnapshot
    {
        internal readonly int    Index;
        internal readonly long   Generation;
        internal readonly string Label;
        internal readonly string RemoteEndpoint;
        internal readonly DateTime ConnectedAt;
        internal readonly DateTime LastUsefulAt;
        internal readonly string LastCommand;
        internal readonly int    InFlightCount;
        internal readonly ClientActivityState State;
        internal readonly string SessionId;
        internal readonly string DisplayName;
        // PID of the Python bridge process (from client_hello "bridgePid" field).
        // Zero when the bridge hasn't sent client_hello yet.
        internal readonly int    BridgePid;

        internal ConnectionSnapshot(int index, long generation, string label,
            string remoteEndpoint, DateTime connectedAt, DateTime lastUsefulAt,
            string lastCommand, int inFlightCount, ClientActivityState state,
            string sessionId, string displayName, int bridgePid = 0)
        {
            Index = index;
            Generation = generation;
            Label = label;
            RemoteEndpoint = remoteEndpoint;
            ConnectedAt = connectedAt;
            LastUsefulAt = lastUsefulAt;
            LastCommand = lastCommand;
            InFlightCount = inFlightCount;
            State = state;
            SessionId = sessionId;
            DisplayName = displayName;
            BridgePid = bridgePid;
        }
    }

    // Per-port client state (fixed slots for up to MaxClients simultaneous clients).
    // Handler registration, not a competing socket read, owns connection liveness.
    internal sealed class ClientSlot
    {
        internal const int MaxClients = 8;

        // Connections idle for longer than 5 minutes are classified as Dormant.
        private static readonly long DormantThresholdTicks = TimeSpan.FromMinutes(5).Ticks;

        private sealed class ClientEntry
        {
            internal volatile TcpClient Client;
            internal volatile CancellationTokenSource Cts;
            internal long Generation;  // Interlocked

            // Per-entry metadata (T3, IMPL-phase2-ports.md)
            internal long ConnectedAtTicks;                // set in TryAdd under _lock
            internal volatile string RemoteEndpoint;
            internal volatile string Label;
            internal volatile string LastCommand;
            internal volatile string SessionId;
            internal volatile string LockToken;
            internal volatile string AgentId;
            internal volatile string DisplayName;
            internal volatile string ChatMode;
            internal int BridgePid;                        // set once in SetEntrySession; main-thread-read in snapshot
            internal long LastUsefulActivityTicks;         // Interlocked.Exchange
            internal int InFlightCount;                   // Volatile.Write/Read

            internal void BeginCommand(string cmd)
            {
                LastCommand = cmd;
                Volatile.Write(ref InFlightCount, 1);
            }

            internal void EndCommand()
            {
                Volatile.Write(ref InFlightCount, 0);
                Interlocked.Exchange(ref LastUsefulActivityTicks, DateTime.UtcNow.Ticks);
            }
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
                        // Initialize metadata fields on slot assignment.
                        _entries[i].ConnectedAtTicks = DateTime.UtcNow.Ticks;
                        _entries[i].RemoteEndpoint = null;
                        _entries[i].Label = null;
                        _entries[i].LastCommand = null;
                        _entries[i].SessionId = null;
                        _entries[i].LockToken = null;
                        _entries[i].AgentId = null;
                        _entries[i].DisplayName = null;
                        _entries[i].ChatMode = null;
                        _entries[i].BridgePid = 0;
                        Interlocked.Exchange(ref _entries[i].LastUsefulActivityTicks, 0);
                        Volatile.Write(ref _entries[i].InFlightCount, 0);
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

        // ── Per-entry metadata API (T3, IMPL-phase2-ports.md) ────────────────

        internal void SetEntryEndpoint(int index, long generation, string endpoint)
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            _entries[index].RemoteEndpoint = endpoint;
        }

        internal void SetEntryLabel(int index, long generation, string label)
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            _entries[index].Label = label;
        }

        internal void BeginCommand(int index, long generation, string cmd)
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            _entries[index].BeginCommand(cmd);
        }

        internal void EndCommand(int index, long generation)
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            _entries[index].EndCommand();
        }

        // Returns null when the entry is cleared (Client == null) or generation is stale.
        internal string GetEntryLabel(int index, long generation)
        {
            if (index < 0 || index >= MaxClients) return null;
            var entry = _entries[index];
            if (Interlocked.Read(ref entry.Generation) != generation) return null;
            if (entry.Client == null) return null;
            return entry.Label;
        }

        internal void SetEntrySession(int index, long generation, string sessionId,
            string lockToken, string agentId, string displayName, int bridgePid = 0,
            string chatMode = "")
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            _entries[index].SessionId = sessionId;
            _entries[index].LockToken = lockToken;
            _entries[index].AgentId = agentId;
            _entries[index].DisplayName = displayName;
            _entries[index].ChatMode = chatMode ?? "";
            _entries[index].BridgePid = bridgePid;
        }

        internal string GetEntryChatMode(int index, long generation)
        {
            if (index < 0 || index >= MaxClients) return "";
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return "";
            return _entries[index].ChatMode ?? "";
        }

        // Snapshot of all live entries with computed activity state.
        // State priority: Closing > Active > Dormant > Idle.
        internal ConnectionSnapshot[] TakeSnapshot()
        {
            lock (_lock)
            {
                var results = new List<ConnectionSnapshot>();
                var now = DateTime.UtcNow;
                for (int i = 0; i < MaxClients; i++)
                {
                    var entry = _entries[i];
                    if (entry.Client == null) continue;

                    var cts = entry.Cts;
                    var inFlight = Volatile.Read(ref entry.InFlightCount);
                    var lastUsefulTicks = Interlocked.Read(ref entry.LastUsefulActivityTicks);

                    ClientActivityState state;
                    if (cts != null && cts.IsCancellationRequested)
                        state = ClientActivityState.Closing;
                    else if (inFlight > 0)
                        state = ClientActivityState.Active;
                    else if (lastUsefulTicks != 0 &&
                             (now.Ticks - lastUsefulTicks) > DormantThresholdTicks)
                        state = ClientActivityState.Dormant;
                    else
                        state = ClientActivityState.Idle;

                    results.Add(new ConnectionSnapshot(
                        index: i,
                        generation: Interlocked.Read(ref entry.Generation),
                        label: entry.Label,
                        remoteEndpoint: entry.RemoteEndpoint,
                        connectedAt: new DateTime(entry.ConnectedAtTicks, DateTimeKind.Utc),
                        lastUsefulAt: lastUsefulTicks == 0
                            ? DateTime.MinValue
                            : new DateTime(lastUsefulTicks, DateTimeKind.Utc),
                        lastCommand: entry.LastCommand,
                        inFlightCount: inFlight,
                        state: state,
                        sessionId: entry.SessionId,
                        displayName: entry.DisplayName,
                        bridgePid: entry.BridgePid));
                }
                return results.ToArray();
            }
        }

        // Alias for UI consumers — same as TakeSnapshot().
        internal ConnectionSnapshot[] GetActiveSnapshots() => TakeSnapshot();

        // Cancel + close a specific entry without touching the OS process.
        // The handler's finally block will call Clear() after catching OperationCanceledException.
        internal void DisconnectEntry(int index, long generation)
        {
            lock (_lock)
            {
                if (index < 0 || index >= MaxClients) return;
                if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
                var c = _entries[index].Client;
                if (c == null) return;
                try { _entries[index].Cts?.Cancel(); } catch { }
                CloseClient(c);
            }
        }

        // Test seam: directly writes LastUsefulActivityTicks for Dormant state testing.
        internal void SetLastUsefulTicksForTest(int index, long generation, long ticks)
        {
            if (index < 0 || index >= MaxClients) return;
            if (Interlocked.Read(ref _entries[index].Generation) != generation) return;
            Interlocked.Exchange(ref _entries[index].LastUsefulActivityTicks, ticks);
        }
    }
}
