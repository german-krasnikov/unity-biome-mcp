// T2 slice 3 characterization (Plans/typed-catalog-args.md): proves CommandRouter's "timeline"
// dispatch (ExecGetTimeline/ExecCreateTimeline/ExecEditTimeline/ExecSetClipIn/ExecGetBindings/
// ExecPreviewTimeline/ExecTimelineConsolidated in CommandRouter.MediaHandlers.cs) is
// byte-for-byte unchanged before and after the hand JsonHelper.Extract*/ParseOpt* -> generated
// timelineArgs accessor swap. Dispatches real JSON through CommandRouter.ExecuteCommand, exactly
// as the TCP bridge would, touching every one of timeline's 18 catalog ARGs with a value that
// changes the observable result, including each handler-level fallback (director_path??path,
// action??"sample", clip_in/time/offset/value/duration ?? default) and the missing-key path.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class TimelineHandlerArgsCharacterizationTests : SceneTestBase
    {
        private GameObject _director;
        private static readonly string AssetFolder = TestPaths.ForFixture("TimelineHandlerArgsCharacterizationTests");

        [SetUp]
        public void SetUp()
        {
            CommandRouter.RegisterAll();
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
            _director = TrackOwnedObject(new GameObject("TimelineArgsCharDirector"));
            _director.AddComponent<PlayableDirector>();
        }

        private string DirectorPath => "/" + _director.name;

        // Shared fixture: creates a timeline bound to _director, optionally with initial tracks.
        private void CreateAsset(string caseName, string tracks = null)
        {
            var assetPath = AssetFolder + "/" + caseName + ".playable";
            TrackOwnedAsset(assetPath);
            var tracksArg = tracks != null ? ",\"tracks\":\"" + tracks + "\"" : "";
            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"director_path\":\"" +
                DirectorPath + "\"" + tracksArg + "}");
            StringAssert.DoesNotStartWith("err", result, result);
        }

        [Test]
        public void Create_DirectorPathPresent_WinsOverPath_WhenBothDiffer()
        {
            // director_path and path both present with DIFFERENT values: director_path must win
            // (the ExecCreateTimeline fallback is director_path ?? path — this proves it is not
            // accidentally flipped to path ?? director_path by the accessor swap).
            var otherGo = TrackOwnedObject(new GameObject("TimelineArgsCharOtherPath"));
            otherGo.AddComponent<PlayableDirector>();
            var assetPath = AssetFolder + "/Case_DirectorWins.playable";
            TrackOwnedAsset(assetPath);

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"path\":\"/" + otherGo.name +
                "\",\"director_path\":\"" + DirectorPath + "\"}");

            StringAssert.Contains("director: " + DirectorPath, result);
            Assert.IsNull(otherGo.GetComponent<PlayableDirector>().playableAsset,
                "path must be ignored when director_path is present and differs");
            Assert.IsNotNull(_director.GetComponent<PlayableDirector>().playableAsset,
                "director_path's GameObject must receive the timeline asset");
        }

        [Test]
        public void Create_OnlyPathPresent_DirectorPathOmitted_UsesPathAsDirector()
        {
            var assetPath = AssetFolder + "/Case_PathFallback.playable";
            TrackOwnedAsset(assetPath);

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"create\",\"asset_path\":\"" + assetPath + "\",\"path\":\"" + DirectorPath + "\"}");

            StringAssert.Contains("director: " + DirectorPath, result);
            Assert.IsNotNull(_director.GetComponent<PlayableDirector>().playableAsset,
                "director_path omitted -> handler must fall back to path");
        }

        [Test]
        public void Create_TracksString_AddsEachDeclaredTrack()
        {
            CreateAsset("Case_Tracks", "Animation:Hero;Audio:Music");

            var detail = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\"}");
            StringAssert.Contains("2 tracks", detail);
            StringAssert.Contains("Hero", detail);
            StringAssert.Contains("Music", detail);
        }

        [Test]
        public void Get_TrackOmitted_ReturnsListSummary_TrackPresent_ReturnsTrackDetail()
        {
            CreateAsset("Case_Get", "Animation:Hero");

            var list = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\"}");
            var detail = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\"}");

            StringAssert.StartsWith("Timeline:", list, "track omitted -> track-list summary");
            StringAssert.StartsWith("[Animation] Hero", detail, "track present -> track-detail view");
            Assert.AreNotEqual(list, detail);
        }

        [Test]
        public void Edit_AddTrack_CreatesTrackOfGivenTypeAndName()
        {
            CreateAsset("Case_AddTrack");

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_track\",\"path\":\"" + DirectorPath +
                "\",\"track_type\":\"Audio\",\"track\":\"MusicTrack\"}");

            StringAssert.Contains("[Audio] MusicTrack", result);
            var detail = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\",\"track\":\"MusicTrack\"}");
            StringAssert.StartsWith("[Audio] MusicTrack", detail);
        }

        [Test]
        public void Edit_AddClipThenSetTiming_AppliesStartDurationBlendInBlendOut()
        {
            // blendInDuration/blendOutDuration are clamped by Unity Timeline to the overlap with a
            // neighbouring clip (verified empirically: no neighbour -> silently clamped to 0), so
            // Lead/Tail give Shot1 something to blend into/out of once set_timing moves it to 2-6s.
            CreateAsset("Case_Timing", "Animation:Hero");
            CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_clip\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"clip\":\"Lead\",\"start\":0,\"duration\":2.5}");
            CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_clip\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"clip\":\"Tail\",\"start\":5,\"duration\":2}");

            var added = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_clip\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"start\":1,\"duration\":2}");
            StringAssert.Contains("1.0-3.0s", added, "clip start+duration define its span");

            var timed = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_timing\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"start\":2,\"duration\":4,\"blend_in\":0.5,\"blend_out\":1}");
            StringAssert.Contains("Shot1", timed);

            var detail = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\"}");
            StringAssert.Contains("2.0-6.0s: Shot1 (blend-in: 0.5s, blend-out: 1.0s)", detail);
        }

        [Test]
        public void Edit_RenameTrack_UsesNameArg()
        {
            CreateAsset("Case_Rename", "Animation:Old");

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"rename_track\",\"path\":\"" + DirectorPath + "\",\"track\":\"Old\",\"name\":\"New\"}");
            StringAssert.Contains("Old → New", result);

            var detail = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get\",\"path\":\"" + DirectorPath + "\",\"track\":\"New\"}");
            StringAssert.StartsWith("[Animation] New", detail);
        }

        [Test]
        public void Edit_ReorderTrack_MovesTrackToIndex()
        {
            CreateAsset("Case_Reorder", "Animation:A;Audio:B;Activation:C");

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"reorder_track\",\"path\":\"" + DirectorPath + "\",\"track\":\"C\",\"index\":0}");

            StringAssert.Contains("reordered: C", result);
            StringAssert.Contains("→ 0", result);
        }

        [Test]
        public void Edit_DuplicateClip_OffsetControlsStart_OmittedFallsBackToClipDuration()
        {
            CreateAsset("Case_Duplicate", "Animation:Hero");
            CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_clip\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"start\":0,\"duration\":2}");

            var withOffset = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"duplicate_clip\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"offset\":5}");
            StringAssert.Contains("@ 5s", withOffset);

            var withoutOffset = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"duplicate_clip\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"clip\":\"Shot1\"}");
            StringAssert.Contains("@ 2s", withoutOffset, "offset omitted -> handler's ?? clip.duration fallback");
        }

        [Test]
        public void Edit_AddMarkerThenRemoveMarker_UsesStartAndNameArgs_NameOmittedStillSucceeds()
        {
            CreateAsset("Case_Marker", "Signal:Cues");

            var added = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_marker\",\"path\":\"" + DirectorPath + "\",\"track\":\"Cues\",\"start\":3,\"name\":\"Boom\"}");
            StringAssert.Contains("Boom @ 3s", added);

            var addedNoName = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_marker\",\"path\":\"" + DirectorPath + "\",\"track\":\"Cues\",\"start\":7}");
            StringAssert.Contains("@ 7s", addedNoName, "name omitted -> marker keeps its default name");

            var removed = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"remove_marker\",\"path\":\"" + DirectorPath + "\",\"track\":\"Cues\",\"start\":3,\"name\":\"Boom\"}");
            StringAssert.Contains("marker removed @ 3s", removed);
        }

        [Test]
        public void Edit_SetTrackOffset_UsesValueArg_OmittedFallsBackToAuto()
        {
            CreateAsset("Case_TrackOffset", "Animation:Hero");

            var explicitResult = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_track_offset\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"value\":\"transform\"}");
            StringAssert.Contains("= transform", explicitResult);

            var defaultResult = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_track_offset\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\"}");
            StringAssert.Contains("= auto", defaultResult, "value omitted -> handler's ?? \"auto\" fallback");
        }

        [Test]
        public void Edit_SetDuration_FixedWhenPresent_AutoWhenOmitted()
        {
            CreateAsset("Case_Duration");

            var fixedResult = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_duration\",\"path\":\"" + DirectorPath + "\",\"duration\":10}");
            StringAssert.Contains("10s fixed", fixedResult);

            var autoResult = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_duration\",\"path\":\"" + DirectorPath + "\"}");
            StringAssert.Contains("auto", autoResult, "duration omitted -> handler's auto-duration branch");
        }

        [Test]
        public void Edit_AddSubTrack_UsesTrackTypeAndNameUnderGroup()
        {
            CreateAsset("Case_SubTrack", "Group:MyGroup");

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_sub_track\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"MyGroup\",\"track_type\":\"Audio\",\"name\":\"SubAudio\"}");

            StringAssert.Contains("SubAudio", result);
            StringAssert.Contains("in group MyGroup", result);
        }

        [Test]
        public void Edit_SetBinding_UsesBindingArg_AndGetBindingsReflectsIt()
        {
            CreateAsset("Case_Binding", "Animation:Hero");
            var targetGo = TrackOwnedObject(new GameObject("TimelineArgsCharBindTarget"));

            var result = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_binding\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"binding\":\"/" +
                targetGo.name + "\"}");
            StringAssert.Contains("Hero -> /" + targetGo.name, result);

            var bindings = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"get_bindings\",\"path\":\"" + DirectorPath + "\"}");
            StringAssert.Contains("Hero -> /" + targetGo.name, bindings);
        }

        [Test]
        public void SetClipIn_ClipInPresent_SetsValue_OmittedFallsBackToZero()
        {
            CreateAsset("Case_ClipIn", "Animation:Hero");
            CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"add_clip\",\"path\":\"" + DirectorPath +
                "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"start\":0,\"duration\":2}");

            var withClipIn = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_clip_in\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"clip\":\"Shot1\",\"clip_in\":1.5}");
            StringAssert.Contains("clipIn:1.50s", withClipIn);

            var omitted = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"set_clip_in\",\"path\":\"" + DirectorPath + "\",\"track\":\"Hero\",\"clip\":\"Shot1\"}");
            StringAssert.Contains("clipIn:0.00s", omitted, "clip_in omitted -> handler's ?? 0f fallback");
        }

        [Test]
        public void Preview_TimeOmitted_FallsBackToZero_ExplicitTimeSamplesAtGivenTime()
        {
            // TimelineHelper.Preview's own "action" (play|stop|pause|sample) shares the wire key
            // "action" with the outer dispatch selector, so a real request can only ever deliver
            // "preview" here, which falls through the switch's default (== "sample") branch; the
            // explicit "play"/"stop"/"pause" cases and the inner `?? "sample"` fallback are
            // unreachable through the consolidated dispatch (same shape as the animation twin's
            // ExecPreviewAnimation `action ?? "sample"`), so only the time arg is exercised here.
            CreateAsset("Case_Preview", "Animation:Hero");

            var omitted = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"preview\",\"path\":\"" + DirectorPath + "\"}");
            StringAssert.Contains("@ 0.0s", omitted, "time omitted -> handler's ?? 0f fallback");

            var explicitTime = CommandRouter.ExecuteCommand("timeline",
                "{\"action\":\"preview\",\"path\":\"" + DirectorPath + "\",\"time\":2.5}");
            StringAssert.Contains("@ 2.5s", explicitTime);
        }
    }
}
