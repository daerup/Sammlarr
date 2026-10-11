using API.Anime;
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

	[Test]
	public async Task ShowsOf_ALaterSeason_AsksForItsShowByTheShowsOwnIdsWithoutHints()
	{
		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries()), ThirdSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
		await Assert.That(asked.Hints).IsNone();
		await Assert.That(asked.Description.Map(hints => hints.Title)).IsSome().And.IsEqualTo("Kusuriya no Hitorigoto 3rd Season");
	}

	[Test]
	public async Task ShowsOf_ALaterSeasonOfASeriesNoEntryStandsFor_AsksByTheSeriesTvdbIdAlone()
	{
		var map = AnimeMaps.Unknown();
		map.FindAsync(Any(), Any()).Returns((id, _) => Optional.When(id is not TvdbId, () => AnimeMaps.ThirdSeason));
		map.FindManyAsync(Any(), Any()).Returns((ids, _) => ids.Where(id => id is not TvdbId).Distinct().ToDictionary(id => id, _ => AnimeMaps.ThirdSeason));

		var asked = await AskedAsync(AnimeMaps.Seasons(map), ThirdSeasonAsked);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(["Tvdb:Show:431162"]);
	}

	[Test]
	public async Task ShowsOf_ForAPageOfShows_ReadsTheMapTwice()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		IdentifyRequest[] page =
		[
			ThirdSeasonAsked,
			new IdentifyRequest([AniListId.From("161645", MediaType.Show)], new None()),
			new IdentifyRequest([AniListId.From("999999", MediaType.Show)], new None()),
		];

		var shows = await AnimeMaps.Seasons(map).ShowsOf(page.ToAsyncEnumerable(), CancellationToken.None).ToListAsync();

		List<IdentifyRequest> asked = [];
		foreach (var show in shows)
		{
			asked.Add((await Assert.That(show).IsOfUnionType<ListTitle, IdentifyRequest>())!);
		}

		await Assert
			.That(asked.Select(request => $"{request.Candidates[0]}"))
			.IsEquivalentTo(["AniList:Show:161645", "AniList:Show:161645", "AniList:Show:999999"], CollectionOrdering.Matching);
		map.FindManyAsync(Any(), Any()).WasCalled(Times.Exactly(2));
		map.FindAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ShowsOf_TheShowItself_LeavesTheRequestAsItIs()
	{
		var request = new IdentifyRequest([AniListId.From("161645", MediaType.Show)], new None());

		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries()), request);

		await Assert.That(asked).IsSameReferenceAs(request);
	}

	[Test]
	public async Task ShowsOf_AnIdTheMapDoesNotKnow_LeavesTheRequestAsItIs()
	{
		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.Unknown()), ThirdSeasonAsked);

		await Assert.That(asked).IsSameReferenceAs(ThirdSeasonAsked);
	}

	[Test]
	public async Task ShowsOf_ALaterSeasonTheMapHasNoIdsFor_AsksForTheShowItsPrequelBelongsToAndCreditsAniList()
	{
		var request = new IdentifyRequest([MalId.From("60001", MediaType.Show)], new None());

		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 59130).Object), request);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
		await Assert.That(asked.Sources).Contains(RemoteService.AniList);
	}

	[Test]
	public async Task ShowsOf_ASecondSeasonWhosePrequelIsTheShowItself_AsksForThatShow()
	{
		var request = new IdentifyRequest([MalId.From("60001", MediaType.Show)], new None());

		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 54492).Object), request);

		await Assert.That(asked.Candidates.Select(id => $"{id}")).IsEquivalentTo(AnimeMaps.ApothecaryDiariesIds);
	}

	[Test]
	public async Task ShowsOf_ALaterSeasonWhosePrequelHasNoIdsEither_LeavesTheRequestAsItIs()
	{
		var request = new IdentifyRequest([MalId.From("60001", MediaType.Show)], new None());

		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, 60002).Object), request);

		await Assert.That(asked).IsSameReferenceAs(request);
	}

	[Test]
	public async Task ShowsOf_WhenAniListCannotSayWhatCameBefore_CouldNotLookUpTheSeason()
	{
		var request = new IdentifyRequest([MalId.From("60001", MediaType.Show)], new None());
		var testee = AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: Prequel(60001, Error.From("AniList answered 429")).Object);

		var shown = await testee.ShowsOf(AsyncOf(request), CancellationToken.None).SingleAsync();

		var unmatched = await Assert.That(shown).IsOfUnionType<ListTitle, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp);
	}

	[Test]
	public async Task ShowsOf_AMovie_NeverAsksForAPrequel()
	{
		var prequels = AnimeMaps.NoPrequels();
		var request = new IdentifyRequest([MalId.From("60003", MediaType.Movie)], new None());

		var asked = await AskedAsync(AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries(), prequels: prequels.Object), request);

		await Assert.That(asked).IsSameReferenceAs(request);
		prequels.PrequelsOfAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ShowsOf_BeforeTheMapsFirstLoad_FailsTheRead()
	{
		var testee = AnimeMaps.Seasons(AnimeMaps.Unknown(), loaded: false);

		await Assert
			.That(async () => await testee.ShowsOf(AsyncOf(ThirdSeasonAsked), CancellationToken.None).ToListAsync())
			.Throws<InvalidOperationException>()
			.WithMessage("The anime id map has not been loaded yet.");
	}

	private static async Task<IdentifyRequest> AskedAsync(LaterSeasons testee, IdentifyRequest request)
	{
		var shown = await testee.ShowsOf(AsyncOf(request), CancellationToken.None).SingleAsync();
		return (await Assert.That(shown).IsOfUnionType<ListTitle, IdentifyRequest>())!;
	}

	private static Mock<IAnimePrequels> Prequel(int season, int prequel) => Prequel(season, Success.Of<Optional<int>>(Some.Of(prequel)));

	private static Mock<IAnimePrequels> Prequel(int season, Result<Optional<int>> prequel)
	{
		var prequels = IAnimePrequels.Mock();
		prequels.PrequelsOfAsync(Any(), Any()).Returns(new Dictionary<int, Result<Optional<int>>> { [season] = prequel });
		return prequels;
	}

	private static async IAsyncEnumerable<IdentifyRequest> AsyncOf(IdentifyRequest request)
	{
		await Task.Yield();
		yield return request;
	}
}
