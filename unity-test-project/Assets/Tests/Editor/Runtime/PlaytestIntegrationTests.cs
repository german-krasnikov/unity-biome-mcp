using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject.Runtime
{
    /// <summary>
    /// Integration tests for PlaytestRunner DSL execution.
    /// Uses TestPlayableAPI as a fake game to verify the full chain:
    /// Parse DSL → Resolve queries → Execute via reflection → Verify state.
    /// </summary>
    [TestFixture]
    public class PlaytestIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _player;
        private TestPlayableAPI _api;
        private PlaytestConfig _config;

        static PlaytestConfig CreateConfig()
        {
            var c = ScriptableObject.CreateInstance<PlaytestConfig>();
            if (c.aliases == null) c.aliases = new List<QueryAlias>();
            c.aliases.Add(new QueryAlias { alias = "HP", path = "TestPlayer", component = "TestPlayableAPI", field = "health" });
            c.aliases.Add(new QueryAlias { alias = "Money", path = "TestPlayer", component = "TestPlayableAPI", field = "money" });
            c.aliases.Add(new QueryAlias { alias = "Cargo", path = "TestPlayer", component = "TestPlayableAPI", field = "cargoCount" });
            c.aliases.Add(new QueryAlias { alias = "Alive", path = "TestPlayer", component = "TestPlayableAPI", field = "isAlive" });
            c.aliases.Add(new QueryAlias { alias = "Name", path = "TestPlayer", component = "TestPlayableAPI", field = "playerName" });
            return c;
        }

        [SetUp]
        public void SetUp()
        {
            _player = TrackOwnedObject(new GameObject("TestPlayer"));
            _api = _player.AddComponent<TestPlayableAPI>();
            _api.health = 100f;
            _api.money = 0f;
            _api.cargoCount = 0;
            _config = TrackOwnedObject(CreateConfig());
        }

        // ─── ReadValue: field + method fallback ───

        [Test]
        public void ReadValue_Field_ReturnsValue()
        {
            var result = PlaytestRunner.ReadValue("TestPlayer", "TestPlayableAPI", "health");
            Assert.That(result, Is.EqualTo("100"));
        }

        [Test]
        public void ReadValue_Method_FallbackWorks()
        {
            _api.money = 42.5f;
            var result = PlaytestRunner.ReadValue("TestPlayer", "TestPlayableAPI", "GetMoney");
            Assert.That(result, Is.EqualTo("42.5"));
        }

        [Test]
        public void ReadValue_StringField()
        {
            var result = PlaytestRunner.ReadValue("TestPlayer", "TestPlayableAPI", "playerName");
            Assert.That(result, Is.EqualTo("TestPlayer"));
        }

        // ─── ASSERT: pass and fail ───

        [Test]
        public void Assert_PassingCondition_ReportsPASS()
        {
            var step = PlaytestParser.Parse("ASSERT HP == 100")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(results[0], Does.Contain("PASS"));
        }

        [Test]
        public void Assert_FailingCondition_ReportsFAIL()
        {
            var step = PlaytestParser.Parse("ASSERT HP == 50")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(0));
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("FAIL"));
        }

        [Test]
        public void Assert_GreaterThan_Works()
        {
            _api.money = 250f;
            var step = PlaytestParser.Parse("ASSERT Money > 200")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("PASS").And.Contain("250"));
        }

        [Test]
        public void Assert_BoolField_Works()
        {
            var step = PlaytestParser.Parse("ASSERT Alive == True")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
        }

        [Test]
        public void Assert_StringContains_Works()
        {
            var step = PlaytestParser.Parse("ASSERT Name contains Test")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
        }

        [Test]
        public void Assert_NonExistentQuery_ReportsERR()
        {
            var step = PlaytestParser.Parse("ASSERT FakeAlias == 0")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            // FakeAlias resolves to path="FakeAlias", no component → error
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("ERR"));
        }

        // ─── INVOKE: call methods, verify side effects ───

        [Test]
        public void Invoke_AddMoney_ChangesState()
        {
            Assert.That(_api.money, Is.EqualTo(0f));
            var step = PlaytestParser.Parse("INVOKE TestPlayer TestPlayableAPI AddMoney 100")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(_api.money, Is.EqualTo(100f));
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("INVOKE AddMoney"));
        }

        [Test]
        public void Invoke_TakeDamage_ReducesHealth()
        {
            var step = PlaytestParser.Parse("INVOKE TestPlayer TestPlayableAPI TakeDamage 30")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(_api.health, Is.EqualTo(70f));
        }

        [Test]
        public void Invoke_ThenAssert_FullChain()
        {
            // INVOKE adds money, then ASSERT checks it
            var steps = PlaytestParser.Parse("INVOKE TestPlayer TestPlayableAPI AddMoney 500\nASSERT Money >= 500");
            var results = new List<string>();
            int passed = 0, failed = 0;
            foreach (var step in steps)
                PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, steps.IndexOf(step));
            Assert.That(passed, Is.EqualTo(2));
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(_api.money, Is.EqualTo(500f));
        }

        // ─── SET: change fields via reflection ───

        [Test]
        public void Set_FieldValue_ChangesState()
        {
            var step = PlaytestParser.Parse("SET TestPlayer TestPlayableAPI health 50")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(_api.health, Is.EqualTo(50f));
            Assert.That(passed, Is.EqualTo(1));
        }

        [Test]
        public void Set_ThenAssert_FullChain()
        {
            var steps = PlaytestParser.Parse(
                "SET TestPlayer TestPlayableAPI money 999\nASSERT Money == 999");
            var results = new List<string>();
            int passed = 0, failed = 0;
            foreach (var step in steps)
                PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, steps.IndexOf(step));
            Assert.That(passed, Is.EqualTo(2));
            Assert.That(_api.money, Is.EqualTo(999f));
        }

        // ─── SNAPSHOT: multi-field read ───

        [Test]
        public void Snapshot_ReturnsAllValues()
        {
            _api.health = 80f;
            _api.money = 300f;
            _api.cargoCount = 5;
            // SNAPSHOT uses pipe format directly (aliases resolve in ResolveQuery for SNAPSHOT queries)
            var step = PlaytestParser.Parse(
                "SNAPSHOT TestPlayer|TestPlayableAPI|health, TestPlayer|TestPlayableAPI|money")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(results[0], Does.Contain("80"));
            Assert.That(results[0], Does.Contain("300"));
        }

        // ─── LOG ───

        [Test]
        public void Log_AddsMessageToReport()
        {
            var step = PlaytestParser.Parse("LOG Test checkpoint reached")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(results[0], Does.Contain("LOG Test checkpoint reached"));
            Assert.That(passed, Is.EqualTo(1));
        }

        // ─── ASSERT_CONSOLE_CLEAN ───

        [Test]
        public void AssertConsoleClean_ExecutesWithoutCrash()
        {
            // Console may have errors from other tests (shared state), so we just verify it runs
            var step = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("ASSERT_CONSOLE_CLEAN"));
        }

        // ─── Multi-step scenario: full gameplay chain ───

        [Test]
        public void FullScenario_InvokeSetAssert_AllPass()
        {
            var script = @"
# Full gameplay test scenario
INVOKE TestPlayer TestPlayableAPI AddMoney 300
ASSERT Money == 300
INVOKE TestPlayer TestPlayableAPI AddCargo 10
ASSERT Cargo > 5
INVOKE TestPlayer TestPlayableAPI TakeDamage 20
ASSERT HP == 80
ASSERT Alive == True
LOG Scenario complete
";
            var steps = PlaytestParser.Parse(script);
            var results = new List<string>();
            int passed = 0, failed = 0;
            for (int i = 0; i < steps.Count; i++)
                PlaytestRunner.ExecuteSyncStep(steps[i], _config, results, ref passed, ref failed, i);

            Assert.That(passed, Is.EqualTo(steps.Count), $"Not all passed:\n{string.Join("\n", results)}");
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(_api.money, Is.EqualTo(300f));
            Assert.That(_api.cargoCount, Is.EqualTo(10));
            Assert.That(_api.health, Is.EqualTo(80f));
        }

        [Test]
        public void FullScenario_WithFailure_ReportsCorrectly()
        {
            var script = @"
INVOKE TestPlayer TestPlayableAPI AddMoney 100
ASSERT Money == 100
ASSERT Money > 500
ASSERT HP == 100
";
            var steps = PlaytestParser.Parse(script);
            var results = new List<string>();
            int passed = 0, failed = 0;
            for (int i = 0; i < steps.Count; i++)
                PlaytestRunner.ExecuteSyncStep(steps[i], _config, results, ref passed, ref failed, i);

            Assert.That(passed, Is.EqualTo(3)); // AddMoney ok, Money==100 ok, HP==100 ok
            Assert.That(failed, Is.EqualTo(1)); // Money>500 fails
            Assert.That(results[2], Does.Contain("FAIL")); // 3rd step (index 2)
        }

        // ─── Alias resolution in full chain ───

        [Test]
        public void AliasResolution_WorksInAssert()
        {
            _api.cargoCount = 42;
            var step = PlaytestParser.Parse("ASSERT Cargo == 42")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("42"));
        }

        // ─── TIMESCALE ───

        [Test]
        public void TimeScale_SetsAndRestores()
        {
            var step = PlaytestParser.Parse("TIMESCALE 5")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(Time.timeScale, Is.EqualTo(5f));
            Assert.That(results[0], Does.Contain("TIMESCALE 5"));
            Time.timeScale = 1f; // cleanup
        }

        // ─── TELEPORT ───

        [Test]
        public void Teleport_SetsPosition()
        {
            var go = new GameObject("TeleportTarget");
            go.transform.position = Vector3.zero;
            var step = PlaytestParser.Parse("TELEPORT TeleportTarget 5,0,3")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(go.transform.position, Is.EqualTo(new Vector3(5, 0, 3)));
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("TELEPORT"));
            Object.DestroyImmediate(go);
        }

        // ─── ASSERT_NEAR ───

        [Test]
        public void AssertNear_Pass_ObjectsClose()
        {
            var goA = new GameObject("NearA");
            var goB = new GameObject("NearB");
            goA.transform.position = new Vector3(0, 0, 0);
            goB.transform.position = new Vector3(1, 0, 0); // dist=1, threshold=2
            var step = PlaytestParser.Parse("ASSERT_NEAR NearA NearB 2.0")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("PASS"));
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        [Test]
        public void AssertNear_Fail_ObjectsFar()
        {
            var goA = new GameObject("FarA");
            var goB = new GameObject("FarB");
            goA.transform.position = new Vector3(0, 0, 0);
            goB.transform.position = new Vector3(10, 0, 0); // dist=10, threshold=2
            var step = PlaytestParser.Parse("ASSERT_NEAR FarA FarB 2.0")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("FAIL"));
            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
        }

        // ─── ASSERT_BATCH ───

        [Test]
        public void AssertBatch_AllPass()
        {
            _api.health = 100f;
            _api.money = 200f;
            _api.cargoCount = 5;
            var script = "ASSERT_BATCH\nASSERT HP == 100\nASSERT Money == 200\nASSERT Cargo == 5\nEND";
            var step = PlaytestParser.Parse(script)[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(results[0], Does.Contain("3/3"));
        }

        [Test]
        public void AssertBatch_MixedResults()
        {
            _api.health = 100f;
            _api.money = 50f; // will fail
            _api.cargoCount = 5;
            var script = "ASSERT_BATCH\nASSERT HP == 100\nASSERT Money == 200\nASSERT Cargo == 5\nEND";
            var step = PlaytestParser.Parse(script)[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, _config, results, ref passed, ref failed, 0);
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(results[0], Does.Contain("2/3"));
            Assert.That(results[0], Does.Contain("FAIL"));
        }

        // ─── CAPTURE + ASSERT_CAPTURED integration ───

        [Test]
        public void Capture_ThenAssertCaptured_Increased()
        {
            _api.money = 100f;
            var script = "CAPTURE money Money\nINVOKE TestPlayer TestPlayableAPI AddMoney 200\nASSERT_CAPTURED money INCREASED";
            var steps = PlaytestParser.Parse(script);
            var results = new List<string>();
            int passed = 0, failed = 0;
            var state = new PlaytestState();
            for (int i = 0; i < steps.Count; i++)
                PlaytestRunner.ExecuteSyncStep(steps[i], _config, results, ref passed, ref failed, i, state);
            Assert.That(passed, Is.EqualTo(3), $"Steps:\n{string.Join("\n", results)}");
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(results[2], Does.Contain("PASS"));
        }
    }

    /// <summary>
    /// Tests for SimulatorRegistry, PlaytestMonitorRegistry, ASSERT_CTA and SIMULATE/MONITOR DSL execution.
    /// </summary>
    [TestFixture]
    public class SimulateMonitorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _player = TrackOwnedObject(new GameObject("TestPlayer"));
            _player.AddComponent<TestPlayableAPI>();
        }

        // ─── SimulatorRegistry ───

        [Test]
        public void SimulatorRegistry_UnknownName_Throws()
        {
            var args = new SimulatorArgs { Duration = 5f };
            Assert.Throws<System.ArgumentException>(() => SimulatorRegistry.Create("nonexistent_sim", args));
        }

        // ─── PlaytestMonitorRegistry ───

        [Test]
        public void MonitorRegistry_UnknownName_ReturnsError()
        {
            var result = PlaytestMonitorRegistry.Start("nonexistent_monitor");
            Assert.That(result, Does.Contain("not found").IgnoreCase.Or.Contain("unknown").IgnoreCase);
        }

        [Test]
        public void MonitorRegistry_StopAll_ClearsActive()
        {
            // Should not throw even when nothing is running
            Assert.DoesNotThrow(() => PlaytestMonitorRegistry.StopAll());
        }

        // ─── ASSERT_CTA integration ───

        [Test]
        public void AssertCta_ActiveGO_Passes()
        {
            // Use name-based CTA detection (no tag registration needed)
            var cta = TrackOwnedObject(new GameObject("CTA_Button"));
            cta.SetActive(true);

            var step = PlaytestParser.Parse("ASSERT_CTA VISIBLE")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.That(passed, Is.EqualTo(1), $"Expected PASS: {string.Join(", ", results)}");
            Assert.That(results[0], Does.Contain("PASS"));

        }

        [Test]
        public void AssertCta_InactiveGO_Fails()
        {
            // Name starts with "CTA" — runner finds it by name prefix
            var cta = TrackOwnedObject(new GameObject("CTAButton"));
            cta.SetActive(false);

            var step = PlaytestParser.Parse("ASSERT_CTA VISIBLE")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.That(failed, Is.EqualTo(1), $"Expected FAIL: {string.Join(", ", results)}");
            Assert.That(results[0], Does.Contain("FAIL"));

        }

        // ─── SIMULATE graceful error ───

        [Test]
        public void Simulate_UnknownSimulator_ReportsError()
        {
            var step = PlaytestParser.Parse("SIMULATE unknown_sim DURATION 5")[0];
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.That(failed, Is.EqualTo(1), $"Expected failure: {string.Join(", ", results)}");
            Assert.That(results[0], Does.Contain("ERR").Or.Contain("not found").IgnoreCase);
        }
    }
}
