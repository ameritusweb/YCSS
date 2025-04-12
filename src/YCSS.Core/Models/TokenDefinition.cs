using System;
using System.Collections.Generic;

namespace YCSS.Core.Models
{
    public class TokenDefinition
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> ThemeOverrides { get; set; } = new();
    }
}
