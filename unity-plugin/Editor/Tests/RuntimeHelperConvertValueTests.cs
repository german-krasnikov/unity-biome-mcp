// TDD P-073: IArgumentConverter registry + Hash128/LayerMask built-ins + reflection Parse fallback.
// EditMode only — no TCP, no Play Mode required.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperConvertValueTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── helper types ──────────────────────────────────────────────────────

        // No Parse(string) method, not IConvertible → fail-closed scenario
        private struct NoParseType { }

        // Custom value object for RegisterConverter test
        private struct CustomValueType { public int Value; }

        private sealed class CustomValueConverter : IArgumentConverter
        {
            public bool CanConvert(Type targetType, string value) => targetType == typeof(CustomValueType);
            public object Convert(string value, Type targetType)
                => new CustomValueType { Value = int.Parse(value) };
        }

        [TearDown]
        public void TearDown() => RuntimeHelper.ResetConvertersForTesting();

        // ── Hash128Converter ──────────────────────────────────────────────────

        [Test]
        public void ConvertValue_Hash128WithPrefix_ReturnsHash128Instance()
        {
            var result = RuntimeHelper.ConvertValue("hash:abcd1234", typeof(Hash128));
            Assert.That(result, Is.InstanceOf<Hash128>());
        }

        [Test]
        public void ConvertValue_Hash128BareHex_ReturnsHash128Instance()
        {
            // Without "hash:" prefix — treated as raw hex
            var result = RuntimeHelper.ConvertValue("abcd1234", typeof(Hash128));
            Assert.That(result, Is.InstanceOf<Hash128>());
        }

        // ── LayerMaskConverter ────────────────────────────────────────────────

        [Test]
        public void ConvertValue_LayerMaskByName_ReturnsCorrectBitmask()
        {
            var result = RuntimeHelper.ConvertValue("Default", typeof(LayerMask));
            Assert.That(result, Is.InstanceOf<LayerMask>());
            // "Default" is layer 0 → bitmask 1
            Assert.That(((LayerMask)result).value, Is.EqualTo(1));
        }

        [Test]
        public void ConvertValue_LayerMaskRawInt_RoundTrips()
        {
            var result = RuntimeHelper.ConvertValue("5", typeof(LayerMask));
            Assert.That(result, Is.InstanceOf<LayerMask>());
            Assert.That(((LayerMask)result).value, Is.EqualTo(5));
        }

        // ── RegisterConverter ─────────────────────────────────────────────────

        [Test]
        public void RegisterConverter_CustomType_ConvertedCorrectly()
        {
            RuntimeHelper.RegisterConverter(new CustomValueConverter());
            var result = RuntimeHelper.ConvertValue("42", typeof(CustomValueType));
            Assert.That(result, Is.InstanceOf<CustomValueType>());
            Assert.That(((CustomValueType)result).Value, Is.EqualTo(42));
        }

        // ── Fail-closed ───────────────────────────────────────────────────────

        [Test]
        public void ConvertValue_UnknownTypeNoParseMethod_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.ConvertValue("test", typeof(NoParseType)));
            Assert.That(ex.Message, Does.Contain("Cannot convert"));
        }

        // ── Reflection Parse fallback ─────────────────────────────────────────

        [Test]
        public void ConvertValue_TypeWithStaticParse_ReflectionFallbackSucceeds()
        {
            // TimeSpan is not IConvertible → Convert.ChangeType fails
            // → reflection fallback calls TimeSpan.Parse("00:01:00")
            var result = RuntimeHelper.ConvertValue("00:01:00", typeof(TimeSpan));
            Assert.That(result, Is.InstanceOf<TimeSpan>());
            Assert.That(((TimeSpan)result).TotalSeconds, Is.EqualTo(60.0).Within(0.001));
        }
    }
}
