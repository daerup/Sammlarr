using Domain.Media;
using UI.Shared.Media.Ratings;

namespace UI.Features.Feeds.Pages.Editor;

internal enum VotesNote
{
	RottenTomatoesTitlesFail,
	EveryTitleFails,
	Nothing,
}

internal sealed record VotesRow(RatingSource Source, Optional<int> Rank)
{
	internal string Name => Source.IsRottenTomatoes ? "Rotten Tomatoes" : RatingTexts.Name(Source);

	internal bool HasVotes => Source.PublishesVotes;
}

internal sealed record VotesExplanation(string Lead, IReadOnlyList<VotesRow> Rows, VotesNote Note)
{
	internal const string Heading = "Votes: how many people rated a title";

	internal const string RottenTomatoesLine = "A title only Rotten Tomatoes rates has no votes and fails this rule.";

	internal const string Warning = "Every title Rotten Tomatoes rates fails this rule, because it publishes no votes. Switch TMDB or IMDb on with the";

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
		List<VotesRow> rows = [.. averaged.Counted(available).Order().DistinctBy(Publisher).Select(source => new VotesRow(source, new None()))];
		if (rows.Count > 0 && rows.All(row => !row.HasVotes))
		{
			return new("This feed's rating averages Rotten Tomatoes only:", rows, VotesNote.EveryTitleFails);
		}

		return new($"Added up from the providers this feed's rating averages ({whose}):", rows, Closing(rows));
	}

	private static VotesExplanation Ordered(OrderedRating ordered, string whose, ValueSet<RatingSource> available)
	{
		List<VotesRow> rows =
		[
			.. ordered.Order.Where(available.Contains).Select((source, index) => new VotesRow(source, Some.Of(index + 1))).DistinctBy(row => Publisher(row.Source)),
		];
		return new($"Taken from the first of these that rated it ({whose}):", rows, Closing(rows));
	}

	private static VotesNote Closing(IReadOnlyList<VotesRow> rows) => rows.Any(row => !row.HasVotes) ? VotesNote.RottenTomatoesTitlesFail : VotesNote.Nothing;

	private static RatingSource Publisher(RatingSource source) => source.IsRottenTomatoes ? RatingSource.RottenTomatoesCritics : source;
}
