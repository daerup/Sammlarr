using Contracts.DI;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Media;
using Domain.Providers;
using Harness;
using TUnit.Assertions.Enums;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class IdLookupGraphTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IEnumerable<Keyed<RemoteService, IExternalIdConverter>> Converters => host.Resolve<IEnumerable<Keyed<RemoteService, IExternalIdConverter>>>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Derive_OverTheRegisteredConverters_GivesPlexsHopsThenTmdbsThenWikidatasHintlessOne()
	{
		var hops = IdLookupPlanner.Derive(Converters);

		await Assert
			.That(hops.Select(Describe))
			.IsEquivalentTo(
				[
					"Plex Plex>Tmdb (1)",
					"Plex Plex>Imdb (1)",
					"Plex Plex>Tvdb (1)",
					"Plex Plex>Plex (1)",
					"Plex Plex>hints (1)",
					"Plex hints>Tmdb (4)",
					"Plex hints>Imdb (4)",
					"Plex hints>Tvdb (4)",
					"Plex hints>Plex (4)",
					"Tmdb Tmdb>Imdb (1)",
					"Tmdb Tmdb>Tvdb (1)",
					"Tmdb Tmdb>hints (1)",
					"Tmdb Imdb>Tmdb (1)",
					"Tmdb Imdb>hints (1)",
					"Tmdb Tvdb>Tmdb (1)",
					"Tmdb Tvdb>hints (1)",
					"Wikidata Imdb>RottenTomatoes (1)",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Plan_ForAnImdbIdWithHints_SearchesFirstThenThroughTmdbsTitle()
	{
		var hops = IdLookupPlanner.Derive(Converters);
		var capacity = host.Resolve<IRemoteCapacity>();

		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, true, MediaProvider.Plex, hops, capacity, DateTimeOffset.UnixEpoch);

		await Assert
			.That(plan.Paths.Select(path => string.Join(", ", path.Select(Describe))))
			.IsEquivalentTo(
				["Plex hints>Plex (4)", "Tmdb Imdb>hints (1), Plex hints>Plex (4)", "Tmdb Imdb>Tmdb (1), Tmdb Tmdb>hints (1), Plex hints>Plex (4)"],
				CollectionOrdering.Matching
			);
	}

	private static string Describe(Hop hop)
	{
		var (from, to) = hop switch
		{
			IdToId id => (id.From.ToString(), id.To.ToString()),
			IdToHints hints => (hints.From.ToString(), "hints"),
			HintsToId search => ("hints", search.To.ToString()),
		};
		return $"{hop.Remote} {from}>{to} ({hop.Cost})";
	}
}
