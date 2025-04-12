using System;
using System.Collections.Generic;

namespace YCSS.Core.Models
{
    public class StyleDefinition
    {
        public Dictionary<string, TokenDefinition> Tokens { get; set; } = new();
        public Dictionary<string, ComponentDefinition> Components { get; set; } = new();
        public Dictionary<string, ComponentBaseDefinition> StreetStyles { get; set; } = new();
    }
}