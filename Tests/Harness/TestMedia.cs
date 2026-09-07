using Domain.Media;
using static Harness.Ids;

namespace Harness;

internal sealed record MediaSpec(string Title, MediaType Type, IReadOnlyList<ProviderId> Ids);

internal static class TestMedia
{
	internal static MediaSpec Inception => new("Inception", MediaType.Movie, [Tmdb("27205"), Plex("27205")]);

	internal static MediaSpec FightClub => new("Fight Club", MediaType.Movie, [Tmdb("550"), Plex("550")]);

	internal static MediaSpec BreakingBad => new("Breaking Bad", MediaType.Show, [Tmdb("1396"), Plex("1396")]);

	internal static MediaSpec Interstellar => new("Interstellar", MediaType.Movie, [Tmdb("157336"), Plex("157336")]);

	internal static MediaSpec Person(string name) => new(name, MediaType.Unknown, []);
}
