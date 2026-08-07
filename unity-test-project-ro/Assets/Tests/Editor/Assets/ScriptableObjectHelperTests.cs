using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;
using System.Text.RegularExpressions;

namespace UnityMCP.TestProject.Assets
{
    // CS3.arch.9 — ComponentSerializer.GetPropertyValueString ObjectReference fallback (ScriptableObject context)
    [TestFixture]
    public class ScriptableObjectHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _sceneObj;

        [SetUp]
        public void SetUp()
        {
            _sceneObj = TrackOwnedObject(new GameObject("SOAudit_SceneRef"));
        }

        [Test]
        public void GetPropertyValueString_ObjectReference_SceneObject_ReturnsName()
        {
            // Create a SO with an ObjectReference field pointing to a scene object
            var so = ScriptableObject.CreateInstance<TestRefSO>();
            so.targetObject = _sceneObj;

            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("targetObject");
            Assert.IsNotNull(prop, "targetObject property must exist on TestRefSO");

            var result = ComponentSerializer.GetPropertyValueString(prop);

            // Asset path is empty for scene objects — must fall back to name
            Assert.IsFalse(string.IsNullOrEmpty(result),
                "Result must not be empty for a scene-object reference");
            StringAssert.Contains(_sceneObj.name, result,
                "Result should contain the object name when AssetDatabase.GetAssetPath returns empty");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_ObjectReference_Null_ReturnsNull()
        {
            var so = ScriptableObject.CreateInstance<TestRefSO>();
            so.targetObject = null;

            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("targetObject");

            var result = ComponentSerializer.GetPropertyValueString(prop);
            Assert.AreEqual("null", result);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_Integer_ReturnsString()
        {
            var so = ScriptableObject.CreateInstance<TestIntSO>();
            so.value = 42;
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("value");
            Assert.AreEqual("42", ComponentSerializer.GetPropertyValueString(prop));
            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_Float_ReturnsInvariantString()
        {
            var so = ScriptableObject.CreateInstance<TestFloatSO>();
            so.value = 1.5f;
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("value");
            var result = ComponentSerializer.GetPropertyValueString(prop);
            StringAssert.Contains("1.5", result);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_Boolean_ReturnsString()
        {
            var so = ScriptableObject.CreateInstance<TestBoolSO>();
            so.value = true;
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("value");
            var result = ComponentSerializer.GetPropertyValueString(prop);
            StringAssert.AreEqualIgnoringCase("true", result);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_String_ReturnsValue()
        {
            var so = ScriptableObject.CreateInstance<TestStringSO>();
            so.value = "hello";
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("value");
            Assert.AreEqual("hello", ComponentSerializer.GetPropertyValueString(prop));
            Object.DestroyImmediate(so);
        }

        [Test]
        public void GetPropertyValueString_Vector3_ReturnsComponents()
        {
            var so = ScriptableObject.CreateInstance<TestVector3SO>();
            so.value = new Vector3(1, 2, 3);
            var serialized = new SerializedObject(so);
            var prop = serialized.FindProperty("value");
            var result = ComponentSerializer.GetPropertyValueString(prop);
            StringAssert.Contains("1", result);
            StringAssert.Contains("2", result);
            StringAssert.Contains("3", result);
            Object.DestroyImmediate(so);
        }
    }

    // ── Multi-field set (C1-C8) ───────────────────────────────────────────────
    [TestFixture]
    public class ScriptableObjectMultiFieldTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private readonly string TempFolder = "Assets/TestsTemp/SOMultiFieldTests";

        [SetUp]
        public void SetUp()
        {
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
        }

        private string CreateAsset(string name)
        {
            var path  = TempFolder + "/" + name;
            var asset = ScriptableObject.CreateInstance<TestMultiSO>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private string SetFields(string path, string fields)
        {
            // \n in C# string literal = real newline; JSON requires \\n escape
            var escapedFields = fields.Replace("\n", "\\n");
            return CommandRouter.Process(
                $"{{\"id\":\"mf1\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"fields\":\"{escapedFields}\"}}}}");
        }

        private TestMultiSO Reload(string path) =>
            AssetDatabase.LoadAssetAtPath<TestMultiSO>(path);

        // C1
        [Test]
        public void Set_Fields_TwoFields_SetsBoth()
        {
            var path   = CreateAsset("C1.asset");
            var result = SetFields(path, "speed=5.5\nhealth=42");
            StringAssert.Contains("\"ok\":true", result);
            var asset  = Reload(path);
            Assert.AreEqual(5.5f, asset.speed, 0.001f);
            Assert.AreEqual(42, asset.health);
        }

        // C2
        [Test]
        public void Set_Fields_WithSpaceInValue_SetsString()
        {
            var path   = CreateAsset("C2.asset");
            var result = SetFields(path, "label=hello world");
            StringAssert.Contains("\"ok\":true", result);
            Assert.AreEqual("hello world", Reload(path).label);
        }

        // C3
        [Test]
        public void Set_Fields_WithBool_SetsCorrectly()
        {
            var path   = CreateAsset("C3.asset");
            var result = SetFields(path, "enabled=true");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsTrue(Reload(path).enabled);
        }

        // C4
        [Test]
        public void Set_Fields_WithVector3_SetsCorrectly()
        {
            var path   = CreateAsset("C4.asset");
            var result = SetFields(path, "offset=1,2,3");
            StringAssert.Contains("\"ok\":true", result);
            Assert.AreEqual(new Vector3(1, 2, 3), Reload(path).offset);
        }

        // C5
        [Test]
        public void Set_Fields_EmptyLinesIgnored_Succeeds()
        {
            var path   = CreateAsset("C5.asset");
            var result = SetFields(path, "\n\nspeed=9\n\n");
            StringAssert.Contains("\"ok\":true", result);
            Assert.AreEqual(9f, Reload(path).speed, 0.001f);
        }

        // C6
        [Test]
        public void Set_Fields_UnknownProperty_ReturnsError()
        {
            var path   = CreateAsset("C6.asset");
            LogAssert.Expect(LogType.Warning, new Regex("Property not found"));
            var result = SetFields(path, "nonexistentProp=5");
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("Property not found", result);
        }

        // C7
        [Test]
        public void Set_BothPropAndFields_ReturnsError()
        {
            var path   = CreateAsset("C7.asset");
            LogAssert.Expect(LogType.Warning, new Regex("mutually exclusive"));
            var result = CommandRouter.Process(
                $"{{\"id\":\"mf7\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"speed\",\"value\":\"5\",\"fields\":\"health=1\"}}}}");
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("mutually exclusive", result);
        }

        // C8 — regression: existing prop+value path unchanged
        [Test]
        public void Set_SinglePropValue_Regression()
        {
            var path   = CreateAsset("C8.asset");
            var result = CommandRouter.Process(
                $"{{\"id\":\"mf8\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"speed\",\"value\":\"3.0\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.AreEqual(3.0f, Reload(path).speed, 0.001f);
        }
    }

    // ── Create with fields (Wave 3 #14) ─────────────────────────────────────
    [TestFixture]
    public class ScriptableObjectCreateWithFieldsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private readonly string TempFolder = "Assets/TestsTemp/SOCreateFieldsTests";

        [SetUp]
        public void SetUp()
        {
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
        }

        [Test]
        public void Create_WithFields_AppliesFieldValues()
        {
            var path = TempFolder + "/CreateWithFields.asset";
            var result = CommandRouter.Process(
                $"{{\"id\":\"cf1\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"create\",\"type\":\"TestMultiSO\",\"path\":\"{path}\",\"fields\":\"speed=9.5\\nhealth=77\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var asset = AssetDatabase.LoadAssetAtPath<TestMultiSO>(path);
            Assert.IsNotNull(asset, "Asset must exist after create");
            Assert.AreEqual(9.5f, asset.speed, 0.001f, "speed field must be set");
            Assert.AreEqual(77, asset.health, "health field must be set");
        }

        [Test]
        public void Create_WithoutFields_StillWorks()
        {
            var path = TempFolder + "/CreateNoFields.asset";
            var result = CommandRouter.Process(
                $"{{\"id\":\"cf2\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"create\",\"type\":\"TestMultiSO\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TestMultiSO>(path));
        }

        [Test]
        public void Create_WithFields_InvalidField_ReturnsError()
        {
            var path = TempFolder + "/CreateBadField.asset";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Property not found"));
            var result = CommandRouter.Process(
                $"{{\"id\":\"cf3\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"create\",\"type\":\"TestMultiSO\",\"path\":\"{path}\",\"fields\":\"nonexistent=99\"}}}}");
            StringAssert.Contains("\"ok\":false", result);
        }
    }
}

// Must be outside namespace so Unity TypeCache finds them as ScriptableObject subtypes
public class TestRefSO : ScriptableObject
{
    public GameObject targetObject;
}

public class TestIntSO : ScriptableObject
{
    public int value;
}

public class TestFloatSO : ScriptableObject
{
    public float value;
}

public class TestBoolSO : ScriptableObject
{
    public bool value;
}

public class TestStringSO : ScriptableObject
{
    public string value;
}

public class TestVector3SO : ScriptableObject
{
    public Vector3 value;
}

public class TestMultiSO : ScriptableObject
{
    public float   speed;
    public int     health;
    public string  label;
    public bool    enabled;
    public Vector3 offset;
}
