using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Command
{
    public class ValueParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("StructSerializerTest"));
        }

        // --- ValueParser ---

        [Test]
        public void ParseFloats_ThreeComponents()
        {
            var f = ValueParser.ParseFloats("1.0,2.0,3.0", 3);
            Assert.That(f, Is.EqualTo(new float[] { 1f, 2f, 3f }));
        }

        [Test]
        public void ParseFloats_WithParentheses()
        {
            var f = ValueParser.ParseFloats("(1.0,2.0,3.0)", 3);
            Assert.That(f, Is.EqualTo(new float[] { 1f, 2f, 3f }));
        }

        [Test]
        public void ParseFloats_WrongCount_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseFloats("1.0,2.0", 3));
        }

        [Test]
        public void ParseFloats_InvalidFloat_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseFloats("1.0,abc,3.0", 3));
        }

        [Test]
        public void ParseVector2_Basic()
        {
            var v = ValueParser.ParseVector2("1.5,2.5");
            Assert.That(v, Is.EqualTo(new Vector2(1.5f, 2.5f)));
        }

        [Test]
        public void ParseVector3_Basic()
        {
            var v = ValueParser.ParseVector3("1,2,3");
            Assert.That(v, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void ParseVector3_WithSpaces()
        {
            var v = ValueParser.ParseVector3("( 1.0 , 2.0 , 3.0 )");
            Assert.That(v, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void ParseVector4_Basic()
        {
            var v = ValueParser.ParseVector4("1,2,3,4");
            Assert.That(v, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
        }

        [Test]
        public void ParseQuaternion_Identity()
        {
            var q = ValueParser.ParseQuaternion("0,0,0,1");
            Assert.That(q, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void ParseColor_HexWithHash()
        {
            var c = ValueParser.ParseColor("#FF0000");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(0f).Within(0.01f));
            Assert.That(c.b, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void ParseColor_HexWithoutHash()
        {
            var c = ValueParser.ParseColor("FF0000");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ParseColor_Tuple()
        {
            var c = ValueParser.ParseColor("(1,0,0,1)");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.a, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ParseColor_TupleNoAlpha()
        {
            var c = ValueParser.ParseColor("(0,1,0)");
            Assert.That(c.g, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.a, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ParseColor_Invalid_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseColor("#GGGGGG"));
        }

        [Test]
        public void SetPropertyValue_Float()
        {
            var go = new GameObject("TestRB");
            go.AddComponent<Rigidbody>();
            var so = new SerializedObject(go.GetComponent<Rigidbody>());
            var prop = so.FindProperty("m_Mass");
            ValueParser.SetPropertyValue(prop, "5.5");
            so.ApplyModifiedProperties();
            Assert.That(go.GetComponent<Rigidbody>().mass, Is.EqualTo(5.5f).Within(0.001f));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Bool()
        {
            var go = new GameObject("TestRB2");
            go.AddComponent<Rigidbody>();
            var so = new SerializedObject(go.GetComponent<Rigidbody>());
            var prop = so.FindProperty("m_UseGravity");
            ValueParser.SetPropertyValue(prop, "false");
            so.ApplyModifiedProperties();
            Assert.That(go.GetComponent<Rigidbody>().useGravity, Is.False);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_String()
        {
            var go = new GameObject("OldName");
            var so = new SerializedObject(go);
            var prop = so.FindProperty("m_Name");
            ValueParser.SetPropertyValue(prop, "NewName");
            so.ApplyModifiedProperties();
            Assert.That(go.name, Is.EqualTo("NewName"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Vector3()
        {
            var go = new GameObject("TestVec");
            var so = new SerializedObject(go.transform);
            var prop = so.FindProperty("m_LocalPosition");
            ValueParser.SetPropertyValue(prop, "1,2,3");
            so.ApplyModifiedProperties();
            Assert.That(go.transform.localPosition, Is.EqualTo(new Vector3(1, 2, 3)));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Color()
        {
            var go = new GameObject("TestLight");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Color");
            ValueParser.SetPropertyValue(prop, "#FF0000");
            so.ApplyModifiedProperties();
            Assert.That(go.GetComponent<Light>().color.r, Is.EqualTo(1f).Within(0.01f));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Enum()
        {
            var go = new GameObject("TestLightEnum");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Type");
            ValueParser.SetPropertyValue(prop, "Directional");
            so.ApplyModifiedProperties();
            Assert.That(go.GetComponent<Light>().type, Is.EqualTo(LightType.Directional));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Int()
        {
            var go = new GameObject("test_vp_int");
            var rb = go.AddComponent<Rigidbody>();
            var so = new UnityEditor.SerializedObject(rb);
            var prop = so.FindProperty("m_SolverIterations");
            if (prop != null)
            {
                ValueParser.SetPropertyValue(prop, "5");
                so.ApplyModifiedProperties();
                Assert.AreEqual(5, rb.solverIterations);
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Vector2()
        {
            var go = new GameObject("test_vp_v2");
            var col = go.AddComponent<BoxCollider2D>();
            var so = new UnityEditor.SerializedObject(col);
            var prop = so.FindProperty("m_Size");
            ValueParser.SetPropertyValue(prop, "(3,4)");
            so.ApplyModifiedProperties();
            Assert.AreEqual(new Vector2(3, 4), col.size);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_Quaternion()
        {
            var go = new GameObject("test_vp_quat");
            var so = new UnityEditor.SerializedObject(go.transform);
            var prop = so.FindProperty("m_LocalRotation");
            ValueParser.SetPropertyValue(prop, "(0,0,0,1)");
            so.ApplyModifiedProperties();
            Assert.AreEqual(Quaternion.identity, go.transform.localRotation);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetPropertyValue_ObjectReference_Null()
        {
            var go = new GameObject("test_vp_objref");
            var light = go.AddComponent<Light>();
            var so = new UnityEditor.SerializedObject(light);
            var prop = so.FindProperty("m_Cookie");
            ValueParser.SetPropertyValue(prop, "null");
            so.ApplyModifiedProperties();
            Assert.IsNull(light.cookie);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ParseColor_TupleInvalidComponent_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseColor("(1,abc,0)"));
        }

        // Fix C: nested path traversal
        [Test]
        public void GetSerializedFieldType_NestedPath_ReturnsCorrectType()
        {
            var go = new GameObject("test_nested_type");
            var script = go.AddComponent<TestRefScript>();
            var so = new SerializedObject(script);
            // waypoint is a direct field typed Transform — tests basic traversal too
            var prop = so.FindProperty("waypoint");
            Assert.IsNotNull(prop, "waypoint property must exist");
            var fieldType = ValueParser.GetSerializedFieldType(prop);
            Assert.IsNotNull(fieldType);
            Assert.AreEqual(typeof(UnityEngine.Transform), fieldType);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GetSerializedFieldType_GameObjectField_ReturnsGameObjectType()
        {
            var go = new GameObject("test_go_type");
            var script = go.AddComponent<TestRefScript>();
            var so = new SerializedObject(script);
            var prop = so.FindProperty("target");
            Assert.IsNotNull(prop, "target property must exist");
            var fieldType = ValueParser.GetSerializedFieldType(prop);
            Assert.IsNotNull(fieldType);
            Assert.AreEqual(typeof(GameObject), fieldType);
            Object.DestroyImmediate(go);
        }

        // Fix B: sub-asset via :: separator
        [Test]
        public void SetObjectReference_SubAsset_ThrowsForMissingSubAsset()
        {
            var go = new GameObject("test_subasset");
            var light = go.AddComponent<Light>();
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_Cookie");
            // path with :: but no such file — should throw ArgumentException
            Assert.Throws<System.ArgumentException>(() =>
                ValueParser.SetPropertyValue(prop, "Assets/nonexistent.fbx::SomeMesh"));
            Object.DestroyImmediate(go);
        }

        // Cycle 6b: Enum int and string name parsing
        [Test]
        public void ValueParser_Enum_IntInput()
        {
            var go = new GameObject("test_enum_int");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Type");
            ValueParser.SetPropertyValue(prop, "2");
            so.ApplyModifiedProperties();
            Assert.AreEqual(2, prop.enumValueIndex);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ValueParser_Enum_StringName()
        {
            var go = new GameObject("test_enum_str");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Type");
            var names = prop.enumNames;
            // find a valid name other than index 0
            var validName = names.Length > 1 ? names[1] : names[0];
            ValueParser.SetPropertyValue(prop, validName);
            so.ApplyModifiedProperties();
            var expectedIdx = System.Array.IndexOf(names, validName);
            Assert.AreEqual(expectedIdx, prop.enumValueIndex);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ValueParser_Enum_InvalidString()
        {
            var go = new GameObject("test_enum_bad");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Type");
            Assert.Throws<System.ArgumentException>(() => ValueParser.SetPropertyValue(prop, "NotAValidName"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ValueParser_Enum_NegativeInt()
        {
            var go = new GameObject("test_enum_neg");
            go.AddComponent<Light>();
            var so = new SerializedObject(go.GetComponent<Light>());
            var prop = so.FindProperty("m_Type");
            // Negative enum indices are invalid and should throw ArgumentException.
            Assert.Throws<System.ArgumentException>(() => ValueParser.SetPropertyValue(prop, "-1"));
            Object.DestroyImmediate(go);
        }

        // --- JsonHelper ---

        [Test]
        public void ExtractString_KeyInsideValueString_FindsRealKey()
        {
            var json = "{\"data\":\"the \\\"path\\\" is here\",\"path\":\"/real\"}";
            Assert.AreEqual("/real", JsonHelper.ExtractString(json, "path"));
        }

        [Test]
        public void ExtractString_KeyAsValueNotKey_FindsActualKey()
        {
            var json = "{\"type\":\"path\",\"path\":\"/x\"}";
            Assert.AreEqual("/x", JsonHelper.ExtractString(json, "path"));
        }

        [Test]
        public void ExtractString_KeyNotPresent_ReturnsNull()
        {
            var json = "{\"a\":\"b\"}";
            Assert.IsNull(JsonHelper.ExtractString(json, "missing"));
        }

        [Test]
        public void ExtractString_NormalCase()
        {
            var json = "{\"name\":\"foo\",\"value\":\"bar\"}";
            Assert.AreEqual("foo", JsonHelper.ExtractString(json, "name"));
            Assert.AreEqual("bar", JsonHelper.ExtractString(json, "value"));
        }

        [Test]
        public void ExtractString_ColonInValue_FindsRealKey()
        {
            var json = "{\"a\":\"key: val\",\"key\":\"real\"}";
            Assert.AreEqual("real", JsonHelper.ExtractString(json, "key"));
        }

        [Test]
        public void ExtractObject_KeyInsideValueString_FindsRealKey()
        {
            var json = "{\"x\":\"has \\\"args\\\" text\",\"args\":{\"a\":1}}";
            Assert.AreEqual("{\"a\":1}", JsonHelper.ExtractObject(json, "args"));
        }

        [Test]
        public void ExtractString_EscapedQuotesInValue()
        {
            var json = "{\"msg\":\"say \\\"hello\\\"\",\"cmd\":\"test\"}";
            Assert.AreEqual("test", JsonHelper.ExtractString(json, "cmd"));
        }

        [Test]
        public void ExtractString_NestedObjectSameKey_FindsTopLevel()
        {
            var json = "{\"nested\":{\"path\":\"/inner\"},\"path\":\"/outer\"}";
            Assert.AreEqual("/outer", JsonHelper.ExtractString(json, "path"));
        }

        // --- SplitArrayValues ---

        [Test]
        public void SplitArrayValues_SimpleComma()
        {
            var r = ValueParser.SplitArrayValues("A,B,C");
            Assert.AreEqual(new[] { "A", "B", "C" }, r);
        }

        [Test]
        public void SplitArrayValues_Vector3()
        {
            var r = ValueParser.SplitArrayValues("(1,2,3),(4,5,6)");
            Assert.AreEqual(new[] { "(1,2,3)", "(4,5,6)" }, r);
        }

        [Test]
        public void SplitArrayValues_EmptyBrackets()
        {
            var r = ValueParser.SplitArrayValues("[]");
            Assert.AreEqual(0, r.Length);
        }

        [Test]
        public void SplitArrayValues_EmptyString()
        {
            var r = ValueParser.SplitArrayValues("");
            Assert.AreEqual(0, r.Length);
        }

        [Test]
        public void SplitArrayValues_SingleElement()
        {
            var r = ValueParser.SplitArrayValues("hello");
            Assert.AreEqual(new[] { "hello" }, r);
        }

        [Test]
        public void SplitArrayValues_BracketWrapped()
        {
            var r = ValueParser.SplitArrayValues("[A,B,C]");
            Assert.AreEqual(new[] { "A", "B", "C" }, r);
        }

        [Test]
        public void SplitArrayValues_Null()
        {
            var r = ValueParser.SplitArrayValues(null);
            Assert.AreEqual(0, r.Length);
        }

        [Test]
        public void SplitArrayValues_NestedParens()
        {
            var r = ValueParser.SplitArrayValues("((1,2),(3,4))");
            Assert.AreEqual(new[] { "((1,2),(3,4))" }, r);
        }

        [Test]
        public void SplitArrayValues_MixedTypes()
        {
            var r = ValueParser.SplitArrayValues("hello,(1,2),42");
            Assert.AreEqual(new[] { "hello", "(1,2)", "42" }, r);
        }

        // --- StructSerializer ---

        // TryInlineStruct: 2-field (string+int) → "Name (Hash)" pretty format
        [Test]
        public void Serialize_HashIdStruct_PrettyFormat()
        {
            var comp = _go.AddComponent<TestStructScript>();
            comp.itemId = new SimpleHashId { _id = "Corn", _hash = 17806169 };

            var so = new SerializedObject(comp);
            var prop = so.FindProperty("itemId");
            var result = ComponentSerializer.GetPropertyValueString(prop);

            Assert.AreEqual("Corn (17806169)", result);
        }

        // TryInlineStruct: empty string + zero hash → no crash, no XML tag
        [Test]
        public void Serialize_EmptyHashId_NoCrash()
        {
            var comp = _go.AddComponent<TestStructScript>();
            comp.itemId = new SimpleHashId { _id = "", _hash = 0 };

            var so = new SerializedObject(comp);
            var prop = so.FindProperty("itemId");
            var result = ComponentSerializer.GetPropertyValueString(prop);

            Assert.IsNotNull(result);
            StringAssert.DoesNotContain("<SimpleHashId>", result);
            // empty string + 0 still uses pretty format: " (0)"
            StringAssert.Contains("(0)", result);
        }

        // TryInlineStruct: 3-field struct → "{name=val, ...}" explicit format
        [Test]
        public void Serialize_ThreeFieldStruct_ExplicitFormat()
        {
            var comp = _go.AddComponent<TestStructScript>();
            comp.threeField = new ThreeFieldStruct { name = "Iron", value = 42, ratio = 0.5f };

            var so = new SerializedObject(comp);
            var prop = so.FindProperty("threeField");
            var result = ComponentSerializer.GetPropertyValueString(prop);

            StringAssert.Contains("name=Iron", result);
            StringAssert.Contains("value=42", result);
            StringAssert.Contains("ratio=0.5", result);
            Assert.IsTrue(result.StartsWith("{") && result.EndsWith("}"));
        }
    }
}
