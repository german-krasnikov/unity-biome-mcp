# Unity Timeline (MCP)

Multi-track cinematic sequences via PlayableDirector/TimelineAsset. For per-object keyframes use `animation`, for state machines use `animator`.

**Gated:** `discover_tools category="MEDIA"`.

## Actions

```
timeline action="get" path="/Director"
timeline action="create" path="/Director" asset_path="Assets/Timelines/Cutscene.playable"
timeline action="add_track" path="/Director" track="MoveTrack" track_type="Animation" binding="/Character"
timeline action="add_track" path="/Director" track="Grp" track_type="Group"
timeline action="add_sub_track" path="/Director" track="Grp" track_type="Animation" name="ArmTrack"
timeline action="rename_track" path="/Director" track="MoveTrack" name="RunTrack"
timeline action="reorder_track" path="/Director" track="RunTrack" index=0
timeline action="remove_track" path="/Director" track="RunTrack"
timeline action="add_clip" path="/Director" track="MoveTrack" clip="RunClip" start=0.0 duration=2.5
timeline action="duplicate_clip" path="/Director" track="MoveTrack" clip="RunClip" offset=2.5   # -> RunClip_copy
timeline action="set_clip_in" path="/Director" track="MoveTrack" clip="RunClip" clip_in=0.5
timeline action="remove_clip" path="/Director" track="MoveTrack" clip="RunClip"
timeline action="set_binding" path="/Director" track="MoveTrack" binding="/Character"
timeline action="get_bindings" path="/Director"
timeline action="set_timing" path="/Director" track="MoveTrack" clip="RunClip" blend_in=0.2 blend_out=0.3
timeline action="set_track_offset" path="/Director" track="MoveTrack" value="scene"   # AnimationTrack only: auto|transform|scene
timeline action="set_duration" path="/Director" duration=10.0                          # omit duration -> auto (based on clips)
timeline action="add_marker" path="/Director" track="Signals" start=2.0 name="Footstep"
timeline action="remove_marker" path="/Director" track="Signals" name="Footstep"
timeline action="mute" path="/Director" track="MoveTrack"
timeline action="unmute" path="/Director" track="MoveTrack"
timeline action="lock" path="/Director" track="MoveTrack"
timeline action="unlock" path="/Director" track="MoveTrack"
timeline action="preview" path="/Director" time=2.5
```

## Parameters

| Param | Description |
|-------|-------------|
| `action` | `get` / `create` / `add_track` / `remove_track` / `rename_track` / `reorder_track` / `add_sub_track` / `add_clip` / `remove_clip` / `duplicate_clip` / `set_clip_in` / `set_binding` / `get_bindings` / `set_timing` / `set_track_offset` / `set_duration` / `add_marker` / `remove_marker` / `mute` / `unmute` / `lock` / `unlock` / `preview` |
| `path` | Path to GameObject with PlayableDirector (required for `set_binding`/`get_bindings`) |
| `track` | Track name (when targeting a specific track) |
| `track_type` | Short name (case-insensitive): `Animation` \| `Audio` \| `Activation` \| `Control` \| `Signal` \| `Group` |
| `clip` | Clip name on the track |
| `binding` | Scene object path to bind track to |
| `start` | Clip start time (seconds); marker time for `add_marker`; time filter for `remove_marker` |
| `duration` | Clip duration (seconds); timeline fixed duration for `set_duration` (omit = auto/BasedOnClips) |
| `blend_in` / `blend_out` | Blend-in/out duration (seconds) |
| `asset_path` | Asset path for `create` (e.g. `Assets/Timelines/X.playable`) |
| `director_path` | Path to PlayableDirector GO for `create` — **optional**: if omitted, `path` is used as the Director GameObject path (v0.93) |
| `tracks` | Bulk track spec for `create`, e.g. `Animation:Move; Audio:Sfx` (short type names) |
| `time` | Scrub time for `preview` (seconds) |
| `name` | New name (`rename_track`); marker name (`add_marker`, filter for `remove_marker`); sub-track name (`add_sub_track`, defaults to `track_type`) |
| `index` | Target position for `reorder_track` (0-based) |
| `offset` | Time shift for `duplicate_clip` (default: source clip's own duration) |
| `clip_in` | Clip-in offset (seconds) for `set_clip_in` — trims where the source media starts playing |
| `value` | Offset mode for `set_track_offset`: `auto` \| `transform` \| `scene` (AnimationTrack only) |

## Verification

```
timeline action="get" path="/Director"        # confirm tracks, clips, bindings
timeline action="get_bindings" path="/Director"    # confirm track -> object bindings
```

---

# C# Editor API Reference

## Creating & Loading TimelineAsset

```csharp
var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
AssetDatabase.CreateAsset(timeline, "Assets/Timelines/MyTimeline.timeline");

var director = GetComponent<PlayableDirector>();
director.playableAsset = timeline;
```

## Track Creation, Binding & Clips

```csharp
var animTrack = timeline.CreateTrack<AnimationTrack>(null, "AnimTrack");
var audioTrack = timeline.CreateTrack<AudioTrack>(null, "Audio");
var groupTrack = timeline.CreateTrack<GroupTrack>(null, "Group");
var subTrack = timeline.CreateTrack<AnimationTrack>(groupTrack, "Arm");  // nested under Group

director.SetGenericBinding(animTrack, transform.Find("Child").gameObject);
director.SetGenericBinding(audioTrack, gameObject.GetComponent<AudioSource>());

var clip = animTrack.CreateClip<AnimationPlayableAsset>();
clip.displayName = "Run";
clip.start = 0.0; clip.duration = 2.5;
clip.blendInDuration = 0.2; clip.blendOutDuration = 0.2;
clip.clipIn = 0.5;  // trim source media start
((AnimationPlayableAsset)clip.asset).clip = runAnimClip;
```

## Markers

```csharp
var signalTrack = timeline.CreateTrack<SignalTrack>(null, "Signals");
var marker = signalTrack.CreateMarker<SignalEmitter>(2.0);
marker.asset = mySignalAsset;  // ScriptableObject with OnSignalReceived
foreach (var m in signalTrack.GetMarkers())
    if (m is SignalEmitter s) Debug.Log(s.asset.name);
```

## PlayableDirector Control

```csharp
director.Play(); director.Pause(); director.Stop();
director.time = 1.5;
double duration = (director.playableAsset as TimelineAsset).duration;
director.playableGraph.GetRootPlayable(0).SetSpeed(2.0);
```

## Persistence

```csharp
AssetDatabase.AddObjectToAsset(customTrack, timelineAsset);  // custom tracks need this; clips auto-added
AssetDatabase.SaveAssets();
TimelineEditor.Refresh(RefreshReason.ContentsModified);
Undo.RecordObject(timeline, "Add animation track");  // BEFORE modifying
```

## Common Pitfalls

| Issue | Solution |
|-------|----------|
| Clip not visible in timeline | Call AssetDatabase.SaveAssets() after CreateAsset |
| Track binding not working | SetGenericBinding BEFORE Play(); same object/component |
| Sub-assets lost after save | AssetDatabase.AddObjectToAsset() for custom objects before SaveAssets |
| Markers don't trigger | Signal asset needs OnSignalReceived(); director must be playing |
| PlayableDirector.time no-op | Stop() first, then set time — not during playback |

## Sources

- [TimelineAsset](https://docs.unity3d.com/Packages/com.unity.timeline@latest/api/UnityEngine.Timeline.TimelineAsset.html)
- [PlayableDirector](https://docs.unity3d.com/ScriptReference/Playables.PlayableDirector.html)
- [AnimationPlayableAsset](https://docs.unity3d.com/Packages/com.unity.timeline@latest/api/UnityEngine.Timeline.AnimationPlayableAsset.html)
- [TimelineEditor](https://docs.unity3d.com/Packages/com.unity.timeline@latest/api/UnityEditor.Timeline.TimelineEditor.html)
- [SignalEmitter](https://docs.unity3d.com/Packages/com.unity.timeline@latest/api/UnityEngine.Timeline.SignalEmitter.html)
