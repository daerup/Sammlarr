using Contracts.Feed;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;

namespace Core.Feed;

internal sealed class FeedService(IFeedRepository repository, IWantlistRepository wantlist, IFeedScheduler scheduler) : IFeedService
{
	public async Task<FeedSnapshot> GetAsync(CancellationToken cancellationToken = default)
	{
		var feeds = await repository.GetAllAsync(cancellationToken);
		var totals = await repository.GetRunTotalsAsync(cancellationToken);
		var onWantlist = await wantlist.CountByFeedAsync(cancellationToken);
		return new FeedSnapshot([.. feeds.Select(feed => FeedOverview.Of(feed, totals.GetValueOrDefault(feed.Id), onWantlist.GetValueOrDefault(feed.Id)))]);
	}

	public async Task<FeedId> CreateAsync(FeedCreateData data, CancellationToken cancellationToken = default)
	{
		var id = await repository.CreateAsync(data, cancellationToken);
		await RescheduleAsync(id, cancellationToken);
		return id;
	}

	public async Task UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default)
	{
		await repository.UpdateAsync(id, data, cancellationToken);
		await RescheduleAsync(id, cancellationToken);
	}

	public async Task DeleteAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		if (await repository.FindAsync(id, cancellationToken) is not Some<Domain.Feed.Feed>)
		{
			return;
		}

		await scheduler.UnscheduleAsync(id, cancellationToken);
		await repository.DeleteAsync(id, cancellationToken);
	}

	public async Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		await RescheduleAllAsync(await repository.PauseAsync(ids, cancellationToken), cancellationToken);

	public async Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		await RescheduleAllAsync(await repository.ResumeAsync(ids, cancellationToken), cancellationToken);

	public async Task<IReadOnlyList<FeedId>> PauseUsingAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		var affected = (await repository.GetAllAsync(cancellationToken))
			.Where(feed => feed.Provider.AsApiKeyProvider() == provider)
			.Select(feed => feed.Id)
			.ToList();
		return await PauseAsync(affected, cancellationToken);
	}

	public Task<IReadOnlyList<FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default) =>
		repository.GetRunsAsync(feedId, take, cancellationToken);

	public Task<IReadOnlyList<FeedRun>> GetRecentRunsAsync(DateTimeOffset since, int take, CancellationToken cancellationToken = default) =>
		repository.GetRecentRunsAsync(since, take, cancellationToken);

	public Task<int> CountRunsSinceAsync(FeedId feedId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
		repository.CountRunsSinceAsync(feedId, since, cancellationToken);

	public Task<Optional<FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) => repository.GetRunAsync(runId, cancellationToken);

	private async Task<IReadOnlyList<FeedId>> RescheduleAllAsync(IReadOnlyList<FeedId> changed, CancellationToken cancellationToken)
	{
		foreach (var id in changed)
		{
			await RescheduleAsync(id, cancellationToken);
		}

		return changed;
	}

	private async Task RescheduleAsync(FeedId id, CancellationToken cancellationToken)
	{
		if (await repository.FindAsync(id, cancellationToken) is not Some<Domain.Feed.Feed>(var feed))
		{
			return;
		}

		if (feed is { Enabled: true, Schedule: Scheduled schedule })
		{
			await scheduler.ScheduleAsync(id, schedule, cancellationToken);
		}
		else
		{
			await scheduler.UnscheduleAsync(id, cancellationToken);
		}
	}
}
