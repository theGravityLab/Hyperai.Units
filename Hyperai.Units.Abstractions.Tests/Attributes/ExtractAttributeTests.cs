using Hyperai.Units.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hyperai.Units.Abstractions.Tests.Attributes
{
    [TestClass]
    public class ExtractAttributeTests
    {
        [TestMethod]
        public void Ctor_GenerateNames()
        {
            // A & A
            ExtractAttribute attr = new ExtractAttribute("!at [hyper.at({who})]");
            // A
            Assert.IsTrue(attr.Names.SequenceEqual(new string[] { "who" }));
        }

        [TestMethod]
        public void Regex_Match()
        {
            // A & A
            ExtractAttribute attr = new ExtractAttribute("!ban {ban}");
            Match match = attr.Pattern.Match("!ban me!");
            // A
            Assert.IsTrue(match.Success);
        }
    }
}
