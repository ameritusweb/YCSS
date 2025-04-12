using Microsoft.Extensions.DependencyInjection;
using YCSS.Core.Analysis.Clustering;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Analysis;
using YCSS.Core.Caching;
using YCSS.Core.Compilation;
using YCSS.Core.Pipeline;
using YCSS.Core.Utils;
using YCSS.Core.Validation;
using Microsoft.Extensions.Logging;

namespace YCSS.Core.Test.Providers
{
    public static class TestServiceProvider
    {
        private static readonly Lazy<IServiceProvider> _serviceProvider = new(CreateServiceProvider);

        public static IServiceProvider Instance => _serviceProvider.Value;

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add YCSS services
            services.AddSingleton<YamlParser>();
            services.AddSingleton<IStylePipeline, StylePipeline>();
            services.AddSingleton<IStyleCompiler, StyleCompiler>();
            services.AddSingleton<IStyleValidator, StyleValidator>();
            services.AddSingleton<IPatternDetector, GeneralPatternDetector>();
            services.AddSingleton<IAnalysisCache, MemoryAnalysisCache>();
            services.AddSingleton<StyleAnalyzer>();
            services.AddSingleton<ClusterAnalyzer>();

            // Register any validators needed for StyleValidator
            services.AddSingleton<IEnumerable<IYamlValidator>>(sp => Array.Empty<IYamlValidator>());

            return services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : notnull
        {
            return Instance.GetRequiredService<T>();
        }
    }
}
