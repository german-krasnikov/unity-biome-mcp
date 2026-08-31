using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityMCP.Editor;

namespace SourcePatchHarness
{
    // P1-20 CI qualification independent evidence writer. Fires on every
    // domain load (cold start and every Domain Reload within a process).
    // Never touches product code; reads only public UnityMCP.Editor.SyncHelper
    // members. Promoted from the local P0-80 evidence generator
    // (harness_files.py:instrumentation_cs), fixed to one CycleId because a
    // CI cell is single-use per Unity process — no cross-attempt SessionState
    // collision risk the way local repeated dev attempts had.
    //
    // Sits under Editor/ so Unity's implicit special-folder rule compiles it
    // into Assembly-CSharp-Editor: deliberately no custom .asmdef, matching
    // the proven P0-80 shape exactly.
    //
    // QueryOracle() exists because execute_code's dynamically-compiled
    // snippet assembly does not reference UnityMCP.Editor.dll. This class
    // lives in ordinary project code (Assembly-CSharp-Editor), which DOES
    // reference UnityMCP.Editor.dll normally, so routing the read through
    // this public static method sidesteps that boundary.
    [InitializeOnLoad]
    public static class CycleInstrumentation
    {
        private const string CycleId = "fsr-qualification";
        private const string TargetName = "SourcePatchHarnessTarget";
        private static readonly string CompileCountKey = "BiomeP120.CompileStartedCount." + CycleId;
        private static readonly string EvidenceDir;
        private static readonly string DomainLoadsPath;

        static CycleInstrumentation()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            EvidenceDir = Path.Combine(projectRoot, "Library", "UnityMCP", "FsrQualificationCell", CycleId);
            Directory.CreateDirectory(EvidenceDir);
            DomainLoadsPath = Path.Combine(EvidenceDir, "domain-loads.jsonl");

            EnsureTarget();
            WriteDomainLoadRecord("domain-load");

            CompilationPipeline.compilationStarted += _ => OnCompilationStarted();
        }

        private static void OnCompilationStarted()
        {
            var count = SessionState.GetInt(CompileCountKey, 0) + 1;
            SessionState.SetInt(CompileCountKey, count);
            AppendJsonLine(Path.Combine(EvidenceDir, "compile-events.jsonl"),
                "{\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\",\"event\":\"compilationStarted\",\"count\":" + count + "}");
        }

        // Single-call oracle for execute_code: retained-object identity +
        // ImplHash + current Compute() value (via the holder) + domain
        // stability signals + our own independent compile-started counter,
        // all in one round trip.
        public static string QueryOracle()
        {
            // Self-healing: a fresh package install cold start can run domain
            // reloads followed by a separate scene (re)load, all after
            // [InitializeOnLoad]'s one-shot EnsureTarget() call above. That
            // later scene load wipes any programmatically-created (unsaved)
            // GameObject, including the holder. Re-running EnsureTarget()
            // here makes every query idempotently self-healing regardless of
            // how many scene-load events happen after domain-load time.
            EnsureTarget();
            var go = GameObject.Find(TargetName);
            var holder = go != null ? go.GetComponent<SourcePatchHarnessHolder>() : null;
            return "instanceId=" + (holder != null ? holder.GetInstanceID().ToString() : "null") +
                "|compute=" + (holder != null ? holder.ComputeViaImpl().ToString() : "null") +
                "|implHash=" + (holder != null ? holder.ImplHash.ToString() : "null") +
                "|stamp=" + SyncHelper.CurrentDomainStamp +
                "|epoch=" + SyncHelper.CurrentEpoch +
                "|compiling=" + EditorApplication.isCompiling.ToString().ToLowerInvariant() +
                "|compileCount=" + SessionState.GetInt(CompileCountKey, 0);
        }

        private static void EnsureTarget()
        {
            var go = GameObject.Find(TargetName);
            if (go == null)
            {
                go = new GameObject(TargetName);
                go.AddComponent<SourcePatchHarnessHolder>();
            }
            else if (go.GetComponent<SourcePatchHarnessHolder>() == null)
            {
                go.AddComponent<SourcePatchHarnessHolder>();
            }
        }

        private static void WriteDomainLoadRecord(string evt)
        {
            var go = GameObject.Find(TargetName);
            var holder = go != null ? go.GetComponent<SourcePatchHarnessHolder>() : null;
            string[] fragments = { "roslyn", "codeanalysis", "harmony", "cecil", "monomod", "fastscriptreload" };
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var matches = assemblies.Where(a =>
            {
                string n;
                try { n = a.GetName().Name.ToLowerInvariant(); } catch { return false; }
                return fragments.Any(f => n.Contains(f));
            }).Select(a =>
            {
                string mvid = "";
                string loc = "";
                try { mvid = a.ManifestModule.ModuleVersionId.ToString(); } catch { }
                try { loc = a.IsDynamic ? "" : a.Location; } catch { }
                return "{\"name\":\"" + Escape(a.GetName().Name) + "\",\"version\":\"" + a.GetName().Version + "\",\"mvid\":\"" + mvid + "\",\"location\":\"" + Escape(loc) + "\"}";
            }).ToArray();

            var line = "{" +
                "\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"," +
                "\"event\":\"" + evt + "\"," +
                "\"pid\":" + System.Diagnostics.Process.GetCurrentProcess().Id + "," +
                "\"epoch\":" + SyncHelper.CurrentEpoch + "," +
                "\"stamp\":\"" + Escape(SyncHelper.CurrentDomainStamp) + "\"," +
                "\"targetInstanceId\":" + (holder != null ? holder.GetInstanceID().ToString() : "null") + "," +
                "\"compileStartedCount\":" + SessionState.GetInt(CompileCountKey, 0) + "," +
                "\"assemblies\":[" + string.Join(",", matches) + "]" +
                "}";
            AppendJsonLine(DomainLoadsPath, line);
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void AppendJsonLine(string path, string line)
        {
            File.AppendAllText(path, line + "\n");
        }
    }
}
