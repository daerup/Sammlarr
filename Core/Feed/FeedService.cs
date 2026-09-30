using System.Runtime.CompilerServices;
using Contracts.DI;
using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;

namespace Core.Feed;

internal sealed class FeedService(IFeedRepository repository, IFeedScheduler scheduler, IKeyedServiceIndex<FeedProvider, IFeedProvider> providers)
	: IFeedService
{
	public Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default) => repository.GetFeedsAsync(cancellationToken);

	public Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default) =>
		repository.GetFeedByIdAsync(id, cancellationToken);

	public async Task<FeedId> CreateFeedAsync(FeedCreateData createData, CancellationToken cancellationToken = default)
	{
		var id = await repository.CreateFeedAsync(createData, cancellationToken);
		await RescheduleAsync(id, cancellationToken);
		return id;
	}

	public async Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default)
	{
		await repository.UpdateFeedAsync(id, updateData, cancellationToken);
		await RescheduleAsync(id, cancellationToken);
	}

	public async Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		if (await repository.GetFeedByIdAsync(id, cancellationToken) is null)
		{
			return;
		}

		await scheduler.UnscheduleAsync(id, cancellationToken);
		await repository.DeleteFeedAsync(id, cancellationToken);
	}

	public async Task DisableFeedsUsingAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		var providers = Enum.GetValues<FeedProvider>().Where(feedProvider => feedProvider.AsApiKeyProvider() == provider).ToList();
		foreach (var id in await repository.DisableFeedsAsync(providers, cancellationToken))
		{
			await scheduler.UnscheduleAsync(id, cancellationToken);
		}
	}

	public async Task PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		await RescheduleAllAsync(await repository.PauseAsync(ids, cancellationToken), cancellationToken);

	public async Task ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default) =>
		await RescheduleAllAsync(await repository.ResumeAsync(ids, cancellationToken), cancellationToken);

	private async Task RescheduleAllAsync(IReadOnlyList<FeedId> changed, CancellationToken cancellationToken)
	{
		foreach (var id in changed)
		{
			await RescheduleAsync(id, cancellationToken);
		}
	}

	private async Task RescheduleAsync(FeedId id, CancellationToken cancellationToken)
	{
		if (await repository.GetFeedByIdAsync(id, cancellationToken) is not { } feed)
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

	public async IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(Domain.Feed.Feed feed, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in providers[feed.Provider].GetFeedItemsAsync(feed.Identifier, cancellationToken))
		{
			yield return item;
		}
	}

	public Task<IReadOnlyList<FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default) =>
		repository.GetRunsAsync(feedId, take, cancellationToken);

	public Task<IReadOnlyList<FeedRun>> GetRecentRunsAsync(DateTimeOffset since, int take, CancellationToken cancellationToken = default) =>
		repository.GetRecentRunsAsync(since, take, cancellationToken);

	public Task<int> CountRunsSinceAsync(FeedId feedId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
		repository.CountRunsSinceAsync(feedId, since, cancellationToken);

	public Task<Optional<FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) => repository.GetRunAsync(runId, cancellationToken);

	public Task<FeedRunHistory> GetHistoryAsync(FeedId feedId, CancellationToken cancellationToken = default) =>
		repository.GetHistoryAsync(feedId, cancellationToken);

	public Task<IReadOnlyDictionary<FeedId, FeedOverview>> GetOverviewsAsync(CancellationToken cancellationToken = default) =>
		repository.GetOverviewsAsync(cancellationToken);
}
