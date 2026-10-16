using Contracts;
using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IMediaRepository _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		_testee = new MediaRepository(fixture.ContextFactory, new MediaDetailsFactory(), new MediaWriteLock(), fixture.Resolve<IDateTimeProvider>());
	}

	[Test]
	public async Task GetExternalsAsync_WhenMediaExists_ReturnsExternalForRequestedSource()
	{
		var ids = new IIdentityKey[] { TmdbId.From("210", MediaType.Movie), PlexId.From("plex-210", MediaType.Movie) };
		var mediaId = await fixture.SeedMediaAsync(ids);

		var result = await _testee.GetExternalsAsync([mediaId], MediaProvider.Plex);

		await Assert.That(result.Count).IsEqualTo(1);
		await Assert.That(result[mediaId]).IsEqualTo(PlexId.From("plex-210", MediaType.Movie));
	}
}
