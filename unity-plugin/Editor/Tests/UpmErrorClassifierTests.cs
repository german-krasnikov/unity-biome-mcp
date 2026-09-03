// TDD: UpmErrorClassifier — pure reason classification of UPM Client.Add
// failure messages (ARC-10 T2). No Unity API in production code; fixture
// still inherits UnityMcpTestBase per repo convention (ARC-0a 2.1).
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpmErrorClassifierTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Field report (ARC-10 §1): the raw message a consumer actually saw.
        // Contains no recognized marker — must classify as Unknown, not guess.
        private const string FieldReportMessage =
            "Unable to add package [https://github.com/acme/unity-biome-mcp.git?path=unity-plugin#v1.49.0]";

        private const string BusyMessage =
            "Unable to add package: this package is already being added, wait for it to complete.";

        private const string GitRefMessage =
            "Unable to add package [https://github.com/acme/unity-biome-mcp.git?path=unity-plugin#v1.49.0]: " +
            "fatal: couldn't find remote ref refs/tags/v1.49.0\n";

        private const string NetworkMessage =
            "fatal: unable to connect to github.com:\nfatal: Could not resolve host: github.com\n";

        // ARC-10 T3 review minor (b): a real curl SSL failure, matched by the
        // tightened "ssl certificate" marker.
        private const string SslCertificateMessage =
            "fatal: unable to access 'https://github.com/acme/unity-biome-mcp.git/': " +
            "SSL certificate problem: unable to get local issuer certificate";

        // ARC-10 T3 review minor (b): a real Unity API name that contains the bare
        // substring "ssl" (...ClaSSListt) without being network-related at all —
        // proves the old bare "ssl" marker was a false-positive trap.
        private const string SslFalsePositiveMessage =
            "NullReferenceException in VisualElement.AddToClassList during manifest UI refresh";

        [Test]
        public void Classify_BusyMessage_ReturnsUpmBusy()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.UpmBusy, UpmErrorClassifier.Classify(BusyMessage));
        }

        [Test]
        public void Classify_GitRefMessage_ReturnsGitRefMissing()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.GitRefMissing, UpmErrorClassifier.Classify(GitRefMessage));
        }

        [Test]
        public void Classify_NetworkMessage_ReturnsNetwork()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Network, UpmErrorClassifier.Classify(NetworkMessage));
        }

        [Test]
        public void Classify_SslCertificateMessage_ReturnsNetwork()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Network, UpmErrorClassifier.Classify(SslCertificateMessage));
        }

        [Test]
        public void Classify_MessageContainingSslSubstringInUnrelatedWord_ReturnsUnknown()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Unknown, UpmErrorClassifier.Classify(SslFalsePositiveMessage));
        }

        [Test]
        public void Classify_UnrecognizedMessage_ReturnsUnknown()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Unknown, UpmErrorClassifier.Classify(FieldReportMessage));
        }

        [Test]
        public void Classify_NullMessage_ReturnsUnknown()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Unknown, UpmErrorClassifier.Classify(null));
        }

        [Test]
        public void Classify_EmptyMessage_ReturnsUnknown()
        {
            Assert.AreEqual(UpmErrorClassifier.Reason.Unknown, UpmErrorClassifier.Classify(string.Empty));
        }

        [Test]
        public void ActionableText_GitRefMissing_ExactCopy()
        {
            var text = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.GitRefMissing, "1.49.0", GitRefMessage);
            Assert.AreEqual(
                "Version v1.49.0 was not found in the plugin repository yet. It may not be tagged — wait a few minutes and try again.",
                text);
        }

        [Test]
        public void ActionableText_UpmBusy_ExactCopy()
        {
            var text = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.UpmBusy, "1.49.0", BusyMessage);
            Assert.AreEqual(
                "Another plugin update is already in progress. Wait for it to finish, then try again.",
                text);
        }

        [Test]
        public void ActionableText_Network_ExactCopy()
        {
            var text = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.Network, "1.49.0", NetworkMessage);
            Assert.AreEqual(
                "Could not reach GitHub. Check your network connection and try again.",
                text);
        }

        // ARC-10 T3 review minors (a)/(c): Unknown must never paraphrase the raw
        // message away, but it must also never leave the user with zero guidance
        // (the field report's exact message classifies Unknown) and must never
        // return null. Renamed from ActionableText_Unknown_FallsBackToRawMessage
        // (DEV-40) — bare raw-only fallback was replaced by raw + generic hint.
        [Test]
        public void ActionableText_Unknown_MessageIncludesRawTextAndGenericHint()
        {
            var text = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.Unknown, "1.49.0", FieldReportMessage);
            Assert.AreEqual(
                FieldReportMessage + " Check that tag v1.49.0 exists in the plugin repository, " +
                "that no other UPM operation is in progress, and that your network connection is working.",
                text);
        }

        [Test]
        public void ActionableText_Unknown_NullMessage_ReturnsHintOnlyNeverNull()
        {
            var text = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.Unknown, "1.49.0", null);
            Assert.AreEqual(
                "Check that tag v1.49.0 exists in the plugin repository, " +
                "that no other UPM operation is in progress, and that your network connection is working.",
                text);
        }
    }
}
