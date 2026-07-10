// TDD: RenderAnalyzer.Lights — lights, shadow_audit, probe_audit, light_optimize.
// RenderPipelineInspector — DetectPipeline, GetPropOrField.
// EditMode tests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RenderAnalyzerLightsTests : SceneTestBase
    {
        private readonly List<GameObject> _gos = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gos)
                if (go != null) Object.DestroyImmediate(go);
            _gos.Clear();
        }

        private Light MakeLight(LightType type, string name = null)
        {
            var go = new GameObject(name ?? type.ToString());
            _gos.Add(go);
            var l = go.AddComponent<Light>();
            l.type = type;
            return l;
        }

        private static string A(string action, string detail = "brief", string path = null) =>
            path == null
                ? $"{{\"action\":\"{action}\",\"detail\":\"{detail}\"}}"
                : $"{{\"action\":\"{action}\",\"detail\":\"{detail}\",\"path\":\"{path}\"}}";

        // ── Lights ───────────────────────────────────────────────────────────

        [Test]
        public void Lights_EmptyScene_ReturnsZero()
        {
            var result = RenderAnalyzer.Execute(A("lights"));
            StringAssert.Contains("LIGHTS:", result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void Lights_CountsByType()
        {
            MakeLight(LightType.Directional, "Sun");
            MakeLight(LightType.Point, "Point1");
            MakeLight(LightType.Spot, "Spot1");

            var result = RenderAnalyzer.Execute(A("lights"));
            StringAssert.Contains("dir=1", result);
        }

        [Test]
        public void Lights_BakeStatus_Breakdown()
        {
            var l = MakeLight(LightType.Point, "BakedLight");
            l.lightmapBakeType = LightmapBakeType.Baked;
            var result = RenderAnalyzer.Execute(A("lights"));
            StringAssert.Contains("baked=", result);
        }

        [Test]
        public void Lights_BriefUnder150Tokens()
        {
            MakeLight(LightType.Directional);
            var result = RenderAnalyzer.Execute(A("lights", "brief"));
            // 150 tokens ≈ 600 chars
            Assert.Less(result.Length, 600, $"Too long: {result.Length}\n{result}");
        }

        // ── ShadowAudit ──────────────────────────────────────────────────────

        [Test]
        public void ShadowAudit_NoIssues_CleanReport()
        {
            var result = RenderAnalyzer.Execute(A("shadow_audit"));
            StringAssert.Contains("SHADOW AUDIT:", result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void ShadowAudit_PathScoped()
        {
            var parent = new GameObject("Zone");
            _gos.Add(parent);
            MakeLight(LightType.Point, "ZoneLight").gameObject.transform.SetParent(parent.transform);
            var result = RenderAnalyzer.Execute(A("shadow_audit", "brief", "/Zone"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        // ── ProbeAudit ───────────────────────────────────────────────────────

        [Test]
        public void ProbeAudit_NoProbes()
        {
            var result = RenderAnalyzer.Execute(A("probe_audit"));
            StringAssert.Contains("PROBE AUDIT:", result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void ProbeAudit_CountsReflectionByMode()
        {
            var go = new GameObject("RefProbe");
            _gos.Add(go);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;

            var result = RenderAnalyzer.Execute(A("probe_audit"));
            StringAssert.Contains("reflection probes:", result);
        }

        [Test]
        public void ProbeAudit_LightProbeCount()
        {
            var go = new GameObject("LightProbeGroup");
            _gos.Add(go);
            var lpg = go.AddComponent<LightProbeGroup>();
            lpg.probePositions = new[] { Vector3.zero, Vector3.up, Vector3.right };

            var result = RenderAnalyzer.Execute(A("probe_audit"));
            StringAssert.Contains("light probes:", result);
        }

        // ── LightOptimize ────────────────────────────────────────────────────

        [Test]
        public void LightOptimize_NoIssues()
        {
            var result = RenderAnalyzer.Execute(A("light_optimize"));
            StringAssert.Contains("LIGHT OPTIMIZE:", result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void LightOptimize_BakeStatic_Detected()
        {
            MakeLight(LightType.Point, "RealTimePoint").lightmapBakeType = LightmapBakeType.Realtime;
            var result = RenderAnalyzer.Execute(A("light_optimize"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        // ── RenderPipelineInspector ──────────────────────────────────────────

        [Test]
        public void DetectPipeline_ReturnsKnownValue()
        {
            var pipeline = RenderPipelineInspector.DetectPipeline();
            Assert.IsNotNull(pipeline);
            Assert.That(new[] { "builtin", "urp", "hdrp", "custom" }, Has.Member(pipeline));
        }

        [Test]
        public void GetPropOrField_NullOnMissing()
        {
            var result = RenderPipelineInspector.GetPropOrField(
                typeof(object), new object(), "nonExistentProp", "nonExistentField");
            Assert.IsNull(result);
        }

        [Test]
        public void GetPropOrField_PublicPropertyFirst()
        {
            // Use a type with a known public property
            var result = RenderPipelineInspector.GetPropOrField(
                typeof(string), "hello", "Length", null);
            Assert.AreEqual(5, result);
        }
    }
}
