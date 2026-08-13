// RED: Per-entry metadata tests for ClientSlot (T3, IMPL-phase2-ports.md).
// All 10 tests fail with compile errors until:
//   - ClientActivityState enum is added to ClientSlot.cs
//   - ConnectionSnapshot readonly struct is added to ClientSlot.cs
//   - ClientEntry gets metadata fields (ConnectedAtTicks, Label, InFlightCount, etc.)
//   - ClientSlot gets: SetEntryLabel, BeginCommand, EndCommand, GetEntryLabel,
//     TakeSnapshot, SetLastUsefulTicksForTest
using System;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClientSlotMetadataTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ConnectedAtTicks is set in TryAdd; snapshot exposes it as DateTime.
        [Test]
        public void TryAdd_SetsConnectedAt_CloseToUtcNow()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var before = DateTime.UtcNow;
            var handle = slot.Add(client, lifetime.Token);
            var after = DateTime.UtcNow;
            try
            {
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.That(snapshots[0].ConnectedAt, Is.GreaterThanOrEqualTo(before));
                Assert.That(snapshots[0].ConnectedAt, Is.LessThanOrEqualTo(after));
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // SetEntryLabel with matching generation stores the label per-entry.
        [Test]
        public void SetEntryLabel_MatchingGeneration_StoresPerEntry()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.SetEntryLabel(handle.index, handle.generation, "Claude Code session");
                Assert.AreEqual("Claude Code session",
                    slot.GetEntryLabel(handle.index, handle.generation));
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // SetEntryLabel with wrong generation is a no-op — original label is preserved.
        [Test]
        public void SetEntryLabel_StaleGeneration_Ignored()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.SetEntryLabel(handle.index, handle.generation, "Original");
                slot.SetEntryLabel(handle.index, handle.generation + 1, "Stale write");
                Assert.AreEqual("Original",
                    slot.GetEntryLabel(handle.index, handle.generation));
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // BeginCommand sets LastCommand and InFlightCount=1 in the snapshot.
        [Test]
        public void BeginCommand_SetsLastCommandAndInflight()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.BeginCommand(handle.index, handle.generation, "get_hierarchy");
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.AreEqual("get_hierarchy", snapshots[0].LastCommand);
                Assert.AreEqual(1, snapshots[0].InFlightCount);
            }
            finally
            {
                slot.EndCommand(handle.index, handle.generation);
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // EndCommand resets InFlightCount to 0 and records LastUsefulAt timestamp.
        [Test]
        public void EndCommand_DecrementsInflight_TouchesActivity()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.BeginCommand(handle.index, handle.generation, "set_property");
                var before = DateTime.UtcNow;
                slot.EndCommand(handle.index, handle.generation);
                var after = DateTime.UtcNow;
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.AreEqual(0, snapshots[0].InFlightCount);
                Assert.That(snapshots[0].LastUsefulAt, Is.GreaterThanOrEqualTo(before));
                Assert.That(snapshots[0].LastUsefulAt, Is.LessThanOrEqualTo(after));
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // Entry with InFlightCount > 0 gets Active state (highest priority after Closing).
        [Test]
        public void TakeSnapshot_ActiveEntry_HasActiveState()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.BeginCommand(handle.index, handle.generation, "run_tests");
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.AreEqual(ClientActivityState.Active, snapshots[0].State);
            }
            finally
            {
                slot.EndCommand(handle.index, handle.generation);
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // Entry with LastUsefulActivityTicks > DormantThreshold ago gets Dormant state.
        [Test]
        public void TakeSnapshot_OldLastUseful_DormantState()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                // 10 minutes in the past — well beyond the 5-minute dormant threshold.
                slot.SetLastUsefulTicksForTest(handle.index, handle.generation,
                    DateTime.UtcNow.AddMinutes(-10).Ticks);
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.AreEqual(ClientActivityState.Dormant, snapshots[0].State);
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // Cancelled CTS means the handler is shutting down — Closing state wins over all.
        [Test]
        public void TakeSnapshot_CtsCancelled_ClosingState()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                handle.clientCts.Cancel();
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(1, snapshots.Length);
                Assert.AreEqual(ClientActivityState.Closing, snapshots[0].State);
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // After Clear(), the entry has Client=null so TakeSnapshot excludes it.
        [Test]
        public void TakeSnapshot_AfterClear_EntryExcluded()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.SetEntryLabel(handle.index, handle.generation, "Before clear");
                slot.Clear(handle.index, handle.generation);
                var snapshots = slot.TakeSnapshot();
                Assert.AreEqual(0, snapshots.Length);
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        // GetEntryLabel returns the label while alive; null after Clear (Client becomes null).
        [Test]
        public void GetEntryLabel_BeforeClearReturnsLabel_AfterClearReturnsNull()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                slot.SetEntryLabel(handle.index, handle.generation, "test-label");
                Assert.AreEqual("test-label",
                    slot.GetEntryLabel(handle.index, handle.generation));
                slot.Clear(handle.index, handle.generation);
                Assert.IsNull(slot.GetEntryLabel(handle.index, handle.generation));
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }
    }
}
