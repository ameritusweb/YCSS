using Microsoft.Extensions.DependencyInjection;
using YCSS.Core.Analysis;
using YCSS.Core.Analysis.Clustering;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Caching;
using YCSS.Core.Compilation;
using YCSS.Core.Utils;
using YCSS.Core.Validation;

namespace YCSS.Core.Pipeline
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddYCSS(this IServiceCollection services, Action<YCSSBuilder>? configure = null)
        {
            var builder = new YCSSBuilder(services);
            configure?.Invoke(builder);

            // Core services
            services.AddSingleton<YamlParser>();
            services.AddSingleton<IStylePipeline, StylePipeline>();
            services.AddSingleton<IStyleCompiler, StyleCompiler>();
            services.AddSingleton<IStyleValidator, StyleValidator>();
            services.AddSingleton<IAnalysisCache, MemoryAnalysisCache>();

            // Analysis services
            services.AddSingleton<GeneralPatternDetector>();
            services.AddSingleton<HierarchicalPatternDetector>();
            services.AddSingleton<BEMAnalyzer>();
            services.AddSingleton<IClusterAnalyzer, ClusterAnalyzer>();
            services.AddSingleton<StyleMetrics>();
            services.AddSingleton<PerformanceAnalyzer>();
            services.AddSingleton<StyleAnalyzer>();

            return services;
        }
    }

    public class YCSSBuilder
    {
        private readonly IServiceCollection _services;

        public YCSSBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public YCSSBuilder ConfigureAnalysis(Action<AnalysisOptions> configure)
        {
            var options = new AnalysisOptions();
            configure(options);
            _services.Configure(configure);
            return this;
        }
    }

    public class AnalysisOptions
    {
        public double MinimumCohesion { get; set; } = 0.5;
        public int MinimumFrequency { get; set; } = 2;
        public int MaxDepth { get; set; } = 3;
    }
}
