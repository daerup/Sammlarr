using Core.Media;
using Domain.Media;
using Domain.Providers;

namespace Core.Tests.Media;

public class UnmatchedCacheKeyTests
{
	private static readonly IExternalMediaId[] Candidates = [TmdbId.From("550", MediaType.Movie), ImdbId.From("tt0137523", MediaType.Movie)];

	[Test]
	public async Task Build_ForTheSameCandidatesInAnotherOrder_IsTheSameKey()
	{
		await Assert
			.That(UnmatchedCacheKey.Build([.. Candidates.Reverse()], MediaProvider.Plex, [RemoteService.Plex]))
			.IsEqualTo(UnmatchedCacheKey.Build(Candidates, MediaProvider.Plex, [RemoteService.Plex]));
	}

	[Test]
	public async Task Build_OnceAnotherServiceConnects_IsAnotherKey()
	{
		await Assert
			.That(UnmatchedCacheKey.Build(Candidates, MediaProvider.Plex, [RemoteService.Plex, RemoteService.Tmdb]))
			.IsNotEqualTo(UnmatchedCacheKey.Build(Candidates, MediaProvider.Plex, [RemoteService.Plex]));
	}

	[Test]
	public async Task Build_ForTheSameConnectedServicesInAnotherOrder_IsTheSameKey()
	{
		await Assert
			.That(UnmatchedCacheKey.Build(Candidates, MediaProvider.Plex, [RemoteService.Tmdb, RemoteService.Plex]))
			.IsEqualTo(UnmatchedCacheKey.Build(Candidates, MediaProvider.Plex, [RemoteService.Plex, RemoteService.Tmdb]));
	}
}
