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
        public bool TrimSpaces { get; private set; }
        public IList<string> Names { get; private set; }

        /// <summary>
        /// 创建一个模板用于匹配消息
        /// </summary>
        /// <param name="pattern">模板</param>
        /// <param name="trimSpaces">是否裁剪前后空格和合并二个空格为一个</param>
        public ExtractAttribute(string pattern, bool trimSpaces = false)
        {
            TrimSpaces = trimSpaces;
            RawString = pattern;
            MatchCollection parameters = Regex.Matches(pattern, @"\{(?<name>[a-z0-9]+)\}");
            Names = parameters.Select(x => x.Groups["name"].Value).ToList();
            pattern = '^' + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\{", "{") + '$';
            pattern = Regex.Replace(pattern, @"\{([a-z0-9]+)\}", @"([\S]+)");
            Pattern = new Regex(pattern);
        }
    }
}