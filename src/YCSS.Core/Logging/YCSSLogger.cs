using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Logging
{
    public static class LoggerExtensions
    {
        public static IDisposable BeginStyleOperation(
            this ILogger logger,
            string operationName,
            string? details = null)
        {
            return new StyleOperationScope(logger, operationName, details);
        }

        public static void LogStyleValidation(
            this ILogger logger,
            string component,
            string property,
            string message)
        {
            logger.LogInformation(
                "Style Validation: {Component}.{Property} - {Message}",
                component,
                property,
                message);
        }

        public static void LogPatternDetection(
            this ILogger logger,
            string patternType,
            double confidence,
            int frequency)
        {
            logger.LogInformation(
                "Pattern Detection: {Type} (Confidence: {Confidence:P}, Frequency: {Frequency})",
                patternType,
                confidence,
                frequency);
        }

        public static void LogCompilation(
            this ILogger logger,
            string stage,
            string details)
        {
            logger.LogInformation(
                "Compilation {Stage}: {Details}",
                stage,
                details);
        }

        public static void LogPerformanceMetric(
            this ILogger logger,
            string operation,
            TimeSpan duration,
            string? details = null)
        {
            logger.LogInformation(
                "Performance: {Operation} took {Duration}ms{Details}",
                operation,
                duration.TotalMilliseconds,
                details != null ? $" ({details})" : "");
        }
    }

    public class StyleOperationScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly string? _details;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public StyleOperationScope(
            ILogger logger,
            string operationName,
            string? details = null)
        {
            _logger = logger;
            _operationName = operationName;
            _details = details;
            _stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Starting {Operation}{Details}",
                operationName,
                details != null ? $" ({details})" : "");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopwatch.Stop();
            _logger.LogInformation(
                "Completed {Operation} in {Duration}ms{Details}",
                _operationName,
                _stopwatch.ElapsedMilliseconds,
                _details != null ? $" ({_details})" : "");
        }
    }

    public class PerformanceLogger
    {
        private readonly ConcurrentDictionary<string, Metrics> _metrics = new();

        public void RecordMetric(string operation, TimeSpan duration)
        {
            _metrics.AddOrUpdate(
                operation,
                _ => new Metrics { Count = 1, TotalDuration = duration },
                (_, existing) =>
                {
                    existing.Count++;
                    existing.TotalDuration += duration;
                    return existing;
                });
        }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new PerformanceMetrics(
                    kvp.Value.Count,
                    kvp.Value.TotalDuration,
                    TimeSpan.FromTicks(kvp.Value.TotalDuration.Ticks / kvp.Value.Count)
                ));
        }

        private class Metrics
        {
            public int Count { get; set; }
            public TimeSpan TotalDuration { get; set; }
        }
    }

    public record PerformanceMetrics(
        int ExecutionCount,
        TimeSpan TotalDuration,
        TimeSpan AverageDuration
    );
}
