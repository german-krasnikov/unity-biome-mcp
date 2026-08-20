using System;

namespace McpFeedbackFixture
{
    [Serializable]
    public struct FixtureId : IEquatable<FixtureId>
    {
        [UnityEngine.SerializeField] int value;

        public FixtureId(int value) { this.value = value; }

        public static implicit operator FixtureId(int v) => new FixtureId(v);
        public static explicit operator int(FixtureId id) => id.value;

        public bool Equals(FixtureId other) => value == other.value;
        public override bool Equals(object obj) => obj is FixtureId id && Equals(id);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => $"FID-{value}";

        public static bool operator ==(FixtureId a, FixtureId b) => a.Equals(b);
        public static bool operator !=(FixtureId a, FixtureId b) => !a.Equals(b);
    }
}
