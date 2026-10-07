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
	public async Task Derive_OverTheRegisteredConverters_GivesPlexsHopsThenTmdbsThenWikidatasHintlessOnesThenTvmazes()
	{
		var hops = IdLookupPlanner.Derive(Converters);

		await Assert
			.That(hops.Where(hop => hop.Remote != RemoteService.Mdblist).Select(Describe))
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
					"Wikidata Imdb>Tmdb (3)",
					"Wikidata Imdb>RottenTomatoes (3)",
					"Wikidata Imdb>Tvdb (3)",
					"Wikidata Imdb>Metacritic (3)",
					"Wikidata Imdb>Letterboxd (3)",
					"Wikidata Imdb>Mal (3)",
					"Wikidata Imdb>AniList (3)",
					"Tvmaze Imdb>Tvdb (2)",
					"Tvmaze Imdb>Tvmaze (2)",
					"Tvmaze Imdb>hints (2)",
					"Tvmaze Tvdb>Imdb (2)",
					"Tvmaze Tvdb>Tvmaze (2)",
					"Tvmaze Tvdb>hints (2)",
					"AnimeLists Mal>Tmdb (1)",
					"AnimeLists Mal>Imdb (1)",
					"AnimeLists Mal>Tvdb (1)",
					"AnimeLists Mal>AniList (1)",
					"AnimeLists AniList>Tmdb (1)",
					"AnimeLists AniList>Imdb (1)",
					"AnimeLists AniList>Tvdb (1)",
					"AnimeLists AniList>Mal (1)",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Derive_OverTheRegisteredConverters_LetsMdblistReachATvdbIdFromEveryOtherIdItTakes()
	{
		var hops = IdLookupPlanner.Derive(Converters);

		await Assert
			.That(hops.Where(hop => hop.Remote == RemoteService.Mdblist && hop is IdToId { To: MediaProvider.Tvdb }).Select(Describe))
			.IsEquivalentTo(["Mdblist Tmdb>Tvdb (2)", "Mdblist Imdb>Tvdb (2)", "Mdblist Trakt>Tvdb (2)", "Mdblist Mal>Tvdb (2)"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_ForAnImdbIdWithHints_SearchesFirstThenThroughTheCheapestTitleHints()
	{
		var hops = IdLookupPlanner.Derive(Converters);
		var capacity = host.Resolve<IRemoteCapacity>();

		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, true, MediaProvider.Plex, hops, capacity, DateTimeOffset.UnixEpoch);

		await Assert
			.That(plan.Paths.Select(path => string.Join(", ", path.Select(Describe))))
			.IsEquivalentTo(
				[
					"Plex hints>Plex (4)",
					"Tmdb Imdb>hints (1), Plex hints>Plex (4)",
					"Mdblist Imdb>hints (2), Plex hints>Plex (4)",
					"Tvmaze Imdb>hints (2), Plex hints>Plex (4)",
					"Tmdb Imdb>Tmdb (1), Tmdb Tmdb>hints (1), Plex hints>Plex (4)",
					"Tmdb Imdb>Tmdb (1), Mdblist Tmdb>hints (2), Plex hints>Plex (4)",
					"Mdblist Imdb>Tmdb (2), Tmdb Tmdb>hints (1), Plex hints>Plex (4)",
					"Mdblist Imdb>Tvdb (2), Tmdb Tvdb>hints (1), Plex hints>Plex (4)",
					"Tvmaze Imdb>Tvdb (2), Tmdb Tvdb>hints (1), Plex hints>Plex (4)",
					"Mdblist Imdb>Tmdb (2), Mdblist Tmdb>hints (2), Plex hints>Plex (4)",
					"Mdblist Imdb>Tvdb (2), Mdblist Tvdb>hints (2), Plex hints>Plex (4)",
					"Mdblist Imdb>Tvdb (2), Tvmaze Tvdb>hints (2), Plex hints>Plex (4)",
					"Mdblist Imdb>Trakt (2), Mdblist Trakt>hints (2), Plex hints>Plex (4)",
					"Mdblist Imdb>Mal (2), Mdblist Mal>hints (2), Plex hints>Plex (4)",
					"Wikidata Imdb>Tmdb (3), Tmdb Tmdb>hints (1), Plex hints>Plex (4)",
					"Wikidata Imdb>Tvdb (3), Tmdb Tvdb>hints (1), Plex hints>Plex (4)",
					"Tvmaze Imdb>Tvdb (2), Mdblist Tvdb>hints (2), Plex hints>Plex (4)",
					"Tvmaze Imdb>Tvdb (2), Tvmaze Tvdb>hints (2), Plex hints>Plex (4)",
					"Wikidata Imdb>Tmdb (3), Mdblist Tmdb>hints (2), Plex hints>Plex (4)",
					"Wikidata Imdb>Tvdb (3), Mdblist Tvdb>hints (2), Plex hints>Plex (4)",
					"Wikidata Imdb>Tvdb (3), Tvmaze Tvdb>hints (2), Plex hints>Plex (4)",
					"Wikidata Imdb>Mal (3), Mdblist Mal>hints (2), Plex hints>Plex (4)",
				],
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
