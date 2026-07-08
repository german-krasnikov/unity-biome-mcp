using NUnit.Framework;
using UnityMCP.Editor;

// [SetUpFixture] without namespace = applies to entire assembly
[SetUpFixture]
public class TestAssemblySetup
{
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        CommandRegistry.InitDefaults();
        // Clean up debris from crashed previous runs
        UnityMCP.Editor.Tests.TestPaths.DeleteRoot();
    }

    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        UnityMCP.Editor.Tests.TestPaths.DeleteRoot();
    }
}
