using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Scene
{
    [TestFixture]
    public class SearchTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void SearchScene_ByName_FindsMatches()
        {
            var go1 = new GameObject("SearchPlayer");
            var go2 = new GameObject("SearchEnemy");
            try
            {
                var json = "{\"id\":\"s100\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"SearchPlayer\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("SearchPlayer", result);
                StringAssert.DoesNotContain("SearchEnemy", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void SearchScene_ByComponent_FindsMatches()
        {
            var go1 = new GameObject("LightObj");
            go1.AddComponent<Light>();
            var go2 = new GameObject("NoLightObj");
            try
            {
                var json = "{\"id\":\"s101\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"t:Light\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("LightObj", result);
                StringAssert.Contains("[Light]", result);
                StringAssert.DoesNotContain("NoLightObj", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void SearchScene_ByTag_FindsMatches()
        {
            var go1 = new GameObject("TaggedObj");
            go1.tag = "MainCamera";
            var go2 = new GameObject("UntaggedObj");
            try
            {
                var json = "{\"id\":\"s102\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"tag=MainCamera\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("TaggedObj", result);
                StringAssert.DoesNotContain("UntaggedObj", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void SearchScene_ByActiveState_FindsInactive()
        {
            var go1 = new GameObject("ActiveObj");
            var go2 = new GameObject("InactiveObj");
            go2.SetActive(false);
            try
            {
                var json = "{\"id\":\"s103\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"active=false\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("InactiveObj", result);
                StringAssert.Contains("!", result);
                StringAssert.DoesNotContain("ActiveObj", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void SearchScene_CombinedQuery_FiltersCorrectly()
        {
            var go1 = new GameObject("ComboLight1");
            go1.AddComponent<Light>();
            var go2 = new GameObject("ComboLight2");
            go2.AddComponent<Light>();
            var go3 = new GameObject("ComboCamera");
            go3.AddComponent<Camera>();
            try
            {
                var json = "{\"id\":\"s104\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"t:Light Combo\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("ComboLight1", result);
                StringAssert.Contains("ComboLight2", result);
                StringAssert.DoesNotContain("ComboCamera", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
                Object.DestroyImmediate(go3);
            }
        }

        [Test]
        public void SearchScene_NoMatches_ReturnsNoMatches()
        {
            var json = "{\"id\":\"s105\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"NonExistentObjectXYZ123\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("no matches", result);
        }

        [Test]
        public void SearchScene_EmptyQuery_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("query is required"));
            var json = "{\"id\":\"s106\",\"cmd\":\"search_scene\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void SearchScene_ByLayer_FindsMatches()
        {
            var go1 = new GameObject("LayerObj");
            go1.layer = 5;
            var go2 = new GameObject("DefaultLayerObj");
            try
            {
                var json = "{\"id\":\"s107\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"layer=5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("LayerObj", result);
                StringAssert.DoesNotContain("DefaultLayerObj", result);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void SearchScene_CaseInsensitive_FindsMatches()
        {
            var go = new GameObject("MyCoolObject");
            try
            {
                var json = "{\"id\":\"s108\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"mycoolobject\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("MyCoolObject", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Cycle 6c: empty-hint tests ---

        [Test]
        public void Search_NoMatches_IncludesSceneContext()
        {
            // Empty scene query for something that cannot exist
            var json = "{\"id\":\"s200\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"Foo_6c_NoExist\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("no matches", result);
            // Must mention object count (even "0 objects") or scene name
            bool hasCount = result.Contains("objects") || result.Contains("0 objects");
            bool hasScene = result.Contains(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Assert.IsTrue(hasCount || hasScene, $"Expected object count or scene name in: {result}");
        }

        [Test]
        public void Search_NoMatches_ListsTopLevelObjects()
        {
            var names = new[] { "Root6cA", "Root6cB", "Root6cC", "Root6cD", "Root6cE" };
            var gos = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
                gos[i] = new GameObject(names[i]);
            try
            {
                var json = "{\"id\":\"s201\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"Bzzz_6c_NoExist\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                foreach (var n in names)
                    StringAssert.Contains(n, result);
            }
            finally
            {
                foreach (var go in gos) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Search_NoMatches_CapsTopLevelAt8()
        {
            // 10 roots → expect first 8 names + "+2 more", and 9th/10th NOT listed in `top:` line
            var names = new string[10];
            var gos = new GameObject[10];
            for (int i = 0; i < 10; i++) { names[i] = $"CapRoot6c_{i:D2}"; gos[i] = new GameObject(names[i]); }
            try
            {
                var json = "{\"id\":\"s201c\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"Bzzz_6c_NoExist_Cap\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Cap exercised: "+K more" must appear where K = (total roots - 8) ≥ 1
                StringAssert.IsMatch(@"\+\d+ more", result);
                // Last created name (i=9) must NOT appear inside `top:` line — guarantees cap is real
                var topIdx = result.IndexOf("top:");
                var hintIdx = result.IndexOf("hint:");
                Assert.Greater(topIdx, 0, "top: line missing");
                Assert.Greater(hintIdx, topIdx, "hint: line missing or before top:");
                var topLine = result.Substring(topIdx, hintIdx - topIdx);
                StringAssert.DoesNotContain(names[9], topLine);
            }
            finally
            {
                foreach (var go in gos) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Search_NoMatches_HintsSyntax()
        {
            var json = "{\"id\":\"s202\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"name=Foo_6c\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("hint:", result);
        }

        [Test]
        public void Search_WithMatches_NoContextAppended()
        {
            var go = new GameObject("MatchTarget6c");
            try
            {
                var json = "{\"id\":\"s203\",\"cmd\":\"search_scene\",\"args\":{\"query\":\"MatchTarget6c\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("MatchTarget6c", result);
                StringAssert.DoesNotContain("objects total", result);
                StringAssert.DoesNotContain("hint:", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
