using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.API.Services.Caching;
using Nocturne.API.Services.Treatments;
using Nocturne.API.Tests.Infrastructure;
using Nocturne.Core.Contracts.Treatments;

namespace Nocturne.API.Tests.Services.Treatments;

[Trait("Category", "Unit")]
public class TreatmentCacheAdapterTests
{
    private static readonly TreatmentQuery CacheableQuery = new() { Count = 10, Skip = 0 };

    private static TreatmentCacheAdapter Adapter(ITenantCache cache) =>
        new(cache, NullLogger<TreatmentCacheAdapter>.Instance);

    [Fact]
    public async Task GetOrComputeAsync_CacheableQuery_ServesTheTenantCache()
    {
        var cached = new List<Treatment> { new() { Id = "cached" } };
        var cache = new Mock<ITenantCache>();
        cache
            .Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(), It.IsAny<Func<Task<List<Treatment>>>>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await Adapter(cache.Object).GetOrComputeAsync(
            CacheableQuery, () => Task.FromResult<IReadOnlyList<Treatment>>([]), CancellationToken.None);

        result.Should().BeSameAs(cached);
    }

    [Fact]
    public async Task InvalidateAsync_DropsTheCachedRead()
    {
        var adapter = Adapter(TestTenantCache.Create());
        await adapter.GetOrComputeAsync(
            CacheableQuery,
            () => Task.FromResult<IReadOnlyList<Treatment>>([new Treatment { Id = "stale" }]),
            CancellationToken.None);

        await adapter.InvalidateAsync(CancellationToken.None);
        var result = await adapter.GetOrComputeAsync(
            CacheableQuery,
            () => Task.FromResult<IReadOnlyList<Treatment>>([new Treatment { Id = "fresh" }]),
            CancellationToken.None);

        result!.Single().Id.Should().Be("fresh");
    }
}
