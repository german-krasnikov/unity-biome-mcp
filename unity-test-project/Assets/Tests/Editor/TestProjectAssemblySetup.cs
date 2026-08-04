using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

// [SetUpFixture] without namespace = applies to entire assembly
[SetUpFixture]
public class TestProjectAssemblySetup
{
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        CommandRegistry.InitDefaults();
    }
}
