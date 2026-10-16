using Domain.Media;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class VotesExplanationTests
{
	private static readonly ValueSet<RatingSource> Everything = [.. Enum.GetValues<RatingSource>()];

	[Test]
	public async Task For_WhenYourDefaultAveragesEverything_ListsTheProvidersAndTheRottenTomatoesLine()
	{
		var explanation = VotesExplanation.For(new RatingStrategy(AveragedRating.Everything), isOwn: false, Everything);

		await Assert.That(explanation.Lead).Contains("your default, Average");
		await Assert
			.That(explanation.Rows.Select(row => (row.Name, row.HasVotes)))
			.IsEquivalentTo(
				[
					("TMDB", true),
					("IMDb", true),
					("Rotten Tomatoes®", false),
					("Metacritic", false),
					("Letterboxd", true),
					("Trakt", true),
					("MyAnimeList", true),
					("AniList", true),
				],
				CollectionOrdering.Matching
			);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.UnvotedTitlesFail);
		await Assert.That(explanation.UnvotedLine).IsEqualTo("A title only Rotten Tomatoes® or Metacritic rates has no votes and fails this rule.");
	}

	[Test]
	public async Task For_WhenTheFeedsOwnFirstAvailable_RanksTheRowsAsThePickerDoes()
	{
		var ordered = new OrderedRating([RatingSource.RottenTomatoesCritics, RatingSource.Tmdb, RatingSource.RottenTomatoesAudience]);

		var explanation = VotesExplanation.For(new RatingStrategy(ordered), isOwn: true, Everything);

		await Assert.That(explanation.Lead).Contains("this feed's own rating, First available");
		await Assert
			.That(explanation.Rows.Select(row => (row.Rank.GetValueOr(0), row.Name)))
			.IsEquivalentTo(
				[(1, "Rotten Tomatoes®"), (2, "TMDB"), (4, "IMDb"), (5, "Metacritic"), (6, "Letterboxd"), (7, "Trakt"), (8, "MyAnimeList"), (9, "AniList")],
				CollectionOrdering.Matching
			);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.UnvotedTitlesFail);
	}

	[Test]
	public async Task For_WhenFirstAvailable_SaysATitleWhoseFirstRatingHasNoVotesFails()
	{
		var explanation = VotesExplanation.For(new RatingStrategy(new OrderedRating()), isOwn: false, Everything);

		await Assert.That(explanation.UnvotedLine).IsEqualTo("A title whose first rating is Rotten Tomatoes® or Metacritic has no votes and fails this rule.");
	}

	[Test]
	public async Task For_WhenAverageCountsRottenTomatoesAlone_WarnsThatEveryTitleFails()
	{
		var averaged = new AveragedRating([RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]);

		var explanation = VotesExplanation.For(new RatingStrategy(averaged), isOwn: false, Everything);

		await Assert.That(explanation.Lead).Contains("Rotten Tomatoes®");
		await Assert.That(explanation.Rows.Select(row => row.Name)).IsEquivalentTo(["Rotten Tomatoes®"]);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.EveryTitleFails);
	}

	[Test]
	public async Task For_WhenOnlyTmdbIsAvailable_ListsTmdbAloneWithoutTheRottenTomatoesLine()
	{
		var averaged = new AveragedRating([RatingSource.RottenTomatoesCritics]);

		var explanation = VotesExplanation.For(new RatingStrategy(averaged), isOwn: false, [RatingSource.Tmdb]);

		await Assert.That(explanation.Rows.Select(row => row.Name)).IsEquivalentTo(["TMDB"]);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.Nothing);
	}
}
