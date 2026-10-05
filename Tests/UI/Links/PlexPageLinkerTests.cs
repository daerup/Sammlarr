using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class PlexPageLinkerTests
{
	private static readonly PlexPageLinker Testee = new();

	[Test]
	public async Task PageOf_ForAPlexId_IsItsDiscoverPage()
	{
		var page = Testee.PageOf([PlexId.From("5d77683085719b001f3a2a24", MediaType.Movie)], new None());

		await Assert
			.That(page)
			.IsSome()
			.And.IsEqualTo("https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F5d77683085719b001f3a2a24");
	}

	[Test]
	public async Task PageOf_WithoutAPlexId_IsNone()
	{
		await Assert.That(Testee.PageOf([TmdbId.From("27205", MediaType.Movie)], Some.Of("Inception"))).IsNone();
	}
}
