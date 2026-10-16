using Domain.Feed.Trace;

namespace UI.Features.Feeds.Components;

public sealed record RunTally(FateTally Fates, Optional<int> Total, bool Estimated, int ElapsedMs, Optional<FeedRunEnd> Ended)
{
	private static readonly RunPart[] LegendOrder = [RunPart.Matched, RunPart.Known, RunPart.NotFound, RunPart.NotFoundEarlier, RunPart.CouldNotLookUp];

	internal static RunTally Before(Optional<int> previous) => new(FateTally.None, previous, previous.IsSome(), 0, new None());

	internal static RunTally Of(FateTally fates, Optional<int> requests, int elapsedMs, Optional<FeedRunEnd> ended, Optional<int> previous)
	{
		var exact = requests.Map(count => count + fates.ListedWithoutIds).Or(() => ended.Map(_ => fates.Settled));
		return new RunTally(fates, exact.Or(previous), exact.Match(false, previous.IsSome()), elapsedMs, ended);
	}

	internal RunProgress Progress { get; init; } = RunProgress.None;

	internal bool RateLimited { get; init; }

	internal int Done => Fates.Settled;

	internal int InFlight => Math.Max(0, Fates.Started - Fates.Matched - Fates.CouldNotLookUp - Fates.NotFound - Fates.Conflicted);

	internal int Count(RunPart part)
	{
		return part switch
		{
			RunPart.Known => Fates.Known,
			RunPart.NotFoundEarlier => Fates.Earlier,
			RunPart.Matched => Fates.Matched - Fates.Known,
			RunPart.NotFound => Fates.NotFound - Fates.Earlier + Fates.ListedWithoutIds,
			RunPart.CouldNotLookUp => Fates.CouldNotLookUp,
			RunPart.Conflict => Fates.Conflicted,
		};
	}

	internal static IReadOnlyList<RunPart> Legend => LegendOrder;

	internal static IReadOnlyList<RunPart> Bar => Enum.GetValues<RunPart>();

	internal double Percent(int count) => 100d * count / Math.Max(1, Math.Max(Total.GetValueOr(0), Done + InFlight));
}
