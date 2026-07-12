// NUnit tests for PlaytestDropHelper — reflection filtering and query formatting.
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestDropHelperTests : SceneTestBase
    {
        // Minimal user component for reflection tests
        class TestComp : MonoBehaviour
        {
            public float health = 100f;
            public int   ammo   = 30;
        }

        // Component with [SerializeField] private fields — for #11 tests
        class TestCompWithPrivate : MonoBehaviour
        {
            public float visible = 1f;
#pragma warning disable CS0414
            [SerializeField] private float serialized = 2f;
            private float hidden = 3f;
#pragma warning restore CS0414
        }

        [Test]
        public void GetFilteredFields_OnlyDeclaredOnUserType()
        {
            var fields = PlaytestDropHelper.GetFilteredFields(typeof(TestComp)).ToList();
            // Every returned field must be declared on TestComp itself, not a Unity base type
            Assert.IsTrue(fields.All(f => f.DeclaringType == typeof(TestComp)),
                "Expected only fields declared on TestComp");
            // Negative: inherited Unity field must be absent
            Assert.IsFalse(fields.Any(f => f.Name == "useGUILayout"),
                "useGUILayout (from MonoBehaviour) must be filtered out");
        }

        [Test]
        public void GetFilteredFields_IncludesPublicFields()
        {
            var fields = PlaytestDropHelper.GetFilteredFields(typeof(TestComp)).ToList();
            Assert.IsTrue(fields.Any(f => f.Name == "health"), "Expected 'health' field");
            Assert.IsTrue(fields.Any(f => f.Name == "ammo"),   "Expected 'ammo' field");
        }

        [Test]
        public void BuildQuery_FormatsPathCompField()
        {
            var q = PlaytestDropHelper.BuildQuery("/Player", "Health", "hp");
            Assert.AreEqual("/Player|Health|hp", q);
        }

        [Test]
        public void ApplyMember_InvokeContext_SetsPathCompMethod_NoParens()
        {
            var step = new VisualStep();
            PlaytestDropHelper.ApplyMember(step, StepType.Invoke, "/Enemy", "Fighter", "Attack()", null);
            Assert.AreEqual("/Enemy",   step.path);
            Assert.AreEqual("Fighter",  step.component);
            Assert.AreEqual("Attack",   step.method,
                "method must NOT contain trailing () — InvokeMethod matches by bare name");
        }

        [Test]
        public void ApplyMember_InvokeContext_NoParens_UnchangedMethod()
        {
            var step = new VisualStep();
            PlaytestDropHelper.ApplyMember(step, StepType.Invoke, "/Enemy", "Fighter", "Attack", null);
            Assert.AreEqual("Attack", step.method, "bare name already has no parens");
        }

        [Test]
        public void ApplyMember_AssertContext_SetsQueryFormat()
        {
            var go   = new GameObject("TestGO");
            var comp = go.AddComponent<TestComp>();
            try
            {
                var step = new VisualStep();
                PlaytestDropHelper.ApplyMember(step, StepType.Assert, "/TestGO", "TestComp", "health", comp);
                Assert.AreEqual("/TestGO|TestComp|health", step.query);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Tests the filtering logic used by ShowComponentPicker.
        [Test]
        public void ShowComponentPicker_FiltersBuiltinComponents()
        {
            var go = new GameObject("TestGO");
            try
            {
                go.AddComponent<TestComp>();
                var comps = PlaytestDropHelper.GetUserComponents(go);
                Assert.IsTrue(comps.Exists(c => c is TestComp), "TestComp must be included");
                Assert.IsFalse(comps.Exists(c => c is Transform), "Transform must be filtered out");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // #11: [SerializeField] private field is included
        [Test]
        public void GetFilteredFields_IncludesSerializeFieldPrivate()
        {
            var names = PlaytestDropHelper.GetFilteredFields(typeof(TestCompWithPrivate)).Select(f => f.Name).ToList();
            Assert.Contains("serialized", names, "[SerializeField] private must be included");
        }

        // #11: non-serialized private field is excluded
        [Test]
        public void GetFilteredFields_ExcludesNonSerializedPrivate()
        {
            var names = PlaytestDropHelper.GetFilteredFields(typeof(TestCompWithPrivate)).Select(f => f.Name).ToList();
            Assert.IsFalse(names.Contains("hidden"), "non-serialized private must be excluded");
        }

        // #11: works on user types with public fields
        [Test]
        public void GetFilteredFields_Light_IncludesPublicFields()
        {
            var fields = PlaytestDropHelper.GetFilteredFields(typeof(TestComp));
            Assert.IsTrue(fields.Any(), "TestComp should expose at least one field");
        }

        // #11: no returned field is declared on a Unity base type
        [Test]
        public void GetFilteredFields_ExcludesUnityBaseTypes()
        {
            var fields = PlaytestDropHelper.GetFilteredFields(typeof(TestComp)).ToList();
            foreach (var f in fields)
                Assert.IsFalse(PlaytestDropHelper._baseTypes.Contains(f.DeclaringType),
                    $"Field '{f.Name}' declared on base type {f.DeclaringType.Name}");
        }

        // #31: AttachDnD path must not hardcode scene prefix
        [Test]
        public void AttachDnD_Resolves_GameObjectPath()
        {
            var go = new GameObject("Mover");
            try
            {
                var path = ComponentSerializer.GetPath(go);
                Assert.IsFalse(path.Contains("SceneName:/"), "Must not hardcode scene prefix");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // #34: SmartDrop.CreateStep for Move fills position from transform
        [Test]
        public void SmartDrop_CreateStep_Move_FillsPosition()
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(5f, 0f, 3f);
            try
            {
                var step = PlaytestSmartDrop.CreateStep(go, StepType.Move);
                Assert.AreEqual(StepType.Move, step.type);
                Assert.AreEqual(new Vector3(5f, 0f, 3f), step.position);
                Assert.IsFalse(string.IsNullOrEmpty(step.path));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // #34: SmartDrop.CreateStep for Assert leaves position at zero
        [Test]
        public void SmartDrop_CreateStep_Assert_NoPosition()
        {
            var go = new GameObject("Enemy");
            try
            {
                var step = PlaytestSmartDrop.CreateStep(go, StepType.Assert);
                Assert.AreEqual(StepType.Assert, step.type);
                Assert.AreEqual(Vector3.zero, step.position);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- ApplyMember Set context ---
        [Test]
        public void ApplyMember_SetContext_SetsPathCompAndField()
        {
            var go   = new GameObject("TestGO");
            var comp = go.AddComponent<TestComp>();
            try
            {
                var step = new VisualStep();
                PlaytestDropHelper.ApplyMember(step, StepType.Set, "/TestGO", "TestComp", "health", comp);
                Assert.AreEqual("/TestGO",   step.path);
                Assert.AreEqual("TestComp",  step.component);
                Assert.AreEqual("health",    step.method, "field name must go in method slot");
                Assert.IsNotNull(step.args,  "args should be pre-filled from field value");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ApplyMember_SetContext_StripsTrailingParens()
        {
            var step = new VisualStep();
            // Method name "()" suffix must be stripped even in Set context
            PlaytestDropHelper.ApplyMember(step, StepType.Set, "/T", "Comp", "Value()", null);
            Assert.AreEqual("Value", step.method);
        }

        [Test]
        public void ApplyMember_SetContext_DoesNotSetQuery()
        {
            var step = new VisualStep { query = "original" };
            PlaytestDropHelper.ApplyMember(step, StepType.Set, "/T", "Comp", "field", null);
            Assert.AreEqual("original", step.query, "Set context must not overwrite query");
        }
    }
}
