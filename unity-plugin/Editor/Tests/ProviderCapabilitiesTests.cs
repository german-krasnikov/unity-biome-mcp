// TDD: RED — ProviderCapabilities.FromJson parses relay capabilities JSON.
// Pure NUnit — no Unity API deps.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProviderCapabilitiesTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── null/empty → Empty defaults ──────────────────────────────────────

        [Test]
        public void FromJson_Null_Returns_Empty()
        {
            var caps = ProviderCapabilities.FromJson(null);
            Assert.That(caps.ProviderId, Is.EqualTo(""));
            Assert.That(caps.HasResume, Is.False);
            Assert.That(caps.HasPlanMode, Is.False);
            Assert.That(caps.HasAgentMode, Is.False);
        }

        [Test]
        public void FromJson_Empty_Returns_Empty_Defaults()
        {
            var caps = ProviderCapabilities.FromJson("");
            Assert.That(caps.ProviderId, Is.EqualTo(""));
            Assert.That(caps.ProtocolVersion, Is.EqualTo("1.0"));
        }

        // ── provider_id parsed ───────────────────────────────────────────────

        [Test]
        public void FromJson_Parses_ProviderId()
        {
            var json = "{\"provider_id\":\"claude\",\"protocol_version\":\"2.0\",\"session\":{},\"permissions\":{},\"modes\":[]}";
            var caps = ProviderCapabilities.FromJson(json);
            Assert.That(caps.ProviderId, Is.EqualTo("claude"));
            Assert.That(caps.ProtocolVersion, Is.EqualTo("2.0"));
        }

        // ── has_resume from session object ───────────────────────────────────

        [Test]
        public void FromJson_Parses_HasResume_True()
        {
            var json = "{\"provider_id\":\"claude\",\"protocol_version\":\"2.0\",\"session\":{\"has_resume\":true},\"permissions\":{},\"modes\":[]}";
            var caps = ProviderCapabilities.FromJson(json);
            Assert.That(caps.HasResume, Is.True);
        }

        // ── supported modes array ─────────────────────────────────────────────

        [Test]
        public void FromJson_Parses_SupportedModes()
        {
            var json = "{\"provider_id\":\"claude\",\"protocol_version\":\"2.0\",\"session\":{},\"permissions\":{},\"modes\":[\"ask\",\"agent\"]}";
            var caps = ProviderCapabilities.FromJson(json);
            Assert.That(caps.SupportedModes, Has.Length.EqualTo(2));
            Assert.That(caps.SupportedModes, Does.Contain("ask"));
            Assert.That(caps.SupportedModes, Does.Contain("agent"));
        }

        // ── permissions.has_plan_mode ────────────────────────────────────────

        [Test]
        public void FromJson_Parses_HasPlanMode_From_Permissions()
        {
            var json = "{\"provider_id\":\"claude\",\"protocol_version\":\"2.0\",\"session\":{},\"permissions\":{\"has_plan_mode\":true,\"has_agent_mode\":true},\"modes\":[]}";
            var caps = ProviderCapabilities.FromJson(json);
            Assert.That(caps.HasPlanMode, Is.True);
            Assert.That(caps.HasAgentMode, Is.True);
        }
    }
}
