using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Cli.Common
{
    public static class CommonOptions
    {
        public static readonly Option<bool> Verbose = new(
        aliases: new[] { "--verbose", "-v" },
        description: "Show detailed output"
    );

        public static readonly Option<bool> NoColor = new(
            name: "--no-color",
            description: "Disable colored output"
        );

        public static readonly Option<FileInfo?> ConfigFile = new(
            aliases: new[] { "--config", "-c" },
            description: "Path to YCSS config file"
        );

        public static readonly Option<FileInfo> InputFile = new(
            aliases: new[] { "--file", "-f" },
            description: "Input YAML file")
        {
            IsRequired = true
        };

        public static readonly Option<FileInfo?> OutputFile = new(
            aliases: new[] { "--out", "-o" },
            description: "Output file path"
        );

        public static readonly Option<string> Format = new(
            aliases: new[] { "--format" },
            getDefaultValue: () => "css",
            description: "Output format (css, scss, tailwind, tokens)"
        );

        public static readonly Option<bool> Watch = new(
            aliases: new[] { "--watch", "-w" },
            description: "Watch for file changes"
        );

        public static readonly Option<bool> Minify = new(
            aliases: new[] { "--minify", "-m" },
            description: "Minify output"
        );

        public static readonly Option<string?> Theme = new(
            aliases: new[] { "--theme", "-t" },
            description: "Theme to apply"
        );
    }
}
