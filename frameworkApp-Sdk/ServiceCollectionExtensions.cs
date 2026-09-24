using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
namespace ConquerorServer
{
	public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationInsights(this IServiceCollection services)
        {
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                options.EnableAdaptiveSampling = false;
                options.EnablePerformanceCounterCollectionModule = false;
                options.EnableDependencyTrackingTelemetryModule = true;
            });

            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            return services;
        }

    }
}
