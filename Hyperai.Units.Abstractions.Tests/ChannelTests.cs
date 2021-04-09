using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperai.Units.Abstractions.Tests
{
    [TestClass]
    public class ChannelTests
    {
        [TestMethod]
        public void Equals_Null_Failed()
        {
            // A
            var channel = Channel.Create(1, 2);
            // A & A
            Assert.IsFalse(channel.Equals(null));
        }

        [TestMethod]
        public void Equals_Same_Success()
        {
            // A
            var channel = Channel.Create(1, 2);
            var other = Channel.Create(1, 2);
            // A & A
            Assert.AreEqual(other, channel);
        }

        [TestMethod]
        public void Equals_Different_Succuss()
        {
            // A
            var channel = Channel.Create(1, 1);
            var other = Channel.Create(1);
            // A & A
            Assert.AreNotEqual(other, channel);
        }
    }
}