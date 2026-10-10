using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class MediaIdentityInvariantsTests
{
	[Test]
	public async Task EnsureSinkIdPresent_WhenMissing_Throws()
	{
		IExternalMediaId[] externals = [TmdbId.From("1", MediaType.Movie)];

		await Assert.That(() => externals.EnsureSinkIdPresent(MediaProvider.Plex)).Throws<MissingSinkIdException>();
	}
}
