// NUnit tests for ValueParser — CS2.test.2 + CS2.arch.6/CS2.test.9 (Float bug regression).
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ValueParserTests : SceneTestBase
    {
        // ── ParseBool ─────────────────────────────────────────────────────────

        [Test]
        public void ParseBool_True_CaseInsensitive()
        {
            Assert.IsTrue(ValueParser.ParseBool("true"));
            Assert.IsTrue(ValueParser.ParseBool("TRUE"));
            Assert.IsTrue(ValueParser.ParseBool("True"));
            Assert.IsTrue(ValueParser.ParseBool("1"));
        }

        [Test]
        public void ParseBool_False_CaseInsensitive()
        {
            Assert.IsFalse(ValueParser.ParseBool("false"));
            Assert.IsFalse(ValueParser.ParseBool("FALSE"));
            Assert.IsFalse(ValueParser.ParseBool("False"));
            Assert.IsFalse(ValueParser.ParseBool("0"));
        }

        [Test]
        public void ParseBool_Empty_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseBool(""));
        }

        [Test]
        public void ParseBool_Invalid_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseBool("yes"));
        }

        // ── ParseColor ────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_HexFormat_RGBA()
        {
            var c = ValueParser.ParseColor("#FF0000FF");
            Assert.AreEqual(1f, c.r, 0.01f);
            Assert.AreEqual(0f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
            Assert.AreEqual(1f, c.a, 0.01f);
        }

        [Test]
        public void ParseColor_HexWithoutHash_RGB()
        {
            var c = ValueParser.ParseColor("00FF00");
            Assert.AreEqual(0f, c.r, 0.01f);
            Assert.AreEqual(1f, c.g, 0.01f);
            Assert.AreEqual(0f, c.b, 0.01f);
        }

        [Test]
        public void ParseColor_RgbTuple_Floats()
        {
            var c = ValueParser.ParseColor("(0.5, 0.25, 0.1)");
            Assert.AreEqual(0.5f, c.r, 0.01f);
            Assert.AreEqual(0.25f, c.g, 0.01f);
            Assert.AreEqual(0.1f, c.b, 0.01f);
            Assert.AreEqual(1f, c.a, 0.01f);
        }

        [Test]
        public void ParseColor_InvalidHex_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseColor("#ZZZZZZ"));
        }

        // ── SplitArrayValues ─────────────────────────────────────────────────

        [Test]
        public void SplitArrayValues_NestedParens_CountsCorrectly()
        {
            var r = ValueParser.SplitArrayValues("[(0,1),(2,3)]");
            Assert.AreEqual(2, r.Length);
            Assert.AreEqual("(0,1)", r[0]);
            Assert.AreEqual("(2,3)", r[1]);
        }

        [Test]
        public void SplitArrayValues_SimpleList_SplitsOnComma()
        {
            var r = ValueParser.SplitArrayValues("[a, b, c]");
            Assert.AreEqual(3, r.Length);
        }

        [Test]
        public void SplitArrayValues_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, ValueParser.SplitArrayValues("[]").Length);
            Assert.AreEqual(0, ValueParser.SplitArrayValues("").Length);
        }

        // ── SetPropertyValue — Float branch (CS2.arch.6 / CS2.test.9) ────────

        [Test]
        public void SetPropertyValue_Float_ValidInput_Succeeds()
        {
            var go = new GameObject("VP_FloatTest");
            go.AddComponent<Light>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Light>());
                var prop = so.FindProperty("m_Intensity");
                Assert.IsNotNull(prop, "m_Intensity must exist on Light");
                ValueParser.SetPropertyValue(prop, "2.5");
                so.ApplyModifiedProperties();
                Assert.AreEqual(2.5f, go.GetComponent<Light>().intensity, 0.001f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_Float_InvalidInput_ThrowsArgumentException()
        {
            var go = new GameObject("VP_FloatBad");
            go.AddComponent<Light>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Light>());
                var prop = so.FindProperty("m_Intensity");
                Assert.IsNotNull(prop);
                Assert.Throws<System.ArgumentException>(() => ValueParser.SetPropertyValue(prop, "not_a_float"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── SetPropertyValue — Integer branch ─────────────────────────────────

        [Test]
        public void SetPropertyValue_Integer_ValidInput_Succeeds()
        {
            var go = new GameObject("VP_IntTest");
            go.AddComponent<Camera>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Camera>());
                var prop = so.FindProperty("m_CullingMask");
                Assert.IsNotNull(prop, "m_CullingMask must exist on Camera");
                ValueParser.SetPropertyValue(prop, "5");
                so.ApplyModifiedProperties();
                Assert.AreEqual(5, go.GetComponent<Camera>().cullingMask);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_Integer_InvalidInput_ThrowsArgumentException()
        {
            var go = new GameObject("VP_IntBad");
            go.AddComponent<Camera>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Camera>());
                var prop = so.FindProperty("m_CullingMask");
                Assert.IsNotNull(prop);
                Assert.Throws<System.ArgumentException>(() => ValueParser.SetPropertyValue(prop, "xyz"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── SetPropertyValue — Bool branch ────────────────────────────────────

        [Test]
        public void SetPropertyValue_Bool_SetsCorrectly()
        {
            var go = new GameObject("VP_BoolTest");
            go.AddComponent<Light>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Light>());
                var prop = so.FindProperty("m_Enabled");
                Assert.IsNotNull(prop, "m_Enabled must exist on Light");
                ValueParser.SetPropertyValue(prop, "false");
                so.ApplyModifiedProperties();
                Assert.IsFalse(go.GetComponent<Light>().enabled);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── SetPropertyValue — String branch ──────────────────────────────────

        [Test]
        public void SetPropertyValue_String_SetsName()
        {
            var go = new GameObject("VP_StrTest");
            try
            {
                var so = new SerializedObject(go);
                var prop = so.FindProperty("m_Name");
                Assert.IsNotNull(prop, "m_Name must exist on GameObject");
                ValueParser.SetPropertyValue(prop, "NewName");
                so.ApplyModifiedProperties();
                Assert.AreEqual("NewName", go.name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── ParseFloats ───────────────────────────────────────────────────────

        [Test]
        public void ParseFloats_FourValues_ParsesCorrectly()
        {
            var f = ValueParser.ParseFloats("1.5, 2.5, 100, 50", 4);
            Assert.AreEqual(4, f.Length);
            Assert.AreEqual(1.5f, f[0], 0.001f);
            Assert.AreEqual(2.5f, f[1], 0.001f);
            Assert.AreEqual(100f, f[2], 0.001f);
            Assert.AreEqual(50f, f[3], 0.001f);
        }

        [Test]
        public void ParseFloats_SixValues_ParsesCorrectly()
        {
            var f = ValueParser.ParseFloats("(1,2,3,4,5,6)", 6);
            Assert.AreEqual(6, f.Length);
            Assert.AreEqual(1f, f[0], 0.001f);
            Assert.AreEqual(6f, f[5], 0.001f);
        }

        [Test]
        public void ParseFloats_WrongCount_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseFloats("1,2,3", 4));
        }

        [Test]
        public void ParseFloats_InvalidFloat_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => ValueParser.ParseFloats("1,2,abc,4", 4));
        }

        [Test]
        public void ParseFloats_WithParens_StripsAndParses()
        {
            var f = ValueParser.ParseFloats("(10, 20, 30, 40)", 4);
            Assert.AreEqual(10f, f[0], 0.001f);
            Assert.AreEqual(40f, f[3], 0.001f);
        }

        // ── SetPropertyValue — Rect branch ────────────────────────────────────

        [Test]
        public void SetPropertyValue_Rect_SetsViewportRect()
        {
            var go = new GameObject("VP_RectTest");
            go.AddComponent<Camera>();
            try
            {
                var so = new SerializedObject(go.GetComponent<Camera>());
                var prop = so.FindProperty("m_NormalizedViewPortRect");
                Assert.IsNotNull(prop, "m_NormalizedViewPortRect must exist on Camera");
                Assert.AreEqual(SerializedPropertyType.Rect, prop.propertyType);
                ValueParser.SetPropertyValue(prop, "0.1, 0.2, 0.8, 0.6");
                so.ApplyModifiedProperties();
                var r = go.GetComponent<Camera>().rect;
                Assert.AreEqual(0.1f, r.x, 0.01f);
                Assert.AreEqual(0.2f, r.y, 0.01f);
                Assert.AreEqual(0.8f, r.width, 0.01f);
                Assert.AreEqual(0.6f, r.height, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }
        // Note: Bounds/RectInt/BoundsInt integration tests omitted — no built-in
        // component exposes those as serialized properties in EditMode.
        // Parse coverage is provided by ParseFloats tests above.

        // ── BUG B: SetObjectReference quote stripping ─────────────────────────

        [Test]
        public void SetObjectReference_StripsSurroundingQuotes_BeforeAssetLookup()
        {
            // When value arrives with literal surrounding quotes, the strip guard
            // must remove them. Asset won't exist — verify error message has bare path.
            var go = new GameObject("VP_ObjRefQuoteStrip");
            go.AddComponent<MeshFilter>();
            try
            {
                var so = new SerializedObject(go.GetComponent<MeshFilter>());
                var prop = so.FindProperty("m_Mesh");
                Assert.IsNotNull(prop, "m_Mesh must exist on MeshFilter");
                // Pass value with literal surrounding quotes
                var ex = Assert.Throws<System.ArgumentException>(() =>
                    ValueParser.SetPropertyValue(prop, "\"Assets/nonexistent.mesh\""));
                // Error must mention the bare path, NOT the quoted path
                StringAssert.Contains("Assets/nonexistent.mesh", ex.Message);
                StringAssert.DoesNotContain("\\\"Assets", ex.Message);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetObjectReference_NoQuotes_PassesThrough()
        {
            // Bare path (no surrounding quotes) must also work: error contains bare path
            var go = new GameObject("VP_ObjRefNoQuote");
            go.AddComponent<MeshFilter>();
            try
            {
                var so = new SerializedObject(go.GetComponent<MeshFilter>());
                var prop = so.FindProperty("m_Mesh");
                Assert.IsNotNull(prop, "m_Mesh must exist on MeshFilter");
                var ex = Assert.Throws<System.ArgumentException>(() =>
                    ValueParser.SetPropertyValue(prop, "Assets/somepath.mesh"));
                StringAssert.Contains("Assets/somepath.mesh", ex.Message);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── SetObjectReference null/empty clearing (P-12440-081, P-12440-129) ──

        [Test]
        public void SetObjectReference_NullString_ClearsReference()
        {
            var go = new GameObject("VP_ObjRefNullStr");
            go.AddComponent<MeshFilter>();
            try
            {
                var so = new SerializedObject(go.GetComponent<MeshFilter>());
                var prop = so.FindProperty("m_Mesh");
                Assert.IsNotNull(prop, "m_Mesh must exist on MeshFilter");
                ValueParser.SetPropertyValue(prop, "null");
                Assert.IsNull(prop.objectReferenceValue, "objectReferenceValue must be null");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetObjectReference_EmptyString_ClearsReference()
        {
            var go = new GameObject("VP_ObjRefEmptyStr");
            go.AddComponent<MeshFilter>();
            try
            {
                var so = new SerializedObject(go.GetComponent<MeshFilter>());
                var prop = so.FindProperty("m_Mesh");
                Assert.IsNotNull(prop, "m_Mesh must exist on MeshFilter");
                ValueParser.SetPropertyValue(prop, "");
                Assert.IsNull(prop.objectReferenceValue, "objectReferenceValue must be null");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetObjectReference_CSharpNull_ClearsReference()
        {
            var go = new GameObject("VP_ObjRefCSharpNull");
            go.AddComponent<MeshFilter>();
            try
            {
                var so = new SerializedObject(go.GetComponent<MeshFilter>());
                var prop = so.FindProperty("m_Mesh");
                Assert.IsNotNull(prop, "m_Mesh must exist on MeshFilter");
                Assert.DoesNotThrow(() => ValueParser.SetPropertyValue(prop, null));
                Assert.IsNull(prop.objectReferenceValue, "objectReferenceValue must be null");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── SetPropertyValue — Enum branch (Cycle 2: enumValueFlag fix) ──────

        private (GameObject go, SerializedObject so) MakeEnumGo(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<EnumTestComponent>();
            return (go, new SerializedObject(go.GetComponent<EnumTestComponent>()));
        }

        [Test]
        public void SetPropertyValue_GapEnum_IntUnderlyingValue_SetsCorrectly()
        {
            var (go, so) = MakeEnumGo("VP_GapEnumInt");
            try
            {
                var prop = so.FindProperty("_toolType");
                Assert.IsNotNull(prop, "_toolType must exist on EnumTestComponent");
                ValueParser.SetPropertyValue(prop, "5");
                so.ApplyModifiedProperties();
                Assert.AreEqual(ToolType.Wrench, go.GetComponent<EnumTestComponent>()._toolType);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_FlagsEnum_Bitmask_SetsCorrectly()
        {
            var (go, so) = MakeEnumGo("VP_FlagsEnum");
            try
            {
                var prop = so.FindProperty("_perms");
                Assert.IsNotNull(prop, "_perms must exist on EnumTestComponent");
                ValueParser.SetPropertyValue(prop, "3");
                so.ApplyModifiedProperties();
                Assert.AreEqual(PermFlags.Read | PermFlags.Write, go.GetComponent<EnumTestComponent>()._perms);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_GapEnum_StringNameCaseInsensitive()
        {
            var (go, so) = MakeEnumGo("VP_GapEnumCaseInsensitive");
            try
            {
                var prop = so.FindProperty("_toolType");
                Assert.IsNotNull(prop);
                ValueParser.SetPropertyValue(prop, "wrench");
                so.ApplyModifiedProperties();
                Assert.AreEqual(ToolType.Wrench, go.GetComponent<EnumTestComponent>()._toolType);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_UnityGapEnum_KeyCode_Int()
        {
            var (go, so) = MakeEnumGo("VP_KeyCodeInt");
            try
            {
                var prop = so.FindProperty("_keyCode");
                Assert.IsNotNull(prop, "_keyCode must exist on EnumTestComponent");
                ValueParser.SetPropertyValue(prop, "32");   // KeyCode.Space = 32
                so.ApplyModifiedProperties();
                Assert.AreEqual(KeyCode.Space, go.GetComponent<EnumTestComponent>()._keyCode);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_Enum_StringName_ExactMatch()
        {
            var (go, so) = MakeEnumGo("VP_EnumExact");
            try
            {
                var prop = so.FindProperty("_toolType");
                Assert.IsNotNull(prop);
                ValueParser.SetPropertyValue(prop, "Wrench");
                so.ApplyModifiedProperties();
                Assert.AreEqual(ToolType.Wrench, go.GetComponent<EnumTestComponent>()._toolType);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyValue_Enum_Zero()
        {
            var (go, so) = MakeEnumGo("VP_EnumZero");
            try
            {
                var prop = so.FindProperty("_toolType");
                Assert.IsNotNull(prop);
                // Set to non-zero first, then reset to zero
                go.GetComponent<EnumTestComponent>()._toolType = ToolType.Wrench;
                so.Update();
                ValueParser.SetPropertyValue(prop, "0");
                so.ApplyModifiedProperties();
                Assert.AreEqual(ToolType.None, go.GetComponent<EnumTestComponent>()._toolType);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
