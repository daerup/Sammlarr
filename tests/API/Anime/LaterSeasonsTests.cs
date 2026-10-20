using System.Runtime.CompilerServices;
using API.Anime;
using Contracts.Datasets;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace API.Tests.Anime;

public class LaterSeasonsTests
{
	private static readonly IdentifyRequest ThirdSeasonAsked = new(
		[AniListId.From("185407", MediaType.Show), MalId.From("59130", MediaType.Show)],
		Some.Of(
			new IdentifyHints
			{
				Title = "Kusuriya no Hitorigoto 3rd Season",
				Year = 2026,
				MediaType = MediaType.Show,
			}
		)
	);

	private static readonly IdentifyRequest ShowItself = new([AniListId.From("161645", MediaType.Show)], new None());

	private static readonly MalId BareSeason = MalId.From("60001", MediaType.Show);

	private static readonly IdentifyRequest BareSeasonAsked = new([BareSeason], new None());

	private static readonly string[] StoredShowIds = ["Tvdb:Show:431162", "AniList:Show:161645"];

	private static readonly string[] ThirdSeasonIds = ["AniList:Show:185407", "Mal:Show:59130"];

	private static readonly string[] BareSeasonIds = ["AniList:Show:200001", "Mal:Show:60001"];

	private readonly List<IdentifyRequest> _asked = [];

	[Test]
	public async Task LoadAsync_ALaterSeason_AsksForItsShowByTheShowsOwnIdsWithoutHints()
	{
		var prequels = AnimeMaps.NoPrequels();

		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: prequels.Object), ThirdSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
		await Assert.That(asked.Hints).IsNone();
		await Assert.That(asked.Description.Map(hints => hints.Title)).IsSome().And.IsEqualTo("Kusuriya no Hitorigoto 3rd Season");
		prequels.PrequelsOfAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ALaterSeasonOfASeriesNoEntryStandsFor_AsksByTheSeriesTvdbIdAlone()
	{
		var map = AnimeMaps.Unknown();
		map.FindAsync(Any(), Any()).Returns((id, _) => Optional.When(id is not TvdbId, () => AnimeMaps.ThirdSeason));
		map.FindManyAsync(Any(), Any()).Returns((ids, _) => ids.Where(id => id is not TvdbId).Distinct().ToDictionary(id => id, _ => AnimeMaps.ThirdSeason));

		var asked = await AskedAsync(Seasons(map), ThirdSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(["Tvdb:Show:431162"]);
	}

	[Test]
	public async Task LoadAsync_ForAPageOfShows_AsksForEachInListOrder()
	{
		IdentifyRequest[] page = [ThirdSeasonAsked, ShowItself, new IdentifyRequest([AniListId.From("999999", MediaType.Show)], new None())];

		await Seasons(AnimeMaps.KnowingTheApothecaryDiaries()).LoadAsync(page.ToAsyncEnumerable(), FieldNeeds.None, CancellationToken.None).ToListAsync();

		await Assert
			.That(_asked.Select(request => $"{request.Candidates[0]}"))
			.IsEquivalentTo(["AniList:Show:161645", "AniList:Show:161645", "AniList:Show:999999"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task LoadAsync_TheShowItself_LeavesTheRequestAsItIs()
	{
		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries()), ShowItself);

		await Assert.That(asked).IsSameReferenceAs(ShowItself);
	}

	[Test]
	public async Task LoadAsync_AnIdTheMapDoesNotKnow_LeavesTheRequestAsItIs()
	{
		var asked = await AskedAsync(Seasons(AnimeMaps.Unknown()), ThirdSeasonAsked);

		await Assert.That(asked).IsSameReferenceAs(ThirdSeasonAsked);
	}

	[Test]
	public async Task LoadAsync_ALaterSeasonTheMapHasNoIdsFor_AsksForTheShowItsPrequelBelongsToAndCreditsAniList()
	{
		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 59130).Object), BareSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
		await Assert.That(asked.Sources).Contains(RemoteService.AniList);
	}

	[Test]
	public async Task LoadAsync_ASecondSeasonWhosePrequelIsTheShowItself_AsksForThatShow()
	{
		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 54492).Object), BareSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
	}

	[Test]
	public async Task LoadAsync_ALaterSeasonWhosePrequelHasNoIdsEither_LeavesTheRequestAsItIs()
	{
		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 60002).Object), BareSeasonAsked);

		await Assert.That(asked).IsSameReferenceAs(BareSeasonAsked);
	}

	[Test]
	public async Task LoadAsync_WhenAniListCannotSayWhatCameBefore_CouldNotLookUpTheSeason()
	{
		var testee = Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, Error.From("AniList answered 429")).Object);

		var listed = await testee.LoadAsync(AsyncOf(BareSeasonAsked), FieldNeeds.None, CancellationToken.None).SingleAsync();

		var unmatched = await Assert.That(listed.Item).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp);
	}

	[Test]
	public async Task LoadAsync_AMovie_NeverAsksForAPrequel()
	{
		var prequels = AnimeMaps.NoPrequels();
		var request = new IdentifyRequest([MalId.From("60003", MediaType.Movie)], new None());

		var asked = await AskedAsync(Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: prequels.Object), request);

		await Assert.That(asked).IsSameReferenceAs(request);
		prequels.PrequelsOfAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_BeforeTheMapsFirstLoad_FailsTheRead()
	{
		var testee = Seasons(AnimeMaps.Unknown(), loaded: false);

		await Assert
			.That(async () => await testee.LoadAsync(AsyncOf(ThirdSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync())
			.Throws<InvalidOperationException>()
			.WithMessage("The anime id map has not been loaded yet.");
	}

	[Test]
	public async Task LoadAsync_ASeasonLinkedToAStoredShow_AsksByTheShowsStoredIdsWithoutTheMapOrAPrequel()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		var prequels = AnimeMaps.NoPrequels();
		var testee = Seasons(map, prequels: prequels.Object, parts: Linking(BareSeason).Object);

		var asked = await AskedAsync(testee, BareSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(StoredShowIds);
		map.FindManyAsync(Any(), Any()).WasNeverCalled();
		prequels.PrequelsOfAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ASeasonLinkedToAStoredShow_KeepsItsHintsAsTheDescription()
	{
		var testee = Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), parts: Linking(AniListId.From("185407", MediaType.Show)).Object);

		var asked = await AskedAsync(testee, ThirdSeasonAsked);

		await Assert.That(asked.Hints).IsNone();
		await Assert.That(asked.Description.Map(hints => hints.Title)).IsSome().And.IsEqualTo("Kusuriya no Hitorigoto 3rd Season");
	}

	[Test]
	public async Task LoadAsync_ForAMatchAtAConvertedPosition_LinksTheSeasonsAniListAndMalIds()
	{
		var parts = AnimeMaps.NoParts();
		var show = MediaId.New();
		var media = Settling(new Listed<MediaMatchResult>(1, Matched(show)), new Listed<MediaMatchResult>(0, Matched(show)));
		var testee = AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), media: media, parts: parts.Object);

		await testee
			.LoadAsync(AsyncOf(ShowItself, new IdentifyRequest([MalId.From("59130", MediaType.Show)], new None())), FieldNeeds.None, CancellationToken.None)
			.ToListAsync();

		parts.AddAsync(Parts(ThirdSeasonIds), LinkedTo(show), Any(), Any()).WasCalled(Times.Once);
		parts.AddAsync(Any(), Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_ForAPageMixingLinkedAndUnlinkedSeasons_LinksTheMatchAtItsOwnPosition()
	{
		var parts = Linking(AniListId.From("300001", MediaType.Show));
		var show = MediaId.New();
		var testee = AnimeMaps.Seasons(
			AnimeMaps.KnowingTheApothecaryDiaries(),
			prequels: Prequel(60001, Error.From("AniList answered 429")).Object,
			media: Settling(new Listed<MediaMatchResult>(2, Matched(show))),
			parts: parts.Object
		);

		await testee
			.LoadAsync(
				AsyncOf(BareSeasonAsked, new IdentifyRequest([AniListId.From("300001", MediaType.Show)], new None()), ThirdSeasonAsked),
				FieldNeeds.None,
				CancellationToken.None
			)
			.ToListAsync();

		parts.AddAsync(Parts(ThirdSeasonIds), LinkedTo(show), Any(), Any()).WasCalled(Times.Once);
		parts.AddAsync(Any(), Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_ForAnUnmatchedSeason_LinksNothing()
	{
		var parts = AnimeMaps.NoParts();
		var media = Settling(new Listed<MediaMatchResult>(0, new UnmatchedMedia([MalId.From("59130", MediaType.Show)], new ShowDetails())));
		var testee = AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), media: media, parts: parts.Object);

		await testee.LoadAsync(AsyncOf(ThirdSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync();

		parts.AddAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ForATitleListedAsItIsOrAlreadyLinked_LinksNothing()
	{
		var parts = Linking(BareSeason);
		var show = MediaId.New();
		var media = Settling(new Listed<MediaMatchResult>(0, Matched(show)), new Listed<MediaMatchResult>(1, Matched(show)));
		var testee = AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), media: media, parts: parts.Object);

		await testee.LoadAsync(AsyncOf(ShowItself, BareSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync();

		parts.AddAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ForASeasonTheMapPlaces_StoresItsTvdbSeasonAndOffset()
	{
		var placed = AnimeMaps.ThirdSeason with { TvdbSeason = Some.Of(3), EpisodeOffset = Some.Of(24) };
		var map = AnimeMaps.Unknown();
		map.FindAsync(Any(), Any()).Returns((id, _) => Some.Of(id is TvdbId ? AnimeMaps.ApothecaryDiaries : placed));
		map.FindManyAsync(Any(), Any()).Returns((ids, _) => ids.Distinct().ToDictionary(id => id, id => id is TvdbId ? AnimeMaps.ApothecaryDiaries : placed));
		var parts = AnimeMaps.NoParts();
		var testee = AnimeMaps.Seasons(map, media: Settling(new Listed<MediaMatchResult>(0, Matched(MediaId.New()))), parts: parts.Object);

		await testee.LoadAsync(AsyncOf(ThirdSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync();

		parts.AddAsync(Any(), Any(), new ShowPartPlace(Some.Of(3), Some.Of(24)), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_ASeasonStoredWithNoShow_LeavesTheRequestAsItIsWithoutTheMapOrAPrequel()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		var prequels = AnimeMaps.NoPrequels();
		var testee = Seasons(map, prequels: prequels.Object, parts: Answering(BareSeason, new None()).Object);

		var asked = await AskedAsync(testee, BareSeasonAsked);

		await Assert.That(asked).IsSameReferenceAs(BareSeasonAsked);
		map.FindManyAsync(Any(), Any()).WasNeverCalled();
		prequels.PrequelsOfAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ForASeasonWhosePrequelLeadsNowhere_StoresThatItHasNoShow()
	{
		var parts = AnimeMaps.NoParts();
		var testee = Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 60002).Object, parts: parts.Object);

		await testee.LoadAsync(AsyncOf(BareSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync();

		parts.AddAsync(Parts(BareSeasonIds), NoShow(), Any(), Any()).WasCalled(Times.Once);
		parts.AddAsync(Any(), Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_WhenAniListFails_StoresNoAnswer()
	{
		var parts = AnimeMaps.NoParts();
		var testee = Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, Error.From("AniList answered 429")).Object, parts: parts.Object);

		await testee.LoadAsync(AsyncOf(BareSeasonAsked), FieldNeeds.None, CancellationToken.None).ToListAsync();

		parts.AddAsync(Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	private LaterSeasons Seasons(Mock<IAnimeIds> map, bool loaded = true, IAnimePrequels? prequels = null, IShowParts? parts = null)
	{
		return AnimeMaps.Seasons(map, loaded, prequels, AnimeMaps.Passing(_asked.Add), parts);
	}

	private async Task<IdentifyRequest> AskedAsync(LaterSeasons testee, IdentifyRequest request)
	{
		await testee.LoadAsync(AsyncOf(request), FieldNeeds.None, CancellationToken.None).ToListAsync();
		await Assert.That(_asked).HasSingleItem();
		return _asked[0];
	}

	private static Mock<IAnimePrequels> Prequel(int season, int prequel) => Prequel(season, Success.Of<Optional<int>>(Some.Of(prequel)));

	private static Mock<IAnimePrequels> Prequel(int season, Result<Optional<int>> prequel)
	{
		var prequels = IAnimePrequels.Mock();
		prequels.PrequelsOfAsync(Any(), Any()).Returns(new Dictionary<int, Result<Optional<int>>> { [season] = prequel });
		return prequels;
	}

	private static async IAsyncEnumerable<IdentifyRequest> AsyncOf(params IdentifyRequest[] requests)
	{
		await Task.Yield();
		foreach (var request in requests)
		{
			yield return request;
		}
	}

	private static Mock<IShowParts> Linking(IExternalMediaId part)
	{
		return Answering(part, Some.Of<IReadOnlyList<IExternalMediaId>>([TvdbId.From("431162", MediaType.Show), AniListId.From("161645", MediaType.Show)]));
	}

	private static Mock<IShowParts> Answering(IExternalMediaId part, Optional<IReadOnlyList<IExternalMediaId>> show)
	{
		var parts = IShowParts.Mock();
		parts.GetShowsAsync(Any(), Any()).Returns(new Dictionary<IExternalMediaId, Optional<IReadOnlyList<IExternalMediaId>>> { [part] = show });
		return parts;
	}

	private static Arg<IReadOnlyCollection<IExternalMediaId>> Parts(string[] expected)
	{
		return Is<IReadOnlyCollection<IExternalMediaId>>(ids => ids != null && ids.Select(id => $"{id}").Order().SequenceEqual(expected.Order()));
	}

	private static Arg<Optional<MediaId>> LinkedTo(MediaId show) => Is<Optional<MediaId>>(found => found.Map(id => id == show).GetValueOr(false));

	private static Arg<Optional<MediaId>> NoShow() => Is<Optional<MediaId>>(found => found.IsNone());

	private static IMediaService Settling(params Listed<MediaMatchResult>[] settled)
	{
		var media = IMediaService.Mock();
		media.LoadListAsync(Any<IAsyncEnumerable<ListTitle>>(), Any(), Any()).Returns((titles, _, cancellation) => SettledAsync(titles, settled, cancellation));
		return media.Object;
	}

	private static async IAsyncEnumerable<Listed<MediaMatchResult>> SettledAsync(
		IAsyncEnumerable<ListTitle> titles,
		Listed<MediaMatchResult>[] settled,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		await titles.ToListAsync(cancellationToken);
		foreach (var listed in settled)
		{
			yield return listed;
		}
	}

	private static MatchedMedia Matched(MediaId show) => new(new MediaIdentity(show, [TvdbId.From("431162", MediaType.Show)]), new ShowDetails());
}
