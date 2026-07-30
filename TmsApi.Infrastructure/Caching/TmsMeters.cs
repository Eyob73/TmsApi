using System.Diagnostics.Metrics;

namespace TmsApi.Infrastructure.Caching;

public static class TmsMeters
{
    public static readonly Meter Meter = new("tms-api");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "tms.cache.hits",
        description: "Course cache hits"
    );

    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "tms.cache.misses",
        description: "Course cache misses"
    );
}
