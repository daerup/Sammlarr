using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class MediaIdentityInvariantsTests
{
	[Test]
	public async Task EnsureSingleMediaType_WhenMixedTypes_Throws()
	{
		IExternalMediaId[] externals = [TmdbId.From("1", MediaType.Movie), ImdbId.From("tt2", MediaType.Show)];

		await Assert.That(() => externals.EnsureSingleMediaType()).Throws<MixedMediaTypesException>();
	}

	[Test]
	public async Task EnsureSingleMediaType_WhenSameType_DoesNotThrow()
	{
		IExternalMediaId[] externals = [TmdbId.From("1", MediaType.Movie), ImdbId.From("tt2", MediaType.Movie)];

		externals.EnsureSingleMediaType();

		await Assert.That(externals).IsNotEmpty();
	}

	[Test]
	public async Task EnsureSinkIdPresent_WhenMissing_Throws()
	{
		IExternalMediaId[] externals = [TmdbId.From("1", MediaType.Movie)];

		await Assert.That(() => externals.EnsureSinkIdPresent(MediaProvider.Plex)).Throws<MissingSinkIdException>();
	}
}
