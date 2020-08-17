using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperai.Units.Abstractions.Tests
{
    [TestClass]
    public class ChannelTests
    {
        [TestMethod]
        public void Equals_Null_Failed()
        {
            // A
            Channel channel = Channel.Create(1, 2);
            // A & A
            Assert.IsFalse(channel.Equals(null));
        }

        [TestMethod]
        public void Equals_Same_Success()
        {
            // A
            Channel channel = Channel.Create(1, 2);
            Channel other = Channel.Create(1, 2);
            // A & A
            Assert.AreEqual(other, channel);
        }

        [TestMethod]
        public void Equals_Different_Succuss()
        {
            // A
            Channel channel = Channel.Create(1, 1);
            Channel other = Channel.Create(1, null);
            // A & A
            Assert.AreNotEqual(other, channel);
        }
    }
}
