using System;
using System.Collections.Generic;
using YCSS.Core.Models;

namespace YCSS.Core.Pipeline
{
    public class BuildContext
    {
        public Dictionary<string, TokenDefinition> Tokens { get; set; } = new();
        public Dictionary<string, ComponentDefinition> Components { get; set; } = new();
        public Dictionary<string, ComponentBaseDefinition> Styles { get; set; } = new();
        public string RawYaml { get; set; }
        public CompilerOptions Options { get; set; }

        public BuildContext(string yaml, CompilerOptions options)
        {
            RawYaml = yaml;
            Options = options;
        }

        public bool HasTokens => Tokens.Count > 0;
        public bool HasComponents => Components.Count > 0;
        public bool HasStyles => Styles.Count > 0;
        public int TotalStyleCount => Components.Count + Styles.Count;
    }
}
