# Feature: Timeline Support

## Overview

The consolidated `timeline` MCP tool reads and authors Unity Timeline assets,
track/clip structure, bindings, markers, timing, and preview samples. Python
forwards the request through the bridge; `TimelineSerializer` owns reads and
`TimelineHelper` owns mutations and evaluation.

## Architecture (for Architect)

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     timeline tool              CommandRouter.MediaHandlers
                                                        │
                                              ┌──────────┼──────────┐
                                              │          │          │
                                        Serializer   Helper    MCPSettings
```

**Components:**
- **TimelineSerializer.cs**: Read timeline → text tree (tracks, clips, bindings, markers)
- **TimelineHelper.cs**: Create/edit/sample timelines via TimelineAsset + PlayableDirector API
- **CommandRouter.MediaHandlers.cs**: `ExecTimelineConsolidated` action dispatch
- **MCPSettings.cs**: Tool name registered (timeline)

**Data Flow:**
1. Python tool constructs args dict (path, track, action, etc.)
2. bridge.send() serializes to JSON, sends via TCP
3. CommandRouter.ExecuteCommand() unpacks args, calls appropriate Exec* method
4. Serializer reads timeline structure (RootTracks → OutputTracks → Clips + Markers)
5. Helper creates/modifies via TimelineAsset.CreateTrack(), SetBinding(), DeleteTrack()
6. Public `preview` sets `PlayableDirector.time` and evaluates one sample; helper
   play/stop/pause branches are not separate public actions
7. Response as compact text (not JSON) to save tokens

**Timeline Structure:**
- TimelineAsset: Root container (has duration, tracks, markers)
- PlayableDirector: Scene component that plays TimelineAsset, stores bindings
- TrackAsset: 6 types (Animation, Audio, Activation, Control, Signal, Group)
- TimelineClip: Playable clip on track (start, duration, blends, asset)
- Markers: Events on tracks (e.g., SignalEmitter for event signals)

## Implementation Notes (for Developer)

**Key APIs used:**
- `TimelineAsset.GetRootTracks()` / `GetOutputTracks()` — iterate tracks (RootTracks excludes nested, OutputTracks flattens)
- `TrackAsset.GetClips()` — iterate clips on track
- `TrackAsset.GetMarkers()` — iterate markers
- `timeline.CreateTrack<T>(parent, name)` — add track of type T
- `track.CreateClip<T>()` or `((AnimationTrack)track).CreateClip(animClip)` — add clip
- `timeline.DeleteTrack(track)` — remove track
- `track.DeleteClip(clip)` — remove clip
- `director.SetGenericBinding(track, targetObject)` — bind track to scene object
- `director.GetGenericBinding(track)` — get binding
- `director.time = value; director.Evaluate()` — sample timeline at time
- `director.Play() / Stop() / Pause()` — control playback
- `TimelineEditor.Refresh(RefreshReason.ContentsModified)` — update editor UI
- `EditorUtility.SetDirty(timeline)` — mark dirty for save
- `AssetDatabase.SaveAssets()` — persist changes

**Track Types (6):**
1. AnimationTrack — clips are AnimationClip, binds to Animator
2. AudioTrack — clips are AudioClip, binds to AudioSource
3. ActivationTrack — clips have no asset, activate/deactivate GameObject
4. ControlTrack — controls nested PlayableDirector playback
5. SignalTrack — emits signals/events (no binding)
6. GroupTrack — container (no clips, has child tracks)

**Edit actions:**
- add_track — create track (requires track_type + track name)
- remove_track — delete track
- add_clip — add clip to track (track + clip path required; start/duration optional)
- remove_clip — delete clip
- set_binding — bind track to GameObject (track + binding=GO path)
- set_timing — change clip timing (start, duration, blend_in, blend_out)
- mute — set track muted
- unmute — unset track muted
- lock — set track locked
- unlock — unset track locked
- rename_track — rename an existing track (`track`, `name`)
- reorder_track — move track to index position (uses reflection on `m_Tracks`; only permitted reflection hack)
- duplicate_clip — duplicate clip on same track with time offset (requires track + clip name + optional offset)
- add_marker — add SignalEmitter marker at time on track (requires track + time)
- remove_marker — remove marker by time (requires track + time)
- set_track_offset — set track offset mode: `auto`, `transform`, or `scene`
- set_duration — set timeline asset total duration (seconds)
- add_sub_track — add sub-track to a GroupTrack (requires parent track name + track_type + name)

`set_clip_in` changes a clip's source offset, `get_bindings` returns current
director bindings, and `preview` evaluates the director at `time`. Although the
C# helper contains play/stop/pause branches, the public action dispatcher exposes
`preview` as sampling; use the `editor` tool for Play Mode control.

**Constraints:**
- asmdef must reference `Unity.Timeline` and `Unity.Timeline.Editor` (from UPM)
- All editor-only code (wrapped in Editor/ folder)
- Path resolution: "/" prefix = GameObject path, "Assets/" = asset path
- Binding stores GameObject reference (survives undo/scene changes if reference valid)
- TimelineAsset files have `.playable` extension
- Text output format optimized for token efficiency (~50 tokens for 4 tracks list vs ~300 JSON)

### Action Dispatch

Edit operations are passed directly as the top-level `action`. The dispatcher
still accepts `action="edit"`, but the helper has no generic edit operation, so
public callers must use a concrete action. See
`unity-plugin/Editor/CommandRouter.MediaHandlers.cs` for the authoritative list.

**Edge Cases:**
- No PlayableDirector on GameObject → return error message with available GO path
- No TimelineAsset assigned → error message
- Track not found → case-insensitive search, return available track names
- Clip asset path invalid → validate exists before adding
- GroupTrack has no clips → serialize only child tracks
- Large timeline (50+ clips) → limit output to first 30, add "+N more" indicator
- Undo integration: `Undo.RecordObject(timeline, "Edit Timeline")` before modify

## Code Locations

- **Python**: `server/src/unity_mcp/tools/animation.py` (public `timeline` wrapper)
- **C#**:
  - `unity-plugin/Editor/TimelineSerializer.cs` — read timeline
  - `unity-plugin/Editor/TimelineHelper.cs` — create/edit/sample
  - `unity-plugin/Editor/CommandRouter.MediaHandlers.cs` — action dispatch
- **Tests**:
  - `server/tests/test_server_timeline.py`
  - `unity-plugin/Editor/Tests/SerializerTests.cs`
  - `unity-plugin/Editor/Tests/HelperTests.cs`

## Tests

- `server/tests/test_server_timeline.py` verifies Python wrapper actions and
  argument forwarding.
- `unity-plugin/Editor/Tests/SerializerTests.cs` and `HelperTests.cs` cover the
  Unity serializer/helper behavior.
- Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.

## Review Checklist (for Reviewer)

- [ ] **Security**: asmdef references validated (Timeline package exists in manifest), no reflection exploits
- [ ] **Performance**: No expensive O(n²) loops on large timelines; clip output limited to 30 per track
- [ ] **Token efficiency**: Large track and clip lists remain bounded
- [ ] **Edge cases**: Empty timeline, no director, invalid paths, missing tracks/clips, binding to deleted GO — all handled with clear errors
- [ ] **Code organization**: Serializer reads only, Helper writes only; CommandRouter thin dispatcher
- [ ] **Testing**: Focused Python and C# suites pass; preview has live evidence when behavior changes
- [ ] **Undo integration**: Timeline modifications wrapped in Undo.RecordObject before changes
- [ ] **API correctness**: GetRootTracks() vs GetOutputTracks() used correctly; clip types per track (AnimClip vs AudioClip)

## Related

- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-animation/references/timeline.md`
- Knowledge: [`AI/architecture.md`](architecture.md) — system-wide architecture
- Testing: [`AI/testing.md`](testing.md) — commands and acceptance policy
