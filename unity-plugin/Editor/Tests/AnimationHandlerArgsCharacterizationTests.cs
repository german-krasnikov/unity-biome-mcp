// T2 slice 2 characterization (Plans/typed-catalog-args.md): proves CommandRouter's "animation"
// dispatch (ExecGetAnimation/ExecCreateAnimation/ExecEditAnimation/ExecPreviewAnimation/
// ExecAnimationConsolidated in CommandRouter.MediaHandlers.cs) is byte-for-byte unchanged before
// and after the hand JsonHelper.Extract*/ParseOpt* -> generated animationArgs accessor swap.
// Dispatches real JSON through CommandRouter.ExecuteCommand, exactly as the TCP bridge would,
// touching every one of animation's 15 catalog ARGs with a value that changes the observable
// result, including each handler-level fallback (clip_name??clip, property??"localPosition",
// keys??"", time??0f) and the missing-key path.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AnimationHandlerArgsCharacterizationTests : SceneTestBase
    {
        private GameObject _go;
        private const string AssetFolder = "Assets/TestsTemp/AnimationHandlerArgsCharacterizationTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(AnimationHelper.ResetAssetDirectoryForTests);
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
            AnimationHelper.SetAssetDirectoryForTests(AssetFolder);
            CommandRouter.RegisterAll();
            _go = TrackOwnedObject(new GameObject("AnimArgsCharTest"));
        }

        private string Path => "/" + _go.name;

        [Test]
        public void Create_ClipNameFallsBackToClip_AndVectorPropertyComponentBindingTangent_UseDefaults()
        {
            // clip_name omitted -> falls back to "clip"; property/component_type/binding_path/tangent
            // omitted -> "localPosition"/Transform/""/"auto"(ClampedAuto) defaults applied by the handler.
            var result = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"create\",\"path\":\"" + Path + "\",\"clip\":\"ClipA\"," +
                "\"keys\":\"t:0 v:(1,2,3);t:1 v:(4,5,6)\"}");
            StringAssert.Contains("ClipA", result);

            var clip = AnimationSerializer.FindClip(_go, "ClipA");
            Assert.IsNotNull(clip, "clip_name must fall back to clip's value");

            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.AreEqual(3, bindings.Length, "vector3 keys produce 3 curves");
            foreach (var b in bindings)
            {
                Assert.AreEqual(typeof(Transform), b.type, "component_type default is Transform");
                Assert.AreEqual("", b.path, "binding_path default is root");
                StringAssert.StartsWith("m_LocalPosition", b.propertyName, "property default is localPosition");
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                Assert.AreEqual(AnimationUtility.TangentMode.ClampedAuto,
                    AnimationUtility.GetKeyLeftTangentMode(curve, 0), "tangent default is auto/ClampedAuto");
            }
        }

        [Test]
        public void Create_ExplicitClipNamePropertyComponentBindingTangent_OverrideDefaults()
        {
            var result = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"create\",\"path\":\"" + Path + "\",\"clip_name\":\"ClipB\"," +
                "\"property\":\"intensity\",\"keys\":\"t:0 v:5;t:1 v:10\"," +
                "\"component_type\":\"Light\",\"binding_path\":\"ChildLight\",\"tangent\":\"linear\"}");
            StringAssert.Contains("ClipB", result);

            var clip = AnimationSerializer.FindClip(_go, "ClipB");
            Assert.IsNotNull(clip);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length, "non-vector3 keys (no parentheses) produce 1 curve");
            var b = bindings[0];
            Assert.AreEqual(typeof(Light), b.type);
            Assert.AreEqual("ChildLight", b.path);
            Assert.AreEqual("intensity", b.propertyName);
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            Assert.AreEqual(AnimationUtility.TangentMode.Linear, AnimationUtility.GetKeyLeftTangentMode(curve, 0));
        }

        [Test]
        public void Get_TimePresentOrMissing_SelectsSampleOrDetailBranch_AndCompactChangesOutput()
        {
            CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"create\",\"path\":\"" + Path + "\",\"clip_name\":\"GetClip\"," +
                "\"keys\":\"t:0 v:(0,0,0);t:1 v:(2,4,6)\"}");

            var detailCompact = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"get\",\"path\":\"" + Path + "\",\"clip\":\"GetClip\",\"compact\":true}");
            var detailFull = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"get\",\"path\":\"" + Path + "\",\"clip\":\"GetClip\",\"compact\":false}");
            var detailOmitted = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"get\",\"path\":\"" + Path + "\",\"clip\":\"GetClip\"}");
            var sampled = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"get\",\"path\":\"" + Path + "\",\"clip\":\"GetClip\",\"time\":0.5}");

            StringAssert.StartsWith("clip:", detailFull, "time missing -> detail branch");
            StringAssert.StartsWith("sample:", sampled, "time present -> sample branch");
            Assert.AreNotEqual(detailCompact, detailFull, "compact must change the observable output");
            Assert.AreEqual(detailFull, detailOmitted, "compact missing-key defaults to false");
        }

        [Test]
        public void AddEvent_GetEvents_RemoveEvent_PlumbEventParamsAndTimeFallback()
        {
            CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"create\",\"path\":\"" + Path + "\",\"clip_name\":\"EventClip\"," +
                "\"keys\":\"t:0 v:(0,0,0);t:1 v:(1,1,1)\"}");

            var added = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"add_event\",\"path\":\"" + Path + "\",\"clip\":\"EventClip\"," +
                "\"time\":0.75,\"function_name\":\"Fire\",\"int_param\":7,\"float_param\":3.5,\"string_param\":\"boom\"}");
            StringAssert.Contains("Fire", added);
            StringAssert.Contains("0.75", added);

            var addedZero = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"add_event\",\"path\":\"" + Path + "\",\"clip\":\"EventClip\"," +
                "\"function_name\":\"Zero\"}");
            StringAssert.Contains("0.00", addedZero, "time missing -> handler's ?? 0f fallback");

            var events = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"get_events\",\"path\":\"" + Path + "\",\"clip\":\"EventClip\"}");
            StringAssert.Contains("fn:Fire", events);
            StringAssert.Contains("i:7", events);
            StringAssert.Contains("f:3.5", events);
            StringAssert.Contains("s:boom", events);
            StringAssert.Contains("fn:Zero", events);
            StringAssert.Contains("t:0.0000", events);

            var removedResult = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"remove_event\",\"path\":\"" + Path + "\",\"clip\":\"EventClip\",\"time\":0.75}");
            StringAssert.Contains("event removed: 1", removedResult);
        }

        [Test]
        public void Preview_TimeKeyMissing_FallsBackToZero_ExplicitTimeSamplesCorrectly()
        {
            CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"create\",\"path\":\"" + Path + "\",\"clip_name\":\"PreviewClip\"," +
                "\"property\":\"intensity\",\"keys\":\"t:0 v:5;t:1 v:10\",\"component_type\":\"Light\"}");

            // time omitted -> handler's ?? 0f fallback samples at t=0 (clip start value).
            var atZero = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"preview\",\"path\":\"" + Path + "\",\"clip\":\"PreviewClip\"}");
            StringAssert.Contains("intensity: 5", atZero);

            // time explicit, non-fallback.
            var atOne = CommandRouter.ExecuteCommand("animation",
                "{\"action\":\"preview\",\"path\":\"" + Path + "\",\"clip\":\"PreviewClip\",\"time\":1.0}");
            StringAssert.Contains("intensity: 10", atOne);
            Assert.AreNotEqual(atZero, atOne);
        }
    }
}
