using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MaterialSlotTests : SceneTestBase
    {
        private GameObject _go;
        private Material _mat0;
        private Material _mat1;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("MatSlotTest");
            var renderer = _go.AddComponent<MeshRenderer>();
            _mat0 = new Material(Shader.Find("Standard")) { name = "Mat0" };
            _mat1 = new Material(Shader.Find("Standard")) { name = "Mat1" };
            renderer.sharedMaterials = new[] { _mat0, _mat1 };
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                var renderer = _go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat != _mat0 && mat != _mat1)
                            UnityEngine.Object.DestroyImmediate(mat);
                    }
                }
            }
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_mat0);
            UnityEngine.Object.DestroyImmediate(_mat1);
        }

        [Test]
        public void ListSlots_ReturnsAllMaterials()
        {
            _go.name = "ListSlotsAll";
            _go.transform.SetParent(null);
            var result = MaterialHelper.Execute("list_slots",
                "{\"object_path\":\"/ListSlotsAll\"}");

            StringAssert.Contains("[0]", result);
            StringAssert.Contains("[1]", result);
            StringAssert.Contains("Mat0", result);
            StringAssert.Contains("Mat1", result);
        }

        [Test]
        public void ResolveMaterial_Slot0_ReturnsFirst()
        {
            _go.name = "SlotZero";
            _go.transform.SetParent(null);
            var result = MaterialHelper.Execute("get",
                "{\"object_path\":\"/SlotZero\",\"slot\":0}");

            StringAssert.Contains("Standard", result);
        }

        [Test]
        public void ResolveMaterial_Slot1_ReturnsSecond()
        {
            _go.name = "SlotOne";
            _go.transform.SetParent(null);
            var result = MaterialHelper.Execute("get",
                "{\"object_path\":\"/SlotOne\",\"slot\":1}");

            StringAssert.Contains("Standard", result);
        }

        [Test]
        public void ResolveMaterial_InvalidSlot_ReturnsError()
        {
            _go.name = "InvalidSlot";
            _go.transform.SetParent(null);

            Assert.Throws<ArgumentException>(() =>
                MaterialHelper.Execute("get",
                    "{\"object_path\":\"/InvalidSlot\",\"slot\":5}"));
        }

        [Test]
        public void Copy_WithSlot_CopiesCorrectSlot()
        {
            _go.name = "CopySrc";
            _go.transform.SetParent(null);

            var target = new GameObject("CopyTgt");
            target.transform.SetParent(null);
            var tr = target.AddComponent<MeshRenderer>();
            var tMat0 = new Material(Shader.Find("Standard")) { name = "TgtMat0" };
            var tMat1 = new Material(Shader.Find("Standard")) { name = "TgtMat1" };
            tr.sharedMaterials = new[] { tMat0, tMat1 };

            try
            {
                var result = MaterialHelper.Execute("copy",
                    "{\"source\":\"/CopySrc\",\"targets\":\"/CopyTgt\",\"slot\":1}");

                StringAssert.Contains("1 copied", result);
                Assert.AreEqual("Mat1", tr.sharedMaterials[1].name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(tMat0);
                UnityEngine.Object.DestroyImmediate(tMat1);
            }
        }

        [Test]
        public void ListSlots_IsValidAction()
        {
            _go.name = "ListSlotsAction";
            _go.transform.SetParent(null);

            // Should not throw — list_slots is a valid action
            Assert.DoesNotThrow(() =>
                MaterialHelper.Execute("list_slots",
                    "{\"object_path\":\"/ListSlotsAction\"}"));
        }

        [Test]
        public void Set_TargetInstance_DoesNotLogEditModeMaterialError()
        {
            _go.name = "MaterialInstanceNoLog";
            _go.transform.SetParent(null);
            var originalColor = _mat0.GetColor("_Color");

            var result = MaterialHelper.Execute("set",
                "{\"object_path\":\"/MaterialInstanceNoLog\",\"prop\":\"_Color\",\"value\":\"#00FF00\",\"target\":\"instance\"}");

            StringAssert.Contains("mutated: renderer_instance", result);
            StringAssert.Contains("asset_modified: false", result);
            Assert.AreEqual(Color.green, _go.GetComponent<Renderer>().sharedMaterials[0].GetColor("_Color"));
            Assert.AreEqual(originalColor, _mat0.GetColor("_Color"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
