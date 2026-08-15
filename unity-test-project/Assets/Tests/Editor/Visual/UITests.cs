using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Visual
{
    [TestFixture]
    public class UITests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string ProcessOwned(string json)
        {
            var before = new HashSet<GameObject>(
                SceneManager.GetActiveScene().GetRootGameObjects());
            try
            {
                return CommandRouter.Process(json);
            }
            finally
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root))
                        TrackOwnedObject(root);
            }
        }

        [Test]
        public void CreateUI_Canvas_CreatesCanvasWithScalerAndRaycaster()
        {
            var json = "{\"id\":\"u1\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"TestCanvas\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("TestCanvas", result);

            var go = GameObject.Find("TestCanvas");
            Assert.IsNotNull(go, "Canvas GO should exist");
            Assert.IsNotNull(go.GetComponent<Canvas>());
            Assert.IsNotNull(go.GetComponent<CanvasScaler>());
            Assert.IsNotNull(go.GetComponent<GraphicRaycaster>());

            var scaler = go.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);

            // EventSystem should exist
            Assert.IsNotNull(Object.FindFirstObjectByType<EventSystem>());
        }

        [Test]
        public void CreateUI_Canvas_NoDoubleEventSystem()
        {
            // Create first canvas — creates EventSystem
            ProcessOwned("{\"id\":\"u2a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"C1\"}}");
            var count1 = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;

            // Create second canvas — should NOT create another EventSystem
            ProcessOwned("{\"id\":\"u2b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"C2\"}}");
            var count2 = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;

            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
        }

        [Test]
        public void CreateUI_Panel_StretchByDefault()
        {
            // Create canvas first
            ProcessOwned("{\"id\":\"u3a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"PanelCanvas\"}}");

            var json = "{\"id\":\"u3b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Panel\",\"name\":\"BG\",\"parent\":\"/PanelCanvas\",\"color\":\"#000000CC\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("BG");
            Assert.IsNotNull(go);
            var rt = go.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.IsNotNull(go.GetComponent<Image>());
        }

        [Test]
        public void CreateUI_Button_CreatesChildText()
        {
            ProcessOwned("{\"id\":\"u4a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"BtnCanvas\"}}");

            var json = "{\"id\":\"u4b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Button\",\"name\":\"MyBtn\",\"parent\":\"/BtnCanvas\",\"text\":\"CLICK\",\"size\":\"(200,50)\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("MyBtn");
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<Button>());
            Assert.IsNotNull(go.GetComponent<Image>());

            // Should have child Text
            Assert.IsTrue(go.transform.childCount > 0, "Button should have child");
            var textChild = go.transform.GetChild(0).gameObject;
            Assert.AreEqual("Text", textChild.name);

            // Check RectTransform size
            var rt = go.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(200, 50), rt.sizeDelta);
        }

        [Test]
        public void CreateUI_Text_WithFontSizeAndColor()
        {
            ProcessOwned("{\"id\":\"u5a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"TextCanvas\"}}");

            var json = "{\"id\":\"u5b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Text\",\"name\":\"Label\",\"parent\":\"/TextCanvas\",\"text\":\"Hello\",\"fontSize\":\"24\",\"color\":\"#FF0000\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("Label");
            Assert.IsNotNull(go);
            // Either TMPro or legacy Text should be present
            var legacyText = go.GetComponent<Text>();
            if (legacyText != null)
            {
                Assert.AreEqual("Hello", legacyText.text);
                Assert.AreEqual(24, legacyText.fontSize);
            }
        }

        [Test]
        public void CreateUI_Image_WithColorAndSize()
        {
            ProcessOwned("{\"id\":\"u6a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"ImgCanvas\"}}");

            var json = "{\"id\":\"u6b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Image\",\"name\":\"Icon\",\"parent\":\"/ImgCanvas\",\"color\":\"#00FF00\",\"size\":\"(64,64)\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("Icon");
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<Image>());
            var rt = go.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(64, 64), rt.sizeDelta);
        }

        [Test]
        public void CreateUI_InvalidType_Error()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Unknown UI type"));
            var json = "{\"id\":\"u7\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"UnknownWidget\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("Unknown UI type", result);
        }

        [Test]
        public void SetRect_AnchorPreset()
        {
            ProcessOwned("{\"id\":\"u8a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"RectCanvas\"}}");
            ProcessOwned("{\"id\":\"u8b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Image\",\"name\":\"RectImg\",\"parent\":\"/RectCanvas\"}}");

            var json = "{\"id\":\"u8c\",\"cmd\":\"set_rect\",\"args\":{\"path\":\"/RectCanvas/RectImg\",\"anchor\":\"top-left\",\"pos\":\"(10,10)\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("updated", result);

            var go = GameObject.Find("RectImg");
            var rt = go.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0, 1), rt.anchorMin);
            Assert.AreEqual(new Vector2(0, 1), rt.anchorMax);
            Assert.AreEqual(new Vector2(10, 10), rt.anchoredPosition);
        }

        [Test]
        public void SetRect_PosAndSize()
        {
            ProcessOwned("{\"id\":\"u9a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"SzCanvas\"}}");
            ProcessOwned("{\"id\":\"u9b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Image\",\"name\":\"SzImg\",\"parent\":\"/SzCanvas\"}}");

            var json = "{\"id\":\"u9c\",\"cmd\":\"set_rect\",\"args\":{\"path\":\"/SzCanvas/SzImg\",\"pos\":\"(50,-30)\",\"size\":\"(200,100)\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("SzImg");
            var rt = go.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(50, -30), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(200, 100), rt.sizeDelta);
        }

        [Test]
        public void SetRect_NoRectTransform_Error()
        {
            var go = new GameObject("PlainObj3D");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No RectTransform"));
                var json = "{\"id\":\"u10\",\"cmd\":\"set_rect\",\"args\":{\"path\":\"/PlainObj3D\",\"anchor\":\"center\"}}";
                var result = ProcessOwned(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("No RectTransform", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetRect_InvalidAnchor_Error()
        {
            ProcessOwned("{\"id\":\"u11a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"ErrCanvas\"}}");
            ProcessOwned("{\"id\":\"u11b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Image\",\"name\":\"ErrImg\",\"parent\":\"/ErrCanvas\"}}");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Unknown anchor"));
            var json = "{\"id\":\"u11c\",\"cmd\":\"set_rect\",\"args\":{\"path\":\"/ErrCanvas/ErrImg\",\"anchor\":\"banana\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("Unknown anchor", result);
        }

        [Test]
        public void CreateUI_DefaultName()
        {
            ProcessOwned("{\"id\":\"u12a\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\",\"name\":\"DfCanvas\"}}");

            // No name → should default to "Image"
            var json = "{\"id\":\"u12b\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Image\",\"parent\":\"/DfCanvas\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("Image");
            Assert.IsNotNull(go, "Default name should be the type name");
        }

        [Test]
        public void CreateUI_AutoCanvas_WhenNoParent()
        {
            // No canvas exists, no parent specified → auto-create Canvas
            var json = "{\"id\":\"u13\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Button\",\"name\":\"AutoBtn\",\"text\":\"GO\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);

            // Canvas should have been auto-created
            Assert.IsNotNull(Object.FindFirstObjectByType<Canvas>(), "Auto-Canvas should exist");
            var btn = GameObject.Find("AutoBtn");
            Assert.IsNotNull(btn);
            Assert.IsNotNull(btn.GetComponent<Button>());
        }

        [Test]
        public void Batch_CreateUI_Integration()
        {
            var commands = "create_ui type=Canvas name=BatchCanvas\n" +
                           "create_ui type=Panel name=BG parent=/BatchCanvas color=#000000AA anchor=stretch\n" +
                           "create_ui type=Button name=Btn1 parent=/BatchCanvas text=OK size=(160,40)\n" +
                           "set_rect path=/BatchCanvas/Btn1 anchor=center pos=(0,0)";

            var json = "{\"id\":\"u14\",\"cmd\":\"batch\",\"args\":{\"commands\":\"" +
                       commands.Replace("\n", "\\n").Replace("\"", "\\\"") + "\"}}";
            var result = ProcessOwned(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("ok:4", result);

            Assert.IsNotNull(GameObject.Find("BatchCanvas"));
            Assert.IsNotNull(GameObject.Find("BG"));
            Assert.IsNotNull(GameObject.Find("Btn1"));
        }

        [Test]
        public void PlayModeGuard_BlocksCreateUI()
        {
            var original = CommandRouter.IsPlayMode;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var json = "{\"id\":\"u15\",\"cmd\":\"create_ui\",\"args\":{\"type\":\"Canvas\"}}";
                var result = ProcessOwned(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("Play mode", result);
            }
            finally
            {
                CommandRouter.IsPlayMode = original;
            }
        }
    }
}
