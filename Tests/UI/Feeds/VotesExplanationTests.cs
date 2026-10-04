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

		await Assert.That(explanation.Lead).IsEqualTo("Added up from the providers this feed's rating averages (your default, Average):");
		await Assert
			.That(explanation.Rows.Select(row => (row.Name, row.HasVotes)))
			.IsEquivalentTo([("TMDB", true), ("IMDb", true), ("Rotten Tomatoes", false)], CollectionOrdering.Matching);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.RottenTomatoesTitlesFail);
	}

	[Test]
	public async Task For_WhenTheFeedsOwnFirstAvailable_RanksTheRowsAsThePickerDoes()
	{
		var ordered = new OrderedRating([RatingSource.RottenTomatoesCritics, RatingSource.Tmdb, RatingSource.RottenTomatoesAudience]);

		var explanation = VotesExplanation.For(new RatingStrategy(ordered), isOwn: true, Everything);

		await Assert.That(explanation.Lead).IsEqualTo("Taken from the first of these that rated it (this feed's own rating, First available):");
		await Assert
			.That(explanation.Rows.Select(row => (row.Rank.GetValueOr(0), row.Name)))
			.IsEquivalentTo([(1, "Rotten Tomatoes"), (2, "TMDB"), (4, "IMDb")], CollectionOrdering.Matching);
		await Assert.That(explanation.Note).IsEqualTo(VotesNote.RottenTomatoesTitlesFail);
	}

	[Test]
	public async Task For_WhenAverageCountsRottenTomatoesAlone_WarnsThatEveryTitleFails()
	{
		var averaged = new AveragedRating([RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]);

		var explanation = VotesExplanation.For(new RatingStrategy(averaged), isOwn: false, Everything);

		await Assert.That(explanation.Lead).IsEqualTo("This feed's rating averages Rotten Tomatoes only:");
		await Assert.That(explanation.Rows.Select(row => row.Name)).IsEquivalentTo(["Rotten Tomatoes"]);
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
