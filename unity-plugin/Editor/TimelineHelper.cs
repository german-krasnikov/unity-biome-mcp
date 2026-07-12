using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class TimelineHelper
    {
        private static readonly Dictionary<string, Type> TrackTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            {"Animation", typeof(AnimationTrack)},
            {"Audio", typeof(AudioTrack)},
            {"Activation", typeof(ActivationTrack)},
            {"Control", typeof(ControlTrack)},
            {"Signal", typeof(SignalTrack)},
            {"Group", typeof(GroupTrack)}
        };

        private static TrackAsset RequireTrack(TimelineAsset tl, string name)
        {
            var t = TimelineSerializer.FindTrack(tl, name);
            if (t == null) throw new InvalidOperationException($"Track not found: {name}");
            return t;
        }

        public static string CreateTimeline(string assetPath, string directorPath, string tracksStr)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("asset_path is required");

            AssetHelper.EnsureDirectory(assetPath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(timeline, assetPath);

            var sb = new StringBuilder();
            sb.Append("created: ").Append(assetPath);

            // Add tracks
            int trackCount = 0;
            if (!string.IsNullOrEmpty(tracksStr))
            {
                var trackDefs = tracksStr.Split(';');
                foreach (var def in trackDefs)
                {
                    var d = def.Trim();
                    if (string.IsNullOrEmpty(d)) continue;
                    var parts = d.Split(':');
                    var typeName = parts[0].Trim();
                    var trackName = parts.Length > 1 ? parts[1].Trim() : typeName;
                    AddTrack(timeline, typeName, trackName);
                    trackCount++;
                }
            }
            sb.Append(" | ").Append(trackCount).AppendLine(" tracks");

            // List created tracks
            foreach (var track in timeline.GetRootTracks())
                sb.Append("  [").Append(TimelineSerializer.TrackTypeName(track)).Append("] ").AppendLine(track.name);

            // Attach to PlayableDirector if specified
            if (!string.IsNullOrEmpty(directorPath))
            {
                var go = ComponentSerializer.FindObject(directorPath);
                if (go == null)
                    throw new InvalidOperationException(ErrorHelper.ObjectNotFound(directorPath));

                var director = go.GetComponent<PlayableDirector>();
                if (director == null)
                    director = Undo.AddComponent<PlayableDirector>(go);

                Undo.RecordObject(director, "Bind Timeline");
                director.playableAsset = timeline;
                EditorUtility.SetDirty(director);
                sb.Append("director: ").AppendLine(directorPath);
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return sb.ToString().TrimEnd();
        }

        public static string Edit(string path, string action, string trackName, string trackType,
            string clipName, string binding, float? start, float? duration, float? blendIn, float? blendOut,
            string name = null, int? index = null, float? offset = null, string value = null)
        {
            var (director, timeline) = TimelineSerializer.Resolve(path);
            Undo.RecordObject(timeline, "Edit Timeline");

            string result;
            switch (action)
            {
                case "add_track":
                    if (string.IsNullOrEmpty(trackType))
                        throw new ArgumentException("track_type is required for add_track");
                    var newTrack = AddTrack(timeline, trackType, trackName ?? trackType);
                    result = $"edited: add_track [{TimelineSerializer.TrackTypeName(newTrack)}] {newTrack.name}";
                    break;

                case "remove_track":
                    var trackToRemove = RequireTrack(timeline, trackName);
                    timeline.DeleteTrack(trackToRemove);
                    result = $"edited: remove_track {trackName}";
                    break;

                case "add_clip":
                    var trackForClip = RequireTrack(timeline, trackName);
                    var newClip = AddClipToTrack(trackForClip, clipName, start, duration);
                    var clipStart = newClip.start.ToString("F1", CultureInfo.InvariantCulture);
                    var clipEnd = (newClip.start + newClip.duration).ToString("F1", CultureInfo.InvariantCulture);
                    result = $"edited: add_clip [{TimelineSerializer.TrackTypeName(trackForClip)}] {trackName} | {newClip.displayName} {clipStart}-{clipEnd}s";
                    break;

                case "remove_clip":
                    var trackForRemove = RequireTrack(timeline, trackName);
                    RemoveClip(timeline, trackForRemove, clipName);
                    result = $"edited: remove_clip {clipName} from {trackName}";
                    break;

                case "set_binding":
                    if (director == null)
                        throw new InvalidOperationException("set_binding requires a PlayableDirector (use GO path, not asset path)");
                    var trackForBind = RequireTrack(timeline, trackName);
                    var bindGo = ComponentSerializer.FindObject(binding);
                    if (bindGo == null)
                        throw new InvalidOperationException(ErrorHelper.ObjectNotFound(binding));
                    director.SetGenericBinding(trackForBind, bindGo);
                    // Director change — no asset save needed
                    return $"edited: set_binding [{TimelineSerializer.TrackTypeName(trackForBind)}] {trackName} -> {binding}";

                case "set_timing":
                    var trackForTiming = RequireTrack(timeline, trackName);
                    SetClipTiming(trackForTiming, clipName, start, duration, blendIn, blendOut);
                    result = $"edited: set_timing {clipName} on {trackName}";
                    break;

                case "mute":
                case "unmute":
                    var trackForMute = RequireTrack(timeline, trackName);
                    trackForMute.muted = (action == "mute");
                    result = $"edited: {action} [{TimelineSerializer.TrackTypeName(trackForMute)}] {trackName}";
                    break;

                case "lock":
                case "unlock":
                    var trackForLock = RequireTrack(timeline, trackName);
                    trackForLock.locked = (action == "lock");
                    result = $"edited: {action} [{TimelineSerializer.TrackTypeName(trackForLock)}] {trackName}";
                    break;

                case "rename_track":
                    if (string.IsNullOrEmpty(name))
                        throw new ArgumentException("name is required for rename_track");
                    var trackToRename = RequireTrack(timeline, trackName);
                    trackToRename.name = name;
                    result = $"edited: rename_track {trackName} → {name}";
                    break;

                case "reorder_track":
                {
                    var reorderField = typeof(TimelineAsset).GetField("m_Tracks",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (reorderField == null)
                        throw new InvalidOperationException("m_Tracks field not found (Unity API changed)");
                    var reorderList = (IList)reorderField.GetValue(timeline);
                    var reorderTrack = RequireTrack(timeline, trackName);
                    int oldIdx = reorderList.IndexOf(reorderTrack);
                    reorderList.Remove(reorderTrack);
                    int newIdx = Math.Clamp(index ?? 0, 0, reorderList.Count);
                    reorderList.Insert(newIdx, reorderTrack);
                    result = $"reordered: {trackName} {oldIdx} → {newIdx}";
                    break;
                }

                case "duplicate_clip":
                {
                    var dupSrcTrack = RequireTrack(timeline, trackName);
                    var dupSrcClip = FindClip(dupSrcTrack, clipName);
                    var dupNewClip = dupSrcTrack.CreateDefaultClip();
                    dupNewClip.displayName = clipName + "_copy";
                    dupNewClip.start = dupSrcClip.start + (offset ?? dupSrcClip.duration);
                    dupNewClip.duration = dupSrcClip.duration;
                    dupNewClip.clipIn = dupSrcClip.clipIn;
                    result = $"duplicated: {clipName} → {dupNewClip.displayName} @ {dupNewClip.start}s";
                    break;
                }

                case "add_marker":
                {
                    var addMarkerTrack = RequireTrack(timeline, trackName);
                    var addedMarker = addMarkerTrack.CreateMarker<SignalEmitter>(start ?? 0f);
                    if (!string.IsNullOrEmpty(name))
                        ((UnityEngine.Object)addedMarker).name = name;
                    result = $"marker added: {((UnityEngine.Object)addedMarker).name} @ {addedMarker.time}s";
                    break;
                }

                case "remove_marker":
                {
                    var rmMarkerTrack = RequireTrack(timeline, trackName);
                    IMarker markerToRemove = null;
                    foreach (var m in rmMarkerTrack.GetMarkers())
                    {
                        bool nameMatch = name == null || (m is UnityEngine.Object mObj && mObj.name == name);
                        bool timeMatch = start == null || Mathf.Approximately((float)m.time, start.Value);
                        if (nameMatch && timeMatch) { markerToRemove = m; break; }
                    }
                    if (markerToRemove == null) throw new ArgumentException("Marker not found");
                    rmMarkerTrack.DeleteMarker(markerToRemove);
                    result = $"marker removed @ {markerToRemove.time}s";
                    break;
                }

                case "set_track_offset":
                {
                    var offsetTrackAsset = RequireTrack(timeline, trackName);
                    if (!(offsetTrackAsset is AnimationTrack animTrackForOffset))
                        throw new ArgumentException("set_track_offset only applies to AnimationTrack");
                    animTrackForOffset.trackOffset = (value ?? "auto") switch
                    {
                        "auto"      => TrackOffset.Auto,
                        "transform" => TrackOffset.ApplyTransformOffsets,
                        "scene"     => TrackOffset.ApplySceneOffsets,
                        _ => throw new ArgumentException($"Unknown offset mode: {value}. Valid: auto|transform|scene")
                    };
                    result = $"track offset: {trackName} = {value ?? "auto"}";
                    break;
                }

                case "set_duration":
                    if (duration.HasValue && duration.Value > 0)
                    {
                        timeline.fixedDuration = duration.Value;
                        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
                    }
                    else
                    {
                        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
                    }
                    result = $"duration: {(duration.HasValue ? $"{duration.Value}s fixed" : "auto")}";
                    break;

                case "add_sub_track":
                {
                    var subGroup = RequireTrack(timeline, trackName) as GroupTrack;
                    if (subGroup == null)
                        throw new ArgumentException($"{trackName} is not a GroupTrack");
                    if (string.IsNullOrEmpty(trackType) || !TrackTypes.TryGetValue(trackType, out var subTt))
                        throw new ArgumentException($"Unknown track type: {trackType}. Valid: Animation|Audio|Activation|Signal|Control");
                    var subNew = timeline.CreateTrack(subTt, subGroup, name ?? trackType);
                    result = $"sub-track added: {subNew.name} in group {trackName}";
                    break;
                }

                default:
                    throw new ArgumentException($"Unknown action: {action}");
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return result;
        }

        public static string Preview(string path, string action, float time)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var director = go.GetComponent<PlayableDirector>();
            if (director == null)
                throw new InvalidOperationException($"No PlayableDirector on: {path}");

            var timelineName = director.playableAsset != null ? director.playableAsset.name : "unknown";

            switch (action)
            {
                case "play":
                    director.time = time;
                    director.Play();
                    return $"preview: {timelineName} @ {time.ToString("F1", CultureInfo.InvariantCulture)}s | playing";
                case "stop":
                    director.Stop();
                    director.time = 0;
                    return $"preview: {timelineName} @ 0.0s | stopped";
                case "pause":
                    director.Pause();
                    return $"preview: {timelineName} @ {director.time.ToString("F1", CultureInfo.InvariantCulture)}s | paused";
                case "sample":
                default:
                    director.time = time;
                    director.Evaluate();
                    return $"preview: {timelineName} @ {time.ToString("F1", CultureInfo.InvariantCulture)}s | sampled";
            }
        }

        private static TrackAsset AddTrack(TimelineAsset timeline, string typeName, string trackName)
        {
            if (!TrackTypes.TryGetValue(typeName, out var trackType))
                throw new ArgumentException($"Unknown track type: {typeName}. Valid: Animation, Audio, Activation, Control, Signal, Group");

            var track = timeline.CreateTrack(trackType, null, trackName);
            return track;
        }

        private static TimelineClip AddClipToTrack(TrackAsset track, string clipRef, float? start, float? duration)
        {
            TimelineClip clip;

            if (track is AnimationTrack animTrack && !string.IsNullOrEmpty(clipRef))
            {
                // Try loading as animation clip asset
                var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipRef);
                if (animClip != null)
                {
                    clip = animTrack.CreateClip(animClip);
                }
                else
                {
                    clip = animTrack.CreateClip<AnimationPlayableAsset>();
                    clip.displayName = clipRef;
                }
            }
            else
            {
                clip = track.CreateDefaultClip();
                if (!string.IsNullOrEmpty(clipRef))
                    clip.displayName = clipRef;
            }

            if (start.HasValue) clip.start = start.Value;
            if (duration.HasValue) clip.duration = duration.Value;

            return clip;
        }

        private static void RemoveClip(TimelineAsset timeline, TrackAsset track, string clipName)
        {
            foreach (var clip in track.GetClips())
            {
                if (string.Equals(clip.displayName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    timeline.DeleteClip(clip);
                    return;
                }
            }
            throw new InvalidOperationException($"Clip not found: {clipName} on track {track.name}");
        }

        private static void SetClipTiming(TrackAsset track, string clipName, float? start, float? duration, float? blendIn, float? blendOut)
        {
            foreach (var clip in track.GetClips())
            {
                if (string.Equals(clip.displayName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    if (start.HasValue) clip.start = start.Value;
                    if (duration.HasValue) clip.duration = duration.Value;
                    if (blendIn.HasValue) clip.blendInDuration = blendIn.Value;
                    if (blendOut.HasValue) clip.blendOutDuration = blendOut.Value;
                    return;
                }
            }
            throw new InvalidOperationException($"Clip not found: {clipName} on track {track.name}");
        }

        public static string SetClipIn(string path, string trackName, string clipName, float clipIn)
        {
            if (clipIn < 0f)
                throw new ArgumentException("clip_in must be >= 0");
            var (_, timeline) = TimelineSerializer.Resolve(path);
            var track = RequireTrack(timeline, trackName);
            var clip = FindClip(track, clipName);
            Undo.RecordObject(timeline, "Set Clip In");
            clip.clipIn = (double)clipIn;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return $"set_clip_in: {clipName} clipIn:{clipIn.ToString("F2", CultureInfo.InvariantCulture)}s";
        }

        public static string GetBindings(string path)
        {
            var (director, timeline) = TimelineSerializer.Resolve(path);
            if (director == null)
                throw new InvalidOperationException("get_bindings requires a PlayableDirector (use GO path, not asset path)");
            var sb = new StringBuilder("bindings:");
            CollectBindings(director, timeline.GetRootTracks(), sb);
            return sb.ToString();
        }

        private static void CollectBindings(PlayableDirector director, System.Collections.Generic.IEnumerable<TrackAsset> tracks, StringBuilder sb)
        {
            foreach (var track in tracks)
            {
                var bound = director.GetGenericBinding(track) as UnityEngine.Object;
                string bindPath;
                if (bound == null) bindPath = "(unbound)";
                else if (bound is GameObject go) bindPath = ComponentSerializer.GetPath(go);
                else if (bound is Component comp) bindPath = ComponentSerializer.GetPath(comp.gameObject);
                else bindPath = bound.name;
                sb.Append("\n  [").Append(TimelineSerializer.TrackTypeName(track)).Append("] ")
                  .Append(track.name).Append(" -> ").Append(bindPath);
                CollectBindings(director, track.GetChildTracks(), sb);
            }
        }

        private static TimelineClip FindClip(TrackAsset track, string clipName)
        {
            foreach (var clip in track.GetClips())
            {
                if (string.Equals(clip.displayName, clipName, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
            throw new InvalidOperationException($"Clip not found: {clipName} on track {track.name}");
        }



    }
}
