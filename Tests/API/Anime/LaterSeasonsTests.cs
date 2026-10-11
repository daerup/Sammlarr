using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
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
		var testee = AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries());

		var asked = await testee.ShowsOf(AsyncOf(ThirdSeasonAsked), CancellationToken.None).SingleAsync();

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

		var asked = await AnimeMaps.Seasons(map).ShowsOf(AsyncOf(ThirdSeasonAsked), CancellationToken.None).SingleAsync();

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

		await Assert
			.That(shows.Select(show => $"{show.Candidates[0]}"))
			.IsEquivalentTo(["AniList:Show:161645", "AniList:Show:161645", "AniList:Show:999999"], CollectionOrdering.Matching);
		map.FindManyAsync(Any(), Any()).WasCalled(Times.Exactly(2));
		map.FindAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ShowsOf_TheShowItself_LeavesTheRequestAsItIs()
	{
		var request = new IdentifyRequest([AniListId.From("161645", MediaType.Show)], new None());

		var asked = await AnimeMaps.Seasons(AnimeMaps.KnowingTheApothecaryDiaries()).ShowsOf(AsyncOf(request), CancellationToken.None).SingleAsync();

		await Assert.That(asked).IsSameReferenceAs(request);
	}

	[Test]
	public async Task ShowsOf_AnIdTheMapDoesNotKnow_LeavesTheRequestAsItIs()
	{
		var asked = await AnimeMaps.Seasons(AnimeMaps.Unknown()).ShowsOf(AsyncOf(ThirdSeasonAsked), CancellationToken.None).SingleAsync();

		await Assert.That(asked).IsSameReferenceAs(ThirdSeasonAsked);
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

	private static async IAsyncEnumerable<IdentifyRequest> AsyncOf(IdentifyRequest request)
	{
		await Task.Yield();
		yield return request;
	}
}
