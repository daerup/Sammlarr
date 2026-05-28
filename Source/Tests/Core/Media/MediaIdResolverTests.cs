using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Core.Media;
using Test.Shared;
using TUnit.Mocks.Generated.Contracts.Media.Convertions;

namespace Core.Tests.Media;

public class MediaIdResolverTests
{
	[Test]
	public async Task ResolveAsync_WhenConverterReturnsNothing_CachesNegativeResultWithTtlAndReturnsNone()
	{
		var tmdbId = TmdbId.From("123", MediaType.Movie);
        var (testee, converter, cache) = CreateTestee();
        converter.ConvertAsync(Any(), Any(), Any()).Returns(new None());

		var result = await testee.ResolveAsync(new ResolveRequest([tmdbId], MediaProvider.Tvdb, new None()));

		await Assert.That(result.Value).IsTypeOf<None>();
        cache.Set(Any(), true, TimeSpan.FromHours(1)).WasCalled();
    }

	[Test]
	public async Task ResolveAsync_WhenUnresolvedCacheHit_ShortCircuitsAndReturnsNone()
	{
		var tmdbId = TmdbId.From("456", MediaType.Movie);
		var (testee, converter, cache) = CreateTestee();
        cache.TryGetValue(Any()).Returns(true);

		var result = await testee.ResolveAsync(new ResolveRequest([tmdbId], MediaProvider.Tvdb, new None()));

		await Assert.That(result.Value).IsTypeOf<None>();
        converter.ConvertAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	private static (MediaIdResolver testee, IExternalIdConverterMock converter, Mock<ITypedMemoryCache<string, bool>> cache) CreateTestee()
	{
        var converter = IExternalIdConverter.Mock();
        var cache = ITypedMemoryCache<string, bool>.Mock();
        var converters = new FakeKeyedServiceIndex<MediaProvider, IExternalIdConverter>().Register(MediaProvider.Plex, converter);
        var testee = new MediaIdResolver(converters, cache);
		return (testee, converter, cache);
	}
}
