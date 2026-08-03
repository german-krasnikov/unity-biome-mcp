using NUnit.Framework;
using UnityMCP.Editor;

// [SetUpFixture] without namespace applies to the entire assembly.
[SetUpFixture]
public class TestAssemblySetup
{
    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        // The global UTF observer owns the scene transaction across every test
        // assembly. This assembly fixture must not replace or delete that scene.
        CommandRegistry.InitDefaults();
    }
}
