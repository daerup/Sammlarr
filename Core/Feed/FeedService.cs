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
		return new FeedSnapshot([.. feeds.Select(feed => FeedOverview.Of(feed, totals.GetValueOrNone(feed.Id), onWantlist.GetValueOrDefault(feed.Id)))]);
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
		await repository
			.FindAsync(id, cancellationToken)
			.Apply(_ => scheduler.UnscheduleAsync(id, cancellationToken))
			.Apply(feed => wantlist.OrphanAsync(feed, cancellationToken))
			.Apply(_ => repository.DeleteAsync(id, cancellationToken));
	}

	public async Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default)
	{
		return await RescheduleAllAsync(await repository.PauseAsync(ids, cancellationToken), cancellationToken);
	}

	public async Task<IReadOnlyList<FeedId>> PauseAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		var affected = (await repository.GetAllAsync(cancellationToken))
			.Where(feed => feed.Provider.AsApiKeyProvider() == provider)
			.Select(feed => feed.Id)
			.ToList();
		return await PauseAsync(affected, cancellationToken);
	}

	public async Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default)
	{
		return await RescheduleAllAsync(await repository.ResumeAsync(ids, cancellationToken), cancellationToken);
	}

	public Task<IReadOnlyList<FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default)
	{
		return repository.GetRunsAsync(query, cancellationToken);
	}

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
		await repository.FindAsync(id, cancellationToken).Apply(feed => ApplyScheduleAsync(feed, cancellationToken));
	}

	private async Task ApplyScheduleAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken)
	{
		if (feed is { Enabled: true, Schedule: Scheduled schedule })
		{
			await scheduler.ScheduleAsync(feed.Id, schedule, cancellationToken);
		}
		else
		{
			await scheduler.UnscheduleAsync(feed.Id, cancellationToken);
		}
	}
}
