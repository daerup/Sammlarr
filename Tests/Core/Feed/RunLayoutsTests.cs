using Contracts.DI;
using Contracts.Media.Conversions;
using Core.Feed;
using Core.Media;
using Domain.Feed.Trace;
using Domain.Media.Conversions;
using Domain.Providers;
using TUnit.Assertions.Enums;

namespace Core.Tests.Feed;

public class RunLayoutsTests
{
	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];
	private static readonly IdLookup PlexId = new ById(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);
	private static readonly IdLookup Search = new ByHints(Yields: Guids, Cost: 4);
	private static readonly IdLookup Detail = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
	private static readonly IdLookup Find = new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1);
	private static readonly IdLookup AnimeMap = new ById(
		Accepts: [MediaProvider.Tvdb],
		Yields: [MediaProvider.AniList, MediaProvider.Mal],
		Cost: 0,
		GivesHints: false
	);

	private static readonly IReadOnlyList<Hop> Hops = IdLookupPlanner.Derive([
		Converter(RemoteService.Plex, [PlexId, Search]),
		Converter(RemoteService.Tmdb, [Detail, Find]),
		Converter(RemoteService.AnimeLists, [AnimeMap]),
	]);

	private static readonly IReadOnlyList<Hop> Crossing = IdLookupPlanner.Derive([
		Converter(RemoteService.Tmdb, [new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.AniList], Cost: 1)]),
		Converter(RemoteService.Plex, [new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Mal], Cost: 1)]),
		Converter(RemoteService.Mdblist, [new ById(Accepts: [MediaProvider.Mal], Yields: [MediaProvider.Tvdb], Cost: 1)]),
		Converter(RemoteService.Wikidata, [new ById(Accepts: [MediaProvider.AniList], Yields: [MediaProvider.Tvdb], Cost: 1)]),
	]);

	[Test]
	public async Task Build_ForAPlexTarget_PlacesPlexAfterTheHintGivers()
	{
		var layout = RunLayouts.Build(Hops, new HashSet<MediaProvider> { MediaProvider.Tmdb }, [MediaProvider.Plex], [RemoteService.Tmdb]);

		await Assert.That(Column(layout, RemoteService.Plex)).IsGreaterThan(Column(layout, RemoteService.Tmdb));
		await Assert.That(layout.Edges).Contains(new LayoutEdge(new None(), RemoteService.Tmdb));
		await Assert.That(layout.Details).IsEquivalentTo([RemoteService.Tmdb]);
	}

	[Test]
	public async Task Build_ForTheArrPair_HasBothTargets()
	{
		var layout = RunLayouts.Build(Hops, new HashSet<MediaProvider> { MediaProvider.Imdb }, [MediaProvider.Tmdb, MediaProvider.Tvdb], []);

		await Assert.That(layout.Targets).IsEquivalentTo([MediaProvider.Tmdb, MediaProvider.Tvdb]);
		await Assert.That(layout.Ids.Select(node => node.Service)).Contains(RemoteService.Tmdb);
		await Assert.That(layout.Edges).Contains(new LayoutEdge(new None(), RemoteService.Tmdb));
	}

	[Test]
	public async Task Build_ForServicesThatConvertEachOthersIds_KeepsEveryEdgeBetweenColumns()
	{
		IReadOnlyList<Hop> hops = IdLookupPlanner.Derive([
			Converter(RemoteService.Tmdb, [Find, new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb], Cost: 1)]),
			Converter(RemoteService.Trakt, [new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb], Cost: 1)]),
		]);

		var layout = RunLayouts.Build(hops, new HashSet<MediaProvider> { MediaProvider.Imdb }, [MediaProvider.Tmdb, MediaProvider.Tvdb], []);

		IReadOnlyList<bool> apart = [.. layout.Edges.Select(edge => edge.From.Map(from => Column(layout, from) != Column(layout, edge.To))).WhereSome()];
		await Assert.That(apart).IsNotEmpty();
		await Assert.That(apart).DoesNotContain(false);
	}

	[Test]
	public async Task Build_WithAFreeLookupOfTheFeedsIds_PlacesItInMemoryInTheFirstColumn()
	{
		var layout = RunLayouts.Build(Hops, new HashSet<MediaProvider> { MediaProvider.Tvdb }, [MediaProvider.Plex], []);

		await Assert.That(layout.Ids).Contains(new LayoutNode(RemoteService.AnimeLists, 1, true));
	}

	[Test]
	public async Task Build_ForAFeedHoldingTheTarget_PlacesNoIdService()
	{
		var layout = RunLayouts.Build(Hops, new HashSet<MediaProvider> { MediaProvider.Tmdb }, [MediaProvider.Tmdb], []);

		await Assert.That(layout.Ids).IsEmpty();
	}

	[Test]
	public async Task Build_WhereTheServiceOrderWouldCrossTwoLines_OrdersEachColumnByWhatFeedsIt()
	{
		var layout = RunLayouts.Build(Crossing, new HashSet<MediaProvider> { MediaProvider.Imdb }, [MediaProvider.Tvdb], []);

		await Assert
			.That(layout.Ids.Select(node => node.Service))
			.IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex, RemoteService.Wikidata, RemoteService.Mdblist], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Build_WithDetailsServices_OrdersThemLikeTheirIdServicesAndTheRestLast()
	{
		var layout = RunLayouts.Build(
			Crossing,
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			[MediaProvider.Tvdb],
			[RemoteService.Tvmaze, RemoteService.Wikidata, RemoteService.Tmdb]
		);

		await Assert.That(layout.Details).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Wikidata, RemoteService.Tvmaze], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Build_WithDetailsServicesOfOneColumn_OrdersThemByTheirRows()
	{
		var layout = RunLayouts.Build(
			Crossing,
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			[MediaProvider.Tvdb],
			[RemoteService.Mdblist, RemoteService.Wikidata]
		);

		await Assert.That(layout.Details).IsEquivalentTo([RemoteService.Wikidata, RemoteService.Mdblist], CollectionOrdering.Matching);
	}

	private static int Column(RunLayout layout, RemoteService service) => layout.Ids.Single(node => node.Service == service).Column;

	private static Keyed<RemoteService, IExternalIdConverter> Converter(RemoteService remote, IReadOnlyList<IdLookup> lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns(lookups);
		return new Keyed<RemoteService, IExternalIdConverter>(remote, converter.Object);
	}
}
