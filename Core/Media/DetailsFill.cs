using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Jobs;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using Shared.Concurrency;

namespace Core.Media;

internal sealed class DetailsFill(
	IWantlistService wantlist,
	IBlocklistService blocklist,
	IJobRepository jobs,
	IJobQueue queue,
	IServiceScopeFactory scopes,
	ICurrentUser currentUser
)
{
	private const int Page = 500;

	private static readonly FieldNeeds EveryFieldShown = new(MediaFields.None, MediaFields.Everything);

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var wanted = await wantlist.GetAsync(cancellationToken);
		var blocked = await blocklist.GetAsync(cancellationToken);
		var pages = wanted.Ids.Concat(blocked.Ids).Distinct().Chunk(Page).ToList();
		foreach (var (page, index) in pages.Select((page, index) => (page, index)))
		{
			if (index > 0 && await jobs.AnyQueuedAsync(JobKind.DetailsFill, cancellationToken))
			{
				await queue.EnqueueAsync(new FillDetails(), cancellationToken);
				return;
			}

			await FillAsync(page, cancellationToken);
		}
	}

	private async Task FillAsync(IReadOnlyList<MediaId> page, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		using var owner = scope.ServiceProvider.GetRequiredService<IUserScope>().As(currentUser);
		using var priority = LanePriority.Next(LaneClass.Background).Enter();
		await scope.ServiceProvider.GetRequiredService<IMediaService>().GetDetailsAsync(page, EveryFieldShown, cancellationToken).ToListAsync(cancellationToken);
	}
}
