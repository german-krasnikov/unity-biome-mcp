// T-6.2: Disk-based agent mention source.
// Scans all ancestor .claude/agents/ dirs (nearest-first) via AgentSearchPath.Resolve,
// then homeDir/.claude/agents. Pure IO — zero UnityEngine deps. NUnit-testable.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AgentMentionSource : IMentionSource
    {
        private readonly string[] _roots;
        private readonly Action   _onScan;   // optional hook for scan-count testing

        private List<(string stem, string displayName)> _agents =
            new List<(string, string)>();
        private long _lastScanMtime  = long.MinValue;  // sentinel: never scanned
        private long _lastCheckTicks = 0;              // DateTime.UtcNow.Ticks of last check
        private const long CooldownTicks = 10_000_000L; // 1 second in 100-ns ticks

        internal AgentMentionSource(string projectRoot, string homeDir, Action onScan = null)
        {
            _roots  = AgentSearchPath.Resolve(projectRoot, homeDir).ToArray();
            _onScan = onScan;
        }

        // IMentionSource ─────────────────────────────────────────────────────

        public void RefreshIfDirty()
        {
            long now = DateTime.UtcNow.Ticks;
            // Skip mtime check if within cooldown, UNLESS never scanned yet.
            if (_lastScanMtime != long.MinValue && now - _lastCheckTicks < CooldownTicks)
                return;
            _lastCheckTicks = now;

            var maxMtime = GetMaxMtime();
            if (maxMtime == _lastScanMtime) return;  // unchanged
            _lastScanMtime = maxMtime;
            Scan();
        }

        public void Search(string query, int maxResults, List<MentionCandidate> results)
        {
            if (string.IsNullOrEmpty(query)) return;
            var lower = query.ToLowerInvariant();
            var qmask = MentionFuzzyScorer.BuildCharMask(lower);

            foreach (var (stem, displayName) in _agents)
            {
                if (results.Count >= maxResults) break;
                var nameLower = stem.ToLowerInvariant();
                var mask      = MentionFuzzyScorer.BuildCharMask(nameLower);
                if (!MentionFuzzyScorer.PassesPreFilter(qmask, mask)) continue;
                var score = MentionFuzzyScorer.Score(lower, nameLower, stem);
                if (score <= 0) continue;
                var chip = new ChipData(ChipKindKeys.Agent, stem, displayName, 0);
                results.Add(new MentionCandidate(chip, score, "d_cs Script Icon"));
            }
        }

        // ── private ─────────────────────────────────────────────────────────

        private long GetMaxMtime()
        {
            long max = -1;
            // _roots entries are already full ".claude/agents" paths from AgentSearchPath.Resolve
            foreach (var dir in _roots)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.md"); }
                catch { continue; }

                foreach (var f in files)
                {
                    try
                    {
                        var t = File.GetLastWriteTimeUtc(f).Ticks;
                        if (t > max) max = t;
                    }
                    catch { /* skip */ }
                }
            }
            return max;
        }

        private void Scan()
        {
            _onScan?.Invoke();
            _agents.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // _roots entries are already full ".claude/agents" paths from AgentSearchPath.Resolve
            foreach (var dir in _roots)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.md"); }
                catch { continue; }

                foreach (var path in files)
                {
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (!seen.Add(stem)) continue;  // first dir wins on collision

                    string text;
                    try { text = File.ReadAllText(path); } catch { text = ""; }
                    var displayName = AgentFrontmatterParser.ParseName(text, stem);
                    _agents.Add((stem, displayName));
                }
            }
        }
    }
}
