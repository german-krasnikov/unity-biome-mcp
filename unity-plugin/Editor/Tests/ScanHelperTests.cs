using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScanHelperTests : SceneTestBase
    {
        // Task 1: Scan_ReturnsHeaderAndCounts

        [Test]
        public void Scan_OutputHeader_ContainsTotalObjectCount()
        {
            TrackOwnedObject(new GameObject("ScanTest_A"));
            TrackOwnedObject(new GameObject("ScanTest_B"));

            var result = ScanHelper.Scan();

            StringAssert.StartsWith("SCAN:", result);
            StringAssert.Contains("2 objects", result);
        }

        [Test]
        public void Scan_ColliderComponent_ShowsColliderBand()
        {
            var go = TrackOwnedObject(new GameObject("ScanTest_Col"));
            go.AddComponent<BoxCollider>();

            var result = ScanHelper.Scan();

            StringAssert.Contains("colliders: 1", result);
        }

        [Test]
        public void Scan_LightComponent_ShowsLightBand()
        {
            var go = TrackOwnedObject(new GameObject("ScanTest_Light"));
            go.AddComponent<Light>();

            var result = ScanHelper.Scan();

            StringAssert.Contains("lights: 1", result);
        }

        [Test]
        public void Scan_AudioSourceAndRigidbody_ShowsAllBands()
        {
            var goAudio = TrackOwnedObject(new GameObject("ScanTest_Audio"));
            goAudio.AddComponent<AudioSource>();
            var goRb = TrackOwnedObject(new GameObject("ScanTest_Rb"));
            goRb.AddComponent<Rigidbody>();

            var result = ScanHelper.Scan();

            StringAssert.Contains("audio: 1", result);
            StringAssert.Contains("rigidbody: 1", result);
        }

        // Task 2: AppendBand_ZeroCount_Suppressed

        [Test]
        public void Scan_NoColliders_ColliderBandAbsent()
        {
            TrackOwnedObject(new GameObject("ScanTest_Plain1"));
            TrackOwnedObject(new GameObject("ScanTest_Plain2"));

            var result = ScanHelper.Scan();

            StringAssert.DoesNotContain("colliders", result);
        }

        [Test]
        public void Scan_NoAudioOrLights_AudioAndLightBandsSuppressed()
        {
            TrackOwnedObject(new GameObject("ScanTest_Plain3"));

            var result = ScanHelper.Scan();

            StringAssert.DoesNotContain("audio", result);
            StringAssert.DoesNotContain("lights", result);
        }
    }
}
