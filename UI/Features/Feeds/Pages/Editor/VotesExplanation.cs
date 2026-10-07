using Domain.Media;
using UI.Shared.Media.Ratings;

namespace UI.Features.Feeds.Pages.Editor;

internal enum VotesNote
{
	UnvotedTitlesFail,
	EveryTitleFails,
	Nothing,
}

internal sealed record VotesRow(RatingSource Source, Optional<int> Rank)
{
	internal string Name => Source.Card.Owner.GetDescription();

	internal bool HasVotes => Source.Card.PublishesVotes;
}

internal sealed record VotesExplanation(string Lead, IReadOnlyList<VotesRow> Rows, VotesNote Note)
{
	internal const string Heading = "Votes: how many people rated a title";

	internal string UnvotedLine => $"A title only {Unvoted(" or ")} rates has no votes and fails this rule.";

	internal string Warning =>
		$"Every title {Unvoted(" or ")} rates fails this rule, because none of them publishes votes. Switch a source with votes on with the";

	internal static VotesExplanation For(RatingStrategy strategy, bool isOwn, ValueSet<RatingSource> available)
	{
		var whose = isOwn ? $"this feed's own rating, {RatingTexts.Strategy(strategy)}" : $"your default, {RatingTexts.Strategy(strategy)}";
		return strategy switch
		{
			AveragedRating averaged => Averaged(averaged, whose, available),
			OrderedRating ordered => Ordered(ordered, whose, available),
		};
	}

	private static VotesExplanation Averaged(AveragedRating averaged, string whose, ValueSet<RatingSource> available)
	{
		List<VotesRow> rows = [.. averaged.Counted(available).Order().DistinctBy(source => source.Card.Owner).Select(source => new VotesRow(source, new None()))];
		if (rows.Count > 0 && rows.All(row => !row.HasVotes))
		{
			return new($"This feed's rating averages {string.Join(" and ", rows.Select(row => row.Name))} only:", rows, VotesNote.EveryTitleFails);
		}

		return new($"Added up from the providers this feed's rating averages ({whose}):", rows, Closing(rows));
	}

	private static VotesExplanation Ordered(OrderedRating ordered, string whose, ValueSet<RatingSource> available)
	{
		List<VotesRow> rows =
		[
			.. ordered.Order.Where(available.Contains).Select((source, index) => new VotesRow(source, Some.Of(index + 1))).DistinctBy(row => row.Source.Card.Owner),
		];
		return new($"Taken from the first of these that rated it ({whose}):", rows, Closing(rows));
	}

	private static VotesNote Closing(IReadOnlyList<VotesRow> rows) => rows.Any(row => !row.HasVotes) ? VotesNote.UnvotedTitlesFail : VotesNote.Nothing;

	private string Unvoted(string separator) => string.Join(separator, Rows.Where(row => !row.HasVotes).Select(row => row.Name));
}
