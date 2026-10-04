using Contracts.Media.Artwork;
using Domain.Providers;
using TestInfrastructure;
using UI.Shared.Services;

namespace UI.Tests.Media;

internal static class ArtworkStub
{
	internal static ArtworkSources Sizing() => Sources(Resizing(), Declining());

	internal static ArtworkSources Sources(IArtworkResizer tmdb, IArtworkResizer plex) =>
		new(
			new FakeKeyedServiceIndex<ArtworkProvider, IArtworkResizer>()
				.Register(ArtworkProvider.Tmdb, tmdb)
				.Register(ArtworkProvider.Plex, plex)
				.Register(ArtworkProvider.Tvdb, Declining())
		);

	internal static IArtworkResizer Resizing()
	{
		var resizer = IArtworkResizer.Mock();
		resizer.Resize(Any(), Any()).Returns((url, size) => Some.Of($"{url}?width={size.Width}"));
		return resizer.Object;
	}

	internal static IArtworkResizer Declining()
	{
		var resizer = IArtworkResizer.Mock();
		resizer.Resize(Any(), Any()).Returns(new None());
		return resizer.Object;
	}
}
