// Pure-lane substitutes only for SessionState/event storage. No native Unity behavior is simulated.
using System;
using System.Collections.Generic;
namespace UnityEditor
{
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public static class EditorApplication { public static double timeSinceStartup = 100; }
    public enum ImportAssetOptions { ForceSynchronousImport }
    public static class AssetDatabase { public static void Refresh(ImportAssetOptions options = 0) { throw new NotSupportedException("native effect must be injected"); } }
    public static class EditorUtility { public static bool scriptCompilationFailed; }
    public static class AssemblyReloadEvents { public static event Action afterAssemblyReload; }
    public static class SessionState
    {
        private static readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public static int GetInt(string key,int fallback) => Values.TryGetValue(key,out var value) ? (int)value : fallback;
        public static float GetFloat(string key,float fallback) => Values.TryGetValue(key,out var value) ? (float)value : fallback;
        public static void SetFloat(string key,float value) => Values[key]=value;
        public static void EraseFloat(string key) => Values.Remove(key);
        public static bool GetBool(string key,bool fallback) => Values.TryGetValue(key,out var value) ? (bool)value : fallback;
        public static void SetBool(string key,bool value) => Values[key]=value;
        public static void EraseBool(string key) => Values.Remove(key);
        public static string GetString(string key,string fallback) => Values.TryGetValue(key,out var value) ? (string)value : fallback;
        public static void SetString(string key,string value) => Values[key]=value;
        public static void EraseString(string key) => Values.Remove(key);
    }
}
namespace UnityEditor.Compilation
{
    public enum RequestScriptCompilationOptions { None }
    public enum CompilerMessageType { Error, Warning }
    public struct CompilerMessage { public CompilerMessageType type; public string file; public int line; public int column; public string message; }
    public static class CompilationPipeline
    {
        public static void RequestScriptCompilation(RequestScriptCompilationOptions options) { throw new NotSupportedException("native effect must be injected"); }
        public static event Action<object> compilationStarted;
        public static event Action<object> compilationFinished;
        public static event Action<string, CompilerMessage[]> assemblyCompilationFinished;
    }
}

namespace UnityEngine
{
    public static class Debug { public static void Log(object value) { } public static void LogWarning(object value) { } }
}
namespace UnityMCP.Reload
{
    public static class ReloadDomainStamp { public static string ComputeStamp() => "stub:0"; }
    public static class ReloadDiagnoseCommand { public static string Execute(string args) => ""; }
    public static class ReloadBinder
    {
        public static (System.Net.Sockets.TcpListener, int) BindListener(int first,int last) =>
            throw new NotSupportedException("network must not run in pure tests");
    }
}
