using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Animation
{
    [TestFixture]
    public class ParticleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("PSTest"));
            _go.AddComponent<ParticleSystem>();
        }

        // --- GET tests ---

        [Test]
        public void Particle_Get_Overview_ReturnsModuleList()
        {
            var json = "{\"id\":\"p1\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PSTest\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("main:", result);
            StringAssert.Contains("emission:", result);
            StringAssert.Contains("shape:", result);
            StringAssert.Contains("renderer:", result);
        }

        [Test]
        public void Particle_Get_MainModule_ReturnsProperties()
        {
            var json = "{\"id\":\"p2\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PSTest\",\"module\":\"main\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("duration:", result);
            StringAssert.Contains("loop:", result);
            StringAssert.Contains("maxParticles:", result);
        }

        [Test]
        public void Particle_Get_EmissionModule_ReturnsRate()
        {
            var json = "{\"id\":\"p3\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PSTest\",\"module\":\"emission\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("rateOverTime:", result);
        }

        [Test]
        public void Particle_Get_InvalidPath_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VALIDATION"));
            var json = "{\"id\":\"p4\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/NonExistent\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void Particle_Get_NoParticleSystem_ReturnsError()
        {
            var plain = new GameObject("PlainObj");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VALIDATION"));
                var json = "{\"id\":\"p5\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PlainObj\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        // --- CREATE tests ---

        [Test]
        public void Particle_Create_CreatesGameObject()
        {
            var json = "{\"id\":\"p6\",\"cmd\":\"particle\",\"args\":{\"action\":\"create\",\"path\":\"\",\"name\":\"NewPS\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("NewPS", result);

            var created = GameObject.Find("NewPS");
            Assert.IsNotNull(created);
            TrackOwnedObject(created);
            Assert.IsNotNull(created.GetComponent<ParticleSystem>());
        }

        [Test]
        public void Particle_Create_WithPreset_AppliesPreset()
        {
            var json = "{\"id\":\"p7\",\"cmd\":\"particle\",\"args\":{\"action\":\"create\",\"path\":\"\",\"name\":\"PresetPS\",\"preset\":\"fire\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var created = GameObject.Find("PresetPS");
            Assert.IsNotNull(created);
            TrackOwnedObject(created);
            var ps = created.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps);
            // Fire preset should have gravity < 0 (rising)
            Assert.Less(ps.main.gravityModifier.constant, 0f);
        }

        // --- SET tests ---

        [Test]
        public void Particle_Set_MainStartSpeed_ChangesValue()
        {
            var json = "{\"id\":\"p8\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"main\",\"prop\":\"startSpeed\",\"value\":\"10\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("set:", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(10f, ps.main.startSpeed.constant, 0.01f);
        }

        [Test]
        public void Particle_Set_MainLoop_ChangesValue()
        {
            var json = "{\"id\":\"p9\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"main\",\"prop\":\"loop\",\"value\":\"false\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.IsFalse(ps.main.loop);
        }

        [Test]
        public void Particle_Set_MainMaxParticles_ChangesValue()
        {
            var json = "{\"id\":\"p10\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"main\",\"prop\":\"maxParticles\",\"value\":\"500\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(500, ps.main.maxParticles);
        }

        [Test]
        public void Particle_Set_EmissionRate_ChangesValue()
        {
            var json = "{\"id\":\"p11\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"emission\",\"prop\":\"rateOverTime\",\"value\":\"100\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(100f, ps.emission.rateOverTime.constant, 0.01f);
        }

        [Test]
        public void Particle_Set_ShapeType_ChangesValue()
        {
            var json = "{\"id\":\"p12\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"shape\",\"prop\":\"shapeType\",\"value\":\"Sphere\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(ParticleSystemShapeType.Sphere, ps.shape.shapeType);
        }

        [Test]
        public void Particle_Set_NoiseEnabled_EnablesModule()
        {
            var json = "{\"id\":\"p13\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"noise\",\"prop\":\"enabled\",\"value\":\"true\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.IsTrue(ps.noise.enabled);
        }

        // --- PRESET tests ---

        [Test]
        public void Particle_Preset_Smoke_AppliesCorrectSettings()
        {
            var json = "{\"id\":\"p14\",\"cmd\":\"particle\",\"args\":{\"action\":\"apply\",\"path\":\"/PSTest\",\"preset\":\"smoke\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("preset:", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.Less(ps.main.gravityModifier.constant, 0f); // smoke rises
            Assert.IsTrue(ps.noise.enabled);
        }

        [Test]
        public void Particle_Preset_Explosion_IsNotLooping()
        {
            var json = "{\"id\":\"p15\",\"cmd\":\"particle\",\"args\":{\"action\":\"apply\",\"path\":\"/PSTest\",\"preset\":\"explosion\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.IsFalse(ps.main.loop);
        }

        [Test]
        public void Particle_Preset_Rain_HasHighParticleCount()
        {
            var json = "{\"id\":\"p16\",\"cmd\":\"particle\",\"args\":{\"action\":\"apply\",\"path\":\"/PSTest\",\"preset\":\"rain\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.GreaterOrEqual(ps.main.maxParticles, 2000);
        }

        [Test]
        public void Particle_Preset_InvalidPreset_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VALIDATION"));
            var json = "{\"id\":\"p17\",\"cmd\":\"particle\",\"args\":{\"action\":\"apply\",\"path\":\"/PSTest\",\"preset\":\"invalid\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        // --- INVALID ACTION test ---

        [Test]
        public void Particle_InvalidAction_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VALIDATION"));
            var json = "{\"id\":\"p18\",\"cmd\":\"particle\",\"args\":{\"action\":\"invalid\",\"path\":\"/PSTest\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        // --- MinMaxCurve range test ---

        [Test]
        public void Particle_Set_StartSpeed_Range_SetsMinMax()
        {
            var json = "{\"id\":\"p19\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"main\",\"prop\":\"startSpeed\",\"value\":\"1,5\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            var speed = ps.main.startSpeed;
            Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, speed.mode);
            Assert.AreEqual(1f, speed.constantMin, 0.01f);
            Assert.AreEqual(5f, speed.constantMax, 0.01f);
        }

        // --- Get all module names ---

        [Test]
        public void Particle_Get_ShapeModule_ReturnsShapeType()
        {
            var json = "{\"id\":\"p20\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PSTest\",\"module\":\"shape\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("shapeType:", result);
        }

        [Test]
        public void Particle_Get_RendererModule_ReturnsRenderMode()
        {
            var json = "{\"id\":\"p21\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PSTest\",\"module\":\"renderer\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("renderMode:", result);
        }

        // --- Scenario 25: Module toggle tests ---

        [TestCase("colorOverLifetime")]
        [TestCase("trails")]
        [TestCase("sizeOverLifetime")]
        public void Particle_Module_Toggle(string module)
        {
            var ps = _go.GetComponent<ParticleSystem>();

            var jsonEnable = "{\"id\":\"t_tog\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"" + module + "\",\"prop\":\"enabled\",\"value\":\"true\"}}";
            var result = CommandRouter.Process(jsonEnable);
            StringAssert.Contains("\"ok\":true", result);

            var jsonDisable = "{\"id\":\"t_togb\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"" + module + "\",\"prop\":\"enabled\",\"value\":\"false\"}}";
            result = CommandRouter.Process(jsonDisable);
            StringAssert.Contains("\"ok\":true", result);
        }

        // --- Scenario 25: Workflow create→set→get ---

        [Test]
        public void Particle_Workflow_CreateFire_SetSpeed_VerifyViaGet()
        {
            // create
            var json = "{\"id\":\"t33\",\"cmd\":\"particle\",\"args\":{\"action\":\"create\",\"path\":\"\",\"name\":\"PS_Workflow\",\"preset\":\"fire\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            // set startSpeed=2
            json = "{\"id\":\"t33b\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PS_Workflow\",\"module\":\"main\",\"prop\":\"startSpeed\",\"value\":\"2\"}}";
            result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            // verify via get main
            json = "{\"id\":\"t33c\",\"cmd\":\"particle\",\"args\":{\"action\":\"get\",\"path\":\"/PS_Workflow\",\"module\":\"main\"}}";
            result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("startSpeed:", result);

            // verify directly
            var created = GameObject.Find("PS_Workflow");
            Assert.IsNotNull(created);
            TrackOwnedObject(created);
            var ps = created.GetComponent<ParticleSystem>();
            Assert.AreEqual(2f, ps.main.startSpeed.constant, 0.01f);
        }

        [Test]
        public void Particle_Set_StartColor_Hex()
        {
            var json = "{\"id\":\"t34\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"main\",\"prop\":\"startColor\",\"value\":\"#FF6600\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            var c = ps.main.startColor.color;
            Assert.AreEqual(1f, c.r, 0.01f);
            Assert.AreEqual(0.4f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
        }

        [Test]
        public void Particle_Set_Noise_Strength_And_Frequency()
        {
            var jsonStrength = "{\"id\":\"t35\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"noise\",\"prop\":\"strength\",\"value\":\"0.3\"}}";
            var result = CommandRouter.Process(jsonStrength);
            StringAssert.Contains("\"ok\":true", result);

            var jsonFreq = "{\"id\":\"t35b\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"noise\",\"prop\":\"frequency\",\"value\":\"2\"}}";
            result = CommandRouter.Process(jsonFreq);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(0.3f, ps.noise.strength.constant, 0.01f);
            Assert.AreEqual(2f, ps.noise.frequency, 0.01f);
        }

        // --- Scenario 25: Set on various modules ---

        [Test]
        public void Particle_Set_Shape_Radius()
        {
            var json = "{\"id\":\"t36\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"shape\",\"prop\":\"radius\",\"value\":\"1.5\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var ps = _go.GetComponent<ParticleSystem>();
            Assert.AreEqual(1.5f, ps.shape.radius, 0.01f);
        }

        [Test]
        public void Particle_Set_Renderer_RenderMode_Stretch()
        {
            var json = "{\"id\":\"t37\",\"cmd\":\"particle\",\"args\":{\"action\":\"set\",\"path\":\"/PSTest\",\"module\":\"renderer\",\"prop\":\"renderMode\",\"value\":\"Stretch\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var r = _go.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Stretch, r.renderMode);
        }
    }
}
