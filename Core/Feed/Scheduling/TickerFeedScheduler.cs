using Contracts.Feed;
using Contracts.Identity;
using Domain.Feed;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Core.Feed.Scheduling;

internal sealed class TickerFeedScheduler(ICronTickerManager<CronTickerEntity> tickers, ICurrentUser currentUser) : IFeedScheduler
{
	public async Task ScheduleAsync(FeedId id, Scheduled schedule, CancellationToken cancellationToken = default)
	{
		await UnscheduleAsync(id, cancellationToken);
		var added = await tickers.AddAsync(Ticker(id, schedule), cancellationToken);
		if (!added.IsSucceeded)
		{
			throw new InvalidOperationException(FormattableString.Invariant($"Feed {id} could not be scheduled on '{schedule}'."), added.Exception);
		}
	}

	public async Task UnscheduleAsync(FeedId id, CancellationToken cancellationToken = default) => await tickers.DeleteAsync(id.Value, cancellationToken);

	private CronTickerEntity Ticker(FeedId id, Scheduled schedule) =>
		new()
		{
			Id = id.Value,
			Function = FeedRunTicker.FunctionName,
			Expression = schedule.ToString(),
			Request = TickerHelper.CreateTickerRequest(new ScheduledFeed(id, currentUser.Id, schedule)),
			IsEnabled = true,
		};
}
