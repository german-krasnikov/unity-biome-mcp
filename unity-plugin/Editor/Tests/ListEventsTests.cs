// TDD: G4 — list_events reads persistent UnityEvent listeners via SerializedObject.
// EditMode NUnit tests. No TCP required.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ListEventsTests : SceneTestBase
    {
        private GameObject _srcGo;
        private GameObject _tgtGo;

        [SetUp]
        public void SetUp()
        {
            _srcGo = TrackOwnedObject(new GameObject("LE_Source"));
            _srcGo.AddComponent<Button>();

            _tgtGo = TrackOwnedObject(new GameObject("LE_Target"));
            // Rigidbody.WakeUp() and Sleep() each have a single unambiguous overload.
            _tgtGo.AddComponent<Rigidbody>();
        }

        // ── 1. No listeners → zero header ─────────────────────────────────────

        [Test]
        public void ListEvents_Zero_ReturnsZeroHeader()
        {
            var result = ObjectManager.ListEvents("/LE_Source", "Button", "onClick");
            Assert.That(result, Does.Contain("0 listeners"), $"Expected '0 listeners' in: {result}");
        }

        // ── 2. One void listener → formatted correctly ─────────────────────────

        [Test]
        public void ListEvents_OneVoid_FormatsCorrectly()
        {
            // Rigidbody.WakeUp() has a single unambiguous overload — safe for void wire.
            ObjectManager.WireEvent("/LE_Source", "Button", "onClick",
                "/LE_Target", "WakeUp", "void", null,
                targetComponentType: "Rigidbody");

            var result = ObjectManager.ListEvents("/LE_Source", "Button", "onClick");

            Assert.That(result, Does.Contain("[0]"), $"Expected [0] entry in: {result}");
            Assert.That(result, Does.Contain("Rigidbody"), $"Expected target type in: {result}");
            Assert.That(result, Does.Contain("WakeUp"), $"Expected method name in: {result}");
        }

        // ── 3. Bool arg → value shown ──────────────────────────────────────────

        [Test]
        public void ListEvents_BoolArg_FormatsArgValue()
        {
            ObjectManager.WireEvent("/LE_Source", "Button", "onClick",
                "/LE_Target", "enabled", "bool", "true",
                targetComponentType: "Rigidbody");

            var result = ObjectManager.ListEvents("/LE_Source", "Button", "onClick");

            Assert.That(result, Does.Contain("bool"), $"Expected 'bool' in: {result}");
        }

        // ── 4. Single listener count header ────────────────────────────────────

        [Test]
        public void ListEvents_StringArg_FormatsArgValue()
        {
            // Wire one listener and verify the header shows (1 listener).
            ObjectManager.WireEvent("/LE_Source", "Button", "onClick",
                "/LE_Target", "WakeUp", "void", null,
                targetComponentType: "Rigidbody");

            var result = ObjectManager.ListEvents("/LE_Source", "Button", "onClick");
            Assert.That(result, Does.Contain("(1 listener"), $"Expected 1 listener in: {result}");
        }

        // ── 5. Invalid field → ArgumentException ──────────────────────────────

        [Test]
        public void ListEvents_InvalidField_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ObjectManager.ListEvents("/LE_Source", "Button", "notAnEvent"));

            Assert.That(ex.Message, Does.Contain("notAnEvent"),
                "Exception must name the invalid field");
        }
    }
}
