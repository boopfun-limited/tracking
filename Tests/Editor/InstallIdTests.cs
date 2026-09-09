using NUnit.Framework;
using Tracking;
using UnityEngine;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>跑在编辑器自己的 PlayerPrefs 上，前后都清掉这一个键，不碰别的。</summary>
    public sealed class InstallIdTests
    {
        [SetUp]
        public void ClearKey()
        {
            PlayerPrefs.DeleteKey(InstallId.Key);
        }

        [TearDown]
        public void ClearKeyAfter()
        {
            PlayerPrefs.DeleteKey(InstallId.Key);
        }

        [Test]
        public void FirstCallCreatesAStable32HexIdAndPersistsIt()
        {
            var first = InstallId.GetOrCreate();

            Assert.That(first, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(InstallId.GetOrCreate(), Is.EqualTo(first));
            Assert.That(PlayerPrefs.GetString(InstallId.Key), Is.EqualTo(first));
        }

        [Test]
        public void LosingTheKeyMeansANewId()
        {
            var first = InstallId.GetOrCreate();
            PlayerPrefs.DeleteKey(InstallId.Key);

            Assert.That(InstallId.GetOrCreate(), Is.Not.EqualTo(first));
        }
    }
}
