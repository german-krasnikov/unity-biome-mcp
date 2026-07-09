# Feature: Timeline Support

## Overview

Phase 8 adds 1 consolidated MCP tool (`timeline`) with 4 actions (get|create|edit|preview) for Unity Timeline assets. Follows existing patterns: Python tool → bridge.send(cmd) → CommandRouter → TimelineSerializer/TimelineHelper → text response.

## Architecture (for Architect)

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     1 consolidated tool        CommandRouter (1 consolidated case)
                     get/create/edit/preview             │
                                              ┌──────────┼──────────┐
                                              │          │          │
                                        Serializer   Helper    MCPSettings
```

**Components:**
- **TimelineSerializer.cs** (216 lines): Read timeline → text tree (tracks, clips, bindings, markers)
- **TimelineHelper.cs** (433 lines): Create/edit/preview timelines via TimelineAsset + PlayableDirector API (v0.77: +161 lines for M1–M6)
- **CommandRouter.cs** (1053 lines): Consolidated tool case (timeline) with ExecTimelineConsolidated routing to Serializer/Helper
- **MCPSettings.cs**: Tool name registered (timeline)

**Data Flow:**
1. Python tool constructs args dict (path, track, action, etc.)
2. bridge.send() serializes to JSON, sends via TCP
3. CommandRouter.ExecuteCommand() unpacks args, calls appropriate Exec* method
4. Serializer reads timeline structure (RootTracks → OutputTracks → Clips + Markers)
5. Helper creates/modifies via TimelineAsset.CreateTrack(), SetBinding(), DeleteTrack()
6. PlayableDirector.time for preview (sample/play/stop/pause)
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

**Edit Sub-Actions (18):**
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
- preview — sample timeline at time T (requires time parameter; action=sample|start|stop)
- reorder_track — move track to index position (uses reflection on `m_Tracks`; only permitted reflection hack)
- duplicate_clip — duplicate clip on same track with time offset (requires track + clip name + optional offset)
- add_marker — add SignalEmitter marker at time on track (requires track + time)
- remove_marker — remove marker by time (requires track + time)
- set_track_offset — set track offset mode: `auto`, `transform`, or `scene`
- set_duration — set timeline asset total duration (seconds)
- add_sub_track — add sub-track to a GroupTrack (requires parent track name + track_type + name)

**Constraints:**
- asmdef must reference `Unity.Timeline` and `Unity.Timeline.Editor` (from UPM)
- All editor-only code (wrapped in Editor/ folder)
- Path resolution: "/" prefix = GameObject path, "Assets/" = asset path
- Binding stores GameObject reference (survives undo/scene changes if reference valid)
- TimelineAsset files have `.playable` extension
- Text output format optimized for token efficiency (~50 tokens for 4 tracks list vs ~300 JSON)

### Sub-Actions Flattening (Phase 16 Bug Fix)
- **Problem:** `timeline action=edit` was broken — sub-actions (add_track, remove_track, etc.) were passed as args but re-extracted, losing the actual sub-action
- **Solution:** Sub-actions now routed as top-level cases in CommandRouter.ExecTimelineConsolidated() switch statement
- **New pattern:** `action` param contains the sub-action directly (add_track|remove_track|add_clip|remove_clip|set_binding|set_timing|mute|unmute|lock|unlock)
- See `unity-plugin/Editor/CommandRouter.cs` ExecTimelineConsolidated() for implementation

**Edge Cases:**
- No PlayableDirector on GameObject → return error message with available GO path
- No TimelineAsset assigned → error message
- Track not found → case-insensitive search, return available track names
- Clip asset path invalid → validate exists before adding
- GroupTrack has no clips → serialize only child tracks
- Large timeline (50+ clips) → limit output to first 30, add "+N more" indicator
- Undo integration: `Undo.RecordObject(timeline, "Edit Timeline")` before modify

## Code Locations

- **Python**: `server/src/unity_mcp/tools/animation.py` (1 consolidated tool: timeline with get|create|edit|preview actions)
- **C#**:
  - `unity-plugin/Editor/TimelineSerializer.cs` (216 lines) — read timeline
  - `unity-plugin/Editor/TimelineHelper.cs` (272 lines) — create/edit/preview
  - `unity-plugin/Editor/CommandRouter.cs` (1053 lines) — ExecTimelineConsolidated case
- **Tests**:
  - `server/tests/test_server.py` — 8 Python timeline tests
  - `server/tests/test_server_edge_cases.py` — 6 timeline tests with sub-action passthrough (Phase 16)
  - `server/tests/test_server_timeline.py` — 10 Python tests for M1–M6 (reorder_track, duplicate_clip, add/remove_marker, set_track_offset, set_duration, add_sub_track)
  - `unity-test-project/Assets/Tests/Editor/MCPTimelineTests.cs` — 9 C# EditMode tests

## TDD Scenarios (for Developer)

### Red Phase (write failing tests first)

**Python Tests (8):**
1. `test_get_timeline_calls_bridge` — path only → bridge sends correct args
2. `test_get_timeline_with_track` — path + track → bridge includes track in args
3. `test_get_timeline_error` — ok=False → raises ToolError (Phase 21 refactor)
4. `test_create_timeline_calls_bridge` — asset_path + optional args → bridge sends all
5. `test_create_timeline_minimal` — asset_path only → bridge sends minimal args (no director/tracks)
6. `test_edit_timeline_calls_bridge` — path + action + optional args → bridge sends correct payload
7. `test_preview_timeline_calls_bridge` — path + action + time → bridge sends all args
8. `test_preview_timeline_defaults` — no action/time → defaults to "sample" + 0.0

**C# EditMode Tests (6):**
1. `CreateTimeline_CreatesAssetWithTracks` — create with 3 tracks → verify asset exists, tracks created, file saved
2. `GetTimeline_ListsTracksAndBindings` — create + bind tracks → get_timeline → output shows track list with bindings
3. `GetTimeline_TrackDetail_ShowsClips` — create + add clips → get_timeline(path, track) → output shows clip timing + blends
4. `EditTimeline_AddTrack_CreatesTrack` — create timeline → edit add_track → verify in get_timeline output
5. `EditTimeline_SetBinding_BindsTrack` — create + track + GO → edit set_binding → verify binding in output
6. `EditTimeline_MuteTrack_ShowsMuted` — create + track → edit mute → verify "muted" in output

### Green Phase (minimal implementation)

**Python tools/animation.py:**
- 1 consolidated `timeline()` function that unpacks optional args, calls bridge.send("timeline", ...), returns error or data

**C# TimelineSerializer.cs:**
- `Serialize(path, trackName)` — entry point, routes to list/detail
- `SerializeTrackList(director, timeline)` — text tree of all tracks + metadata
- `SerializeTrackDetail(director, timeline, trackName)` — clips + clip timing + markers
- `Resolve(path)` — GameObject path → PlayableDirector, or asset path → TimelineAsset

**C# TimelineHelper.cs:**
- `CreateTimeline(assetPath, directorPath, tracksStr)` — create asset, add tracks, attach if director specified
- `Edit(path, action, ...)` — dispatch by action, execute modifying operation
- `Preview(path, action, time)` — set director.time or call Play/Stop/Pause

**C# CommandRouter.cs:**
- 1 consolidated `ExecTimelineConsolidated` that switches on action → delegates to `ExecGetTimeline`, `ExecCreateTimeline`, `ExecEditTimeline`, `ExecPreviewTimeline`

### Refactor Phase

- Extract large Serializer/Helper methods if exceeding ~50 lines
- Consider caching track type mappings (currently in TimelineHelper.TrackTypes dict)
- Validate error messages are clear and actionable (e.g., "Track 'Char' not found. Available: Character, BGM")
- Ensure token efficiency in output format (verify ~50 tokens for 4 tracks)

## Review Checklist (for Reviewer)

- [ ] **Security**: asmdef references validated (Timeline package exists in manifest), no reflection exploits
- [ ] **Performance**: No expensive O(n²) loops on large timelines; clip output limited to 30 per track
- [ ] **Token efficiency**: Text output format tested (verify 50 tokens for 4 tracks, 300 for equivalent JSON)
- [ ] **Edge cases**: Empty timeline, no director, invalid paths, missing tracks/clips, binding to deleted GO — all handled with clear errors
- [ ] **Code organization**: Serializer reads only, Helper writes only; CommandRouter thin dispatcher
- [ ] **Testing**: All 14 Python + 9 C# tests pass; live TCP test with preview
- [ ] **File size**: TimelineSerializer < 250 lines, TimelineHelper < 300 lines
- [ ] **Undo integration**: Timeline modifications wrapped in Undo.RecordObject before changes
- [ ] **API correctness**: GetRootTracks() vs GetOutputTracks() used correctly; clip types per track (AnimClip vs AudioClip)

## Related

- Skill: `.claude/skills/csharp-unity.md` — Editor API patterns
- Knowledge: `AI/architecture.md` — System-wide architecture
