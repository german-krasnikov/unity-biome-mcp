// TDD RED: WatchCondition unit tests — no Unity API, pure logic.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchConditionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void EmptyCondition_ReturnsFalse() =>
            Assert.IsFalse(WatchCondition.Evaluate("", 5f));

        [Test] public void NullCondition_ReturnsFalse() =>
            Assert.IsFalse(WatchCondition.Evaluate(null, 5f));

        [Test] public void InvalidFormat_ReturnsFalse() =>
            Assert.IsFalse(WatchCondition.Evaluate("10 <", 5f));

        [Test] public void LessThan_TrueWhenLess() =>
            Assert.IsTrue(WatchCondition.Evaluate("< 10", 8f));

        [Test] public void LessThan_FalseWhenEqual() =>
            Assert.IsFalse(WatchCondition.Evaluate("< 10", 10f));

        [Test] public void LessThan_FalseWhenGreater() =>
            Assert.IsFalse(WatchCondition.Evaluate("< 10", 15f));

        [Test] public void GreaterThan_TrueWhenGreater() =>
            Assert.IsTrue(WatchCondition.Evaluate("> 5", 10f));

        [Test] public void GreaterThan_FalseWhenLess() =>
            Assert.IsFalse(WatchCondition.Evaluate("> 5", 3f));

        [Test] public void LessOrEqual_TrueWhenEqual() =>
            Assert.IsTrue(WatchCondition.Evaluate("<= 10", 10f));

        [Test] public void LessOrEqual_TrueWhenLess() =>
            Assert.IsTrue(WatchCondition.Evaluate("<= 10", 9f));

        [Test] public void GreaterOrEqual_TrueWhenEqual() =>
            Assert.IsTrue(WatchCondition.Evaluate(">= 5", 5f));

        [Test] public void EqualOp_TrueWhenMatch() =>
            Assert.IsTrue(WatchCondition.Evaluate("== 3", 3f));

        [Test] public void EqualOp_FalseWhenNoMatch() =>
            Assert.IsFalse(WatchCondition.Evaluate("== 3", 4f));

        [Test] public void NotEqual_TrueWhenDifferent() =>
            Assert.IsTrue(WatchCondition.Evaluate("!= 0", 1f));

        [Test] public void NotEqual_FalseWhenSame() =>
            Assert.IsFalse(WatchCondition.Evaluate("!= 5", 5f));

        [Test] public void EqualNull_TrueWhenNull() =>
            Assert.IsTrue(WatchCondition.Evaluate("== null", null));

        [Test] public void EqualNull_FalseWhenNotNull() =>
            Assert.IsFalse(WatchCondition.Evaluate("== null", "value"));

        [Test] public void NotEqualNull_TrueWhenNotNull() =>
            Assert.IsTrue(WatchCondition.Evaluate("!= null", "value"));

        [Test] public void NotEqualNull_FalseWhenNull() =>
            Assert.IsFalse(WatchCondition.Evaluate("!= null", null));

        [Test] public void StringEquality_CaseInsensitive() =>
            Assert.IsTrue(WatchCondition.Evaluate("== True", "true"));

        [Test] public void IntValue_ComparesAsFloat() =>
            Assert.IsTrue(WatchCondition.Evaluate("< 10", 5));
    }
}
