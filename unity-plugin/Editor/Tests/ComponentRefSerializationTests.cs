// TDD — P-258: Component reference resolves to Transform instead of typed component.
// Regression: ComponentSerializer emitted the gameObject's instanceID for Component refs.
// Fix: emit component's own instanceID with ::TypeName suffix for typed resolution.
// P-258b: "path::TypeName" (no #id) — LLM-friendly bare type resolution without instanceID.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ComponentRefSerializationTests : SceneTestBase
    {
        // ── 1. GetComponentWireValue encodes component's own instanceID ────────

        [Test]
        public void GetComponentWireValue_ReturnsDifferentIdThanGameObject()
        {
            var go = TrackOwnedObject(new GameObject("P258_WireValueTest"));
            var bc = go.AddComponent<BoxCollider>();

            var compWire = TransientObjectId.GetComponentWireValue(bc);
            var goWire = TransientObjectId.GetWireValue(go);

            Assert.That(compWire, Is.Not.EqualTo(goWire),
                "Component instanceID must differ from gameObject instanceID");
        }

        [Test]
        public void GetComponentWireValue_ResolvesToComponent_NotGameObject()
        {
            var go = TrackOwnedObject(new GameObject("P258_ResolveTest"));
            var bc = go.AddComponent<BoxCollider>();

            var compWire = TransientObjectId.GetComponentWireValue(bc);
            var resolved = TransientObjectId.Resolve(compWire);

            Assert.That(resolved, Is.InstanceOf<BoxCollider>(),
                "Wire value of a component should resolve back to that component, not its gameObject");
        }

        // ── 2. Serialization emits ::TypeName format ───────────────────────────

        [Test]
        public void Serialize_ComponentRef_IncludesTypeSuffix()
        {
            // HingeJoint.connectedBody is a Rigidbody (Component) reference field.
            var go1 = TrackOwnedObject(new GameObject("P258_RbHolder"));
            var rb = go1.AddComponent<Rigidbody>();

            var go2 = TrackOwnedObject(new GameObject("P258_JointHolder"));
            var joint = go2.AddComponent<HingeJoint>();
            joint.connectedBody = rb;

            var so = new SerializedObject(joint);
            so.Update();
            var prop = so.FindProperty("m_ConnectedBody");
            Assert.IsNotNull(prop, "HingeJoint must have m_ConnectedBody serialized property");

            var result = ComponentSerializer.GetPropertyValueString(prop);

            // New wire format: "/Path::Rigidbody #componentId"
            Assert.That(result, Does.Contain("::Rigidbody"),
                $"Component ref wire value must include ::TypeName suffix, got: {result}");
            // Old format with parens must not appear
            Assert.That(result, Does.Not.Contain("(Rigidbody)"),
                $"Old '(TypeName)' format must not appear in wire value, got: {result}");
        }

        // ── 3. FindComponentByRef resolves the typed component ─────────────────

        [Test]
        public void FindComponentByRef_ResolvesTypedComponent_NotTransform()
        {
            var go = TrackOwnedObject(new GameObject("P258_FindRef"));
            var bc = go.AddComponent<BoxCollider>();

            // Build the new wire ref format
            var wireRef = $"{ComponentSerializer.GetPath(go)}::BoxCollider #{TransientObjectId.GetComponentWireValue(bc)}";
            var resolved = ComponentSerializer.FindComponentByRef(wireRef);

            Assert.That(resolved, Is.Not.Null, "FindComponentByRef must resolve a valid wire ref");
            Assert.That(resolved, Is.InstanceOf<BoxCollider>(),
                "Resolved component must be BoxCollider, not Transform");
        }

        [Test]
        public void FindComponentByRef_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(ComponentSerializer.FindComponentByRef(null));
            Assert.IsNull(ComponentSerializer.FindComponentByRef(""));
        }

        // ── 4. SetProperty round-trip: wire value resolves to correct component ─
        // (P-258b regression guard: existing ::TypeName #id path still works)

        [Test]
        public void SetProperty_ComponentRef_WiresToConcrete_NotTransform()
        {
            // GO1 has Rigidbody; GO2 has HingeJoint referencing it.
            var go1 = TrackOwnedObject(new GameObject("P258_RbTarget"));
            var rb = go1.AddComponent<Rigidbody>();

            var go2 = TrackOwnedObject(new GameObject("P258_JointSrc"));
            var joint = go2.AddComponent<HingeJoint>();
            go2.AddComponent<Rigidbody>(); // HingeJoint requires Rigidbody on same GO
            joint.connectedBody = rb;  // set the reference in the editor API

            // Get the serialized wire value of connectedBody (new format)
            var so = new SerializedObject(joint);
            so.Update();
            var prop = so.FindProperty("m_ConnectedBody");
            var wireValue = ComponentSerializer.GetPropertyValueString(prop);

            // Clear the reference
            joint.connectedBody = null;
            so.Update();
            so.ApplyModifiedProperties();

            // Set it back via ObjectManager.SetProperty using the wire value
            ObjectManager.SetProperty(
                ComponentSerializer.GetPath(go2), "HingeJoint", "m_ConnectedBody", wireValue);

            // Read back and verify
            so.Update();
            var restoredProp = so.FindProperty("m_ConnectedBody");
            var storedObj = restoredProp.objectReferenceValue;

            Assert.That(storedObj, Is.Not.Null,
                "connectedBody must not be null after SetProperty with wire value");
            Assert.That(storedObj, Is.InstanceOf<Rigidbody>(),
                $"connectedBody must be Rigidbody, not {storedObj?.GetType().Name ?? "null"}");
            Assert.That(storedObj, Is.EqualTo(rb),
                "connectedBody must reference the exact same Rigidbody component");
        }

        // ── 5. P-258b: "path::TypeName" (no #id) — bare type resolution ─────────

        [Test]
        public void SetProperty_PathColonTypeName_ResolvesCorrectComponent()
        {
            // P-258b: LLM provides "/Target::BoxCollider" (no instanceID).
            // ValueParser must resolve the BoxCollider, not Transform.
            var goTarget = TrackOwnedObject(new GameObject("P258b_Target"));
            goTarget.AddComponent<BoxCollider>();
            goTarget.AddComponent<Rigidbody>();  // multiple non-Transform components

            var goSrc = TrackOwnedObject(new GameObject("P258b_Src"));
            goSrc.AddComponent<Rigidbody>();  // HingeJoint needs Rigidbody on same GO
            goSrc.AddComponent<HingeJoint>();

            var joint = goSrc.GetComponent<HingeJoint>();
            var rb = goTarget.GetComponent<Rigidbody>();

            var bareRef = $"{ComponentSerializer.GetPath(goTarget)}::Rigidbody";
            ObjectManager.SetProperty(
                ComponentSerializer.GetPath(goSrc), "HingeJoint", "m_ConnectedBody", bareRef);

            var so = new SerializedObject(joint);
            so.Update();
            var stored = so.FindProperty("m_ConnectedBody").objectReferenceValue;

            Assert.That(stored, Is.Not.Null, "connectedBody must not be null after bare type ref");
            Assert.That(stored, Is.InstanceOf<Rigidbody>(),
                $"P-258b: expected Rigidbody via '::Rigidbody', got {stored?.GetType().Name}");
            Assert.That(stored, Is.EqualTo(rb),
                "P-258b: must reference the exact Rigidbody on the target, not any other component");
        }

        [Test]
        public void SetProperty_PathColonTypeName_UnknownType_ThrowsWithList()
        {
            // P-258b: unknown type name → ArgumentException containing available types.
            var goTarget = TrackOwnedObject(new GameObject("P258b_TypeErr"));
            goTarget.AddComponent<BoxCollider>();

            var goSrc = TrackOwnedObject(new GameObject("P258b_TypeErrSrc"));
            goSrc.AddComponent<HingeJoint>();
            goSrc.AddComponent<Rigidbody>();

            var badRef = $"{ComponentSerializer.GetPath(goTarget)}::RigidChickenBody";
            var ex = Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty(
                    ComponentSerializer.GetPath(goSrc), "HingeJoint", "m_ConnectedBody", badRef));

            Assert.That(ex.Message, Does.Contain("RigidChickenBody"),
                "Exception must name the missing type");
            Assert.That(ex.Message, Does.Contain("BoxCollider"),
                "Exception must list available components");
        }

        [Test]
        public void SetProperty_PathColonTypeName_AssetPath_FallsThrough()
        {
            // P-258b: "Assets/X.mat::SubName" must NOT be intercepted by the new block
            // (FindObject returns null for asset paths) — falls through to sub-asset handler.
            var goSrc = TrackOwnedObject(new GameObject("P258b_AssetFT"));
            goSrc.AddComponent<HingeJoint>();
            goSrc.AddComponent<Rigidbody>();

            var fakeAssetRef = "Assets/DoesNotExist.mat::SubName";
            var ex = Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty(
                    ComponentSerializer.GetPath(goSrc), "HingeJoint", "m_ConnectedBody", fakeAssetRef));

            // Must NOT be the new P-258b error (which would contain "No 'SubName' component on")
            Assert.That(ex.Message, Does.Not.Contain("No 'SubName' component on"),
                "New block must NOT intercept asset paths when FindObject returns null");
            // Sub-asset handler error: "Sub-asset '...' not found in: ..."
            Assert.That(ex.Message, Does.Contain("SubName").Or.Contain("DoesNotExist"),
                "Error must come from the sub-asset handler, not the new P-258b block");
        }
    }
}
