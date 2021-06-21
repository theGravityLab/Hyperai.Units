using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hyperai.Units.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExtractAttribute : Attribute
    {
        /// <summary>
        ///     创建一个模板用于匹配消息
        /// </summary>
        /// <param name="pattern">模板</param>
        /// <param name="trimSpaces">是否裁剪前后空格和合并二个空格为一个</param>
        public ExtractAttribute(string pattern, bool trimSpaces = false)
        {
            TrimSpaces = trimSpaces;
            RawString = pattern;
            var parameters = Regex.Matches(pattern, @"\{(?<name>[a-z0-9]+)\}");
            Names = parameters.Select(x => x.Groups["name"].Value).ToList();
            pattern = '^' + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\{", "{") + '$';
            pattern = Regex.Replace(pattern, @"\{([a-z0-9]+)\}", @"([\S]+)");
            Pattern = new Regex(pattern);
        }

        public Regex Pattern { get; }
        public string RawString { get; }
        public bool TrimSpaces { get; }
        public IList<string> Names { get; }
    }
}
