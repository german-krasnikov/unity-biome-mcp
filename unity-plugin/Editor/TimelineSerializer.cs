using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class TimelineSerializer
    {
        public static string Serialize(string path, string trackName)
        {
            var (director, timeline) = Resolve(path);
            if (timeline == null)
                throw new InvalidOperationException($"No TimelineAsset found at: {path}");

            if (!string.IsNullOrEmpty(trackName))
                return SerializeTrackDetail(director, timeline, trackName);
            return SerializeTrackList(director, timeline);
        }

        private static string SerializeTrackList(PlayableDirector director, TimelineAsset timeline)
        {
            var sb = new StringBuilder();
            sb.Append("Timeline: ").Append(timeline.name);
            sb.Append(" | ").Append(timeline.duration.ToString("F1", CultureInfo.InvariantCulture)).Append("s");

            int trackCount = 0;
            foreach (var _ in timeline.GetOutputTracks()) trackCount++;
            sb.Append(" | ").Append(trackCount).AppendLine(" tracks");

            if (director != null)
                sb.Append("director: /").AppendLine(director.gameObject.name);
            else
                sb.AppendLine("director: (none)");

            sb.AppendLine("---");

            foreach (var track in timeline.GetRootTracks())
                AppendTrackSummary(sb, track, director);

            return sb.ToString().TrimEnd();
        }

        private static void AppendTrackSummary(StringBuilder sb, TrackAsset track, PlayableDirector director)
        {
            // For GroupTrack, show group and children
            if (track is GroupTrack)
            {
                sb.Append("[Group] ").AppendLine(track.name);
                foreach (var child in track.GetChildTracks())
                    AppendTrackSummary(sb, child, director);
                return;
            }

            sb.Append("[").Append(TrackTypeName(track)).Append("] ").Append(track.name);

            // Binding
            if (director != null)
            {
                var bound = director.GetGenericBinding(track);
                if (bound != null)
                {
                    if (bound is GameObject go)
                        sb.Append(" | bound: /").Append(go.name);
                    else if (bound is Component comp)
                        sb.Append(" | bound: /").Append(comp.gameObject.name);
                    else
                        sb.Append(" | bound: ").Append(bound.name);
                }
                else
                    sb.Append(" | unbound");
            }
            else
                sb.Append(" | unbound");

            // Clip count
            int clipCount = 0;
            foreach (var _ in track.GetClips()) clipCount++;
            sb.Append(" | ").Append(clipCount).Append(clipCount == 1 ? " clip" : " clips");

            // Flags
            if (track.muted) sb.Append(" | muted");
            if (track.locked) sb.Append(" | locked");

            sb.AppendLine();

            // Inline clip listing (capped at 10 to keep tokens tight)
            int ci = 0;
            foreach (var clip in track.GetClips())
            {
                if (ci >= 10) { sb.AppendLine("  ..."); break; }
                sb.Append("  ");
                AppendClip(sb, clip);
                ci++;
            }
        }

        private static string SerializeTrackDetail(PlayableDirector director, TimelineAsset timeline, string trackName)
        {
            var track = FindTrack(timeline, trackName);
            if (track == null)
                throw new InvalidOperationException($"Track not found: {trackName}");

            var sb = new StringBuilder();
            sb.Append("[").Append(TrackTypeName(track)).Append("] ").Append(track.name);

            if (director != null)
            {
                var bound = director.GetGenericBinding(track);
                if (bound != null)
                {
                    if (bound is Component comp)
                        sb.Append(" | bound: /").Append(comp.gameObject.name);
                    else if (bound is GameObject go)
                        sb.Append(" | bound: /").Append(go.name);
                }
                else
                    sb.Append(" | unbound");
            }
            sb.AppendLine();
            sb.AppendLine("---");

            // Clips
            int clipIdx = 0;
            foreach (var clip in track.GetClips())
            {
                if (clipIdx >= 30)
                {
                    int remaining = 0;
                    foreach (var _ in track.GetClips()) remaining++;
                    sb.Append("...+").Append(remaining - 30).AppendLine(" more");
                    break;
                }
                AppendClip(sb, clip);
                clipIdx++;
            }

            // Markers
            bool hasMarkers = false;
            foreach (var marker in track.GetMarkers())
            {
                if (!hasMarkers)
                {
                    sb.AppendLine("---");
                    sb.AppendLine("markers:");
                    hasMarkers = true;
                }
                sb.Append("  ").Append(marker.time.ToString("F1", CultureInfo.InvariantCulture)).Append("s: ");
                sb.Append(marker.GetType().Name);
                if (marker is SignalEmitter se && se.asset != null)
                    sb.Append(" \"").Append(se.asset.name).Append("\"");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendClip(StringBuilder sb, TimelineClip clip)
        {
            var start = clip.start.ToString("F1", CultureInfo.InvariantCulture);
            var end = (clip.start + clip.duration).ToString("F1", CultureInfo.InvariantCulture);
            sb.Append(start).Append("-").Append(end).Append("s: ").Append(clip.displayName);

            var blends = new StringBuilder();
            if (clip.blendInDuration > 0)
                blends.Append("blend-in: ").Append(clip.blendInDuration.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
            if (clip.blendOutDuration > 0)
            {
                if (blends.Length > 0) blends.Append(", ");
                blends.Append("blend-out: ").Append(clip.blendOutDuration.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
            }
            if (blends.Length > 0)
                sb.Append(" (").Append(blends).Append(")");

            sb.AppendLine();
        }

        internal static (PlayableDirector director, TimelineAsset timeline) Resolve(string path)
        {
            // Try as GameObject path first
            var go = ComponentSerializer.FindObject(path);
            if (go != null)
            {
                var director = go.GetComponent<PlayableDirector>();
                if (director != null && director.playableAsset is TimelineAsset ta)
                    return (director, ta);
                throw new InvalidOperationException($"No PlayableDirector with TimelineAsset on: {path}");
            }

            // Try as asset path
            var asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (asset != null)
                return (null, asset);

            throw new InvalidOperationException($"Not found (GO or asset): {path}");
        }

        internal static string TrackTypeName(TrackAsset track)
        {
            var name = track.GetType().Name;
            // Remove "Track" suffix: "AnimationTrack" -> "Animation"
            if (name.EndsWith("Track"))
                return name.Substring(0, name.Length - 5);
            return name;
        }

        internal static TrackAsset FindTrack(TimelineAsset timeline, string name)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (string.Equals(track.name, name, StringComparison.OrdinalIgnoreCase))
                    return track;
            }
            // Also search root tracks (includes GroupTrack)
            foreach (var track in timeline.GetRootTracks())
            {
                if (string.Equals(track.name, name, StringComparison.OrdinalIgnoreCase))
                    return track;
            }
            return null;
        }
    }
}
