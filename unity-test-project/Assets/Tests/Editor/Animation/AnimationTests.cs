using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;
using UnityEngine.Timeline;

namespace UnityMCP.TestProject.Animation
{
    [TestFixture]
    public class AnimationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/AnimationTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(AnimationHelper.ResetAssetDirectoryForTests);
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
            AnimationHelper.SetAssetDirectoryForTests(TempFolder);
        }

        [Test]
        public void AssetDirectoryOverride_ResetRestoresProductionDefault()
        {
            Assert.AreEqual(TempFolder, AnimationHelper.AssetDirectory);

            AnimationHelper.ResetAssetDirectoryForTests();
            Assert.AreEqual("Assets/Animations", AnimationHelper.AssetDirectory);

            AnimationHelper.SetAssetDirectoryForTests(TempFolder);
        }

        [Test]
        public void AssetDirectoryOverride_RejectsPathsOutsideOwnedRoot()
        {
            Assert.Throws<System.ArgumentException>(() =>
                AnimationHelper.SetAssetDirectoryForTests("Assets/Animations"));
            Assert.AreEqual(TempFolder, AnimationHelper.AssetDirectory);
        }

        [Test]
        public void CreateAnimation_CreatesClipWithKeyframes()
        {
            var go = new GameObject("AnimTestObj");
            try
            {
                var json = "{\"id\":\"a1\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimTestObj\",\"clip_name\":\"TestMove\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(0,2,0)\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("created", result);
                StringAssert.Contains("TestMove", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetAnimation_ListsClips()
        {
            var go = new GameObject("AnimListObj");
            try
            {
                CommandRouter.Process("{\"id\":\"a2a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimListObj\",\"clip_name\":\"ListTestClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(1,1,1)\"}}");

                var json = "{\"id\":\"a2b\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimListObj\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("ListTestClip", result);
                StringAssert.Contains("curves", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetAnimation_ClipDetail_ShowsCurvesAndKeyframes()
        {
            var go = new GameObject("AnimDetailObj");
            try
            {
                CommandRouter.Process("{\"id\":\"a3a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimDetailObj\",\"clip_name\":\"DetailClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:0.5 v:(1,2,3); t:1 v:(0,0,0)\"}}");

                var json = "{\"id\":\"a3b\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimDetailObj\",\"clip\":\"DetailClip\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("DetailClip", result);
                StringAssert.Contains("0.500:", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EditAnimation_AddKey_InsertsKeyframe()
        {
            var go = new GameObject("AnimEditObj");
            try
            {
                CommandRouter.Process("{\"id\":\"a4a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimEditObj\",\"clip_name\":\"EditClip\",\"property\":\"m_LocalPosition.x\",\"keys\":\"t:0 v:0; t:1 v:5\"}}");

                var json = "{\"id\":\"a4b\",\"cmd\":\"animation\",\"args\":{\"action\":\"add_key\",\"path\":\"/AnimEditObj\",\"clip\":\"EditClip\",\"property\":\"m_LocalPosition.x\",\"keys\":\"t:0.5 v:10\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("edited", result);

                var detail = CommandRouter.Process("{\"id\":\"a4c\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimEditObj\",\"clip\":\"EditClip\"}}");
                StringAssert.Contains("0.500:", detail);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EditAnimation_RemoveCurve_DeletesCurve()
        {
            var go = new GameObject("AnimRemoveObj");
            try
            {
                CommandRouter.Process("{\"id\":\"a5a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimRemoveObj\",\"clip_name\":\"RemoveClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(1,1,1)\"}}");

                var json = "{\"id\":\"a5b\",\"cmd\":\"animation\",\"args\":{\"action\":\"remove_curve\",\"path\":\"/AnimRemoveObj\",\"clip\":\"RemoveClip\",\"property\":\"m_LocalPosition\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                var detail = CommandRouter.Process("{\"id\":\"a5c\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimRemoveObj\",\"clip\":\"RemoveClip\"}}");
                StringAssert.Contains("RemoveClip", detail);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreviewAnimation_Sample_ReturnsSampledValues()
        {
            var go = new GameObject("AnimPreviewObj");
            try
            {
                CommandRouter.Process("{\"id\":\"a6a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimPreviewObj\",\"clip_name\":\"PreviewClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(10,0,0)\"}}");

                var json = "{\"id\":\"a6b\",\"cmd\":\"animation\",\"args\":{\"action\":\"preview\",\"path\":\"/AnimPreviewObj\",\"clip\":\"PreviewClip\",\"time\":\"0.5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("preview", result);
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateAnimation_LocalEulerAngles_MapsToEulerAnglesRaw()
        {
            var go = new GameObject("AnimEulerObj");
            try
            {
                var json = "{\"id\":\"ea1\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimEulerObj\",\"clip_name\":\"EulerClip\",\"property\":\"localEulerAngles\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(0,90,0)\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                var detail = CommandRouter.Process("{\"id\":\"ea2\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimEulerObj\",\"clip\":\"EulerClip\"}}");
                StringAssert.Contains("localEulerAnglesRaw", detail);
                Assert.IsFalse(detail.Contains("m_LocalPosition"), "localEulerAngles should not create m_LocalPosition curves");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Animation_AddKey_ViaConsolidated()
        {
            var go = new GameObject("AnimConsObj");
            try
            {
                CommandRouter.Process("{\"id\":\"ac1\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimConsObj\",\"clip_name\":\"ConsClip\",\"property\":\"m_LocalPosition.x\",\"keys\":\"t:0 v:0; t:1 v:5\"}}");

                var json = "{\"id\":\"ac2\",\"cmd\":\"animation\",\"args\":{\"path\":\"/AnimConsObj\",\"clip\":\"ConsClip\",\"action\":\"add_key\",\"property\":\"m_LocalPosition.x\",\"keys\":\"t:0.5 v:10\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("edited", result);

                var detail = CommandRouter.Process("{\"id\":\"ac3\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimConsObj\",\"clip\":\"ConsClip\"}}");
                StringAssert.Contains("0.500:", detail);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Animation_RemoveCurve_ViaConsolidated()
        {
            var go = new GameObject("AnimConsRmObj");
            try
            {
                CommandRouter.Process("{\"id\":\"ac4\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimConsRmObj\",\"clip_name\":\"RmClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(1,1,1)\"}}");

                var json = "{\"id\":\"ac5\",\"cmd\":\"animation\",\"args\":{\"action\":\"remove_curve\",\"path\":\"/AnimConsRmObj\",\"clip\":\"RmClip\",\"property\":\"m_LocalPosition\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("remove_curve", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Animation_SetLoop_ViaConsolidated()
        {
            var go = new GameObject("AnimConsLoopObj");
            try
            {
                CommandRouter.Process("{\"id\":\"ac6\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/AnimConsLoopObj\",\"clip_name\":\"LoopClip\",\"property\":\"m_LocalPosition.x\",\"keys\":\"t:0 v:0; t:1 v:5\"}}");

                var json = "{\"id\":\"ac7\",\"cmd\":\"animation\",\"args\":{\"action\":\"set_loop\",\"path\":\"/AnimConsLoopObj\",\"clip\":\"LoopClip\",\"keys\":\"true\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("set_loop", result);

                var detail = CommandRouter.Process("{\"id\":\"ac8\",\"cmd\":\"animation\",\"args\":{\"action\":\"get\",\"path\":\"/AnimConsLoopObj\",\"clip\":\"LoopClip\"}}");
                StringAssert.Contains("loop", detail);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Timeline tests (merged from MCPTimelineTests) ---

        [Test]
        public void CreateTimeline_CreatesAssetWithTracks()
        {
            var assetPath = TempFolder + "/TestTimeline.playable";
            try
            {
                var json = "{\"id\":\"t100\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"tracks\":\"Animation:Character;Audio:Music;Activation:Effects\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("created:", result);
                StringAssert.Contains("3 tracks", result);

                var asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                Assert.IsNotNull(asset, "TimelineAsset should exist");
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void GetTimeline_ListsTracksAndBindings()
        {
            var assetPath = TempFolder + "/TestTimeline2.playable";
            var go = new GameObject("TimelineTestObj");
            try
            {
                var createJson = "{\"id\":\"t101\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TimelineTestObj\",\"tracks\":\"Animation:Character;Audio:Music\"}}";
                CommandRouter.Process(createJson);

                var getJson = "{\"id\":\"t102\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TimelineTestObj\"}}";
                var result = CommandRouter.Process(getJson);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Timeline:", result);
                StringAssert.Contains("[Animation] Character", result);
                StringAssert.Contains("[Audio] Music", result);
                StringAssert.Contains("director:", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void GetTimeline_TrackDetail_ShowsClips()
        {
            var assetPath = TempFolder + "/TestTimeline3.playable";
            var go = new GameObject("TimelineTestObj2");
            try
            {
                var createJson = "{\"id\":\"t103\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TimelineTestObj2\",\"tracks\":\"Activation:Effects\"}}";
                CommandRouter.Process(createJson);

                var editJson = "{\"id\":\"t104\",\"cmd\":\"timeline\",\"args\":{\"action\":\"add_clip\",\"path\":\"/TimelineTestObj2\",\"track\":\"Effects\",\"clip\":\"Burst\",\"start\":1.0,\"duration\":2.0}}";
                CommandRouter.Process(editJson);

                var getJson = "{\"id\":\"t105\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TimelineTestObj2\",\"track\":\"Effects\"}}";
                var result = CommandRouter.Process(getJson);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("[Activation] Effects", result);
                StringAssert.Contains("1.0-3.0s", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void EditTimeline_AddTrack_CreatesTrack()
        {
            var assetPath = TempFolder + "/TestTimeline4.playable";
            var go = new GameObject("TimelineTestObj3");
            try
            {
                var createJson = "{\"id\":\"t106\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TimelineTestObj3\"}}";
                CommandRouter.Process(createJson);

                var editJson = "{\"id\":\"t107\",\"cmd\":\"timeline\",\"args\":{\"action\":\"add_track\",\"path\":\"/TimelineTestObj3\",\"track\":\"BGM\",\"track_type\":\"Audio\"}}";
                var result = CommandRouter.Process(editJson);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("add_track", result);
                StringAssert.Contains("[Audio] BGM", result);

                var getJson = "{\"id\":\"t108\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TimelineTestObj3\"}}";
                var getResult = CommandRouter.Process(getJson);
                StringAssert.Contains("[Audio] BGM", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void EditTimeline_SetBinding_BindsTrack()
        {
            var assetPath = TempFolder + "/TestTimeline5.playable";
            var go = new GameObject("TimelineTestObj4");
            var target = new GameObject("BindTarget");
            try
            {
                var createJson = "{\"id\":\"t109\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TimelineTestObj4\",\"tracks\":\"Animation:Character\"}}";
                CommandRouter.Process(createJson);

                var editJson = "{\"id\":\"t110\",\"cmd\":\"timeline\",\"args\":{\"action\":\"set_binding\",\"path\":\"/TimelineTestObj4\",\"track\":\"Character\",\"binding\":\"/BindTarget\"}}";
                var result = CommandRouter.Process(editJson);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("set_binding", result);
                StringAssert.Contains("-> /BindTarget", result);

                var getJson = "{\"id\":\"t111\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TimelineTestObj4\"}}";
                var getResult = CommandRouter.Process(getJson);
                StringAssert.Contains("→ /BindTarget", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void EditTimeline_MuteTrack_ShowsMuted()
        {
            var assetPath = TempFolder + "/TestTimeline6.playable";
            var go = new GameObject("TimelineTestObj5");
            try
            {
                var createJson = "{\"id\":\"t112\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TimelineTestObj5\",\"tracks\":\"Audio:Music\"}}";
                CommandRouter.Process(createJson);

                var muteJson = "{\"id\":\"t113\",\"cmd\":\"timeline\",\"args\":{\"action\":\"mute\",\"path\":\"/TimelineTestObj5\",\"track\":\"Music\"}}";
                var result = CommandRouter.Process(muteJson);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("mute", result);

                var getJson = "{\"id\":\"t114\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TimelineTestObj5\"}}";
                var getResult = CommandRouter.Process(getJson);
                StringAssert.Contains("muted", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void Timeline_AddTrack_ViaConsolidated()
        {
            var assetPath = TempFolder + "/TestTimelineCons1.playable";
            var go = new GameObject("TLConsObj1");
            try
            {
                CommandRouter.Process("{\"id\":\"tc1\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TLConsObj1\",\"path\":\"/TLConsObj1\"}}");

                var json = "{\"id\":\"tc2\",\"cmd\":\"timeline\",\"args\":{\"action\":\"add_track\",\"path\":\"/TLConsObj1\",\"track\":\"SFX\",\"track_type\":\"Audio\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("add_track", result);
                StringAssert.Contains("[Audio] SFX", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void Timeline_SetBinding_ViaConsolidated()
        {
            var assetPath = TempFolder + "/TestTimelineCons2.playable";
            var go = new GameObject("TLConsObj2");
            var target = new GameObject("TLConsTarget");
            try
            {
                CommandRouter.Process("{\"id\":\"tc3\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TLConsObj2\",\"tracks\":\"Animation:Char\",\"path\":\"/TLConsObj2\"}}");

                var json = "{\"id\":\"tc4\",\"cmd\":\"timeline\",\"args\":{\"action\":\"set_binding\",\"path\":\"/TLConsObj2\",\"track\":\"Char\",\"binding\":\"/TLConsTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("set_binding", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void Timeline_Mute_ViaConsolidated()
        {
            var assetPath = TempFolder + "/TestTimelineCons3.playable";
            var go = new GameObject("TLConsObj3");
            try
            {
                CommandRouter.Process("{\"id\":\"tc5\",\"cmd\":\"timeline\",\"args\":{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"/TLConsObj3\",\"tracks\":\"Audio:Music\",\"path\":\"/TLConsObj3\"}}");

                var json = "{\"id\":\"tc6\",\"cmd\":\"timeline\",\"args\":{\"action\":\"mute\",\"path\":\"/TLConsObj3\",\"track\":\"Music\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("mute", result);

                var getResult = CommandRouter.Process("{\"id\":\"tc7\",\"cmd\":\"timeline\",\"args\":{\"action\":\"get\",\"path\":\"/TLConsObj3\"}}");
                StringAssert.Contains("muted", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }
}
