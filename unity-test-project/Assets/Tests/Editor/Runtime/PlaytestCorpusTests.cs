using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class PlaytestCorpusTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void CiSmokePlaytest_Parse_ContainsConsoleCleanAcceptance()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests/ci_smoke.playtest"));
            Assert.That(File.Exists(path), Is.True, "checked-in PlayTest corpus file is missing");

            var script = File.ReadAllText(path);
            var result = PlaytestParser.Parse(script);

            Assert.That(result.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(result.Exists(step => step.Type == StepType.AssertConsoleClean), Is.True);
        }
    }
}
