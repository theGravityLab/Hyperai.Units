using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hyperai.Units.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExtractAttribute : Attribute
    {
        public Regex Pattern { get; private set; }
        public string RawString { get; private set; }
        public IList<string> Names { get; private set; }

        public ExtractAttribute(string pattern)
        {
            RawString = pattern;
            MatchCollection parameters = Regex.Matches(pattern, @"\{(?<name>[a-z0-9]+)\}");
            Names = parameters.Select(x => x.Groups["name"].Value).ToList();
            pattern = '^' + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\{", "{") + '$';
            pattern = Regex.Replace(pattern, @"\{([a-z0-9]+)\}", @"([\S]+)");
            Pattern = new Regex(pattern);
        }
    }
}