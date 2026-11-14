// PT-10: empty stand-in for UnityMCP.Editor.Testing.UnityMcpTestBase
// (unity-plugin/Editor/TestSupport/UnityMcpTestBase.cs) so the EditMode playtest
// test files below compile unchanged in this pure dotnet lane. The real base
// class owns Editor-only scene/asset ownership cleanup; the EditMode test files
// wired into the csproj's "Compile" items are pure PlaytestParser.Parse(...)
// tests that never touch Unity APIs, so an empty base is sufficient here.
namespace UnityMCP.Editor.Testing
{
    public abstract class UnityMcpTestBase
    {
    }
}
