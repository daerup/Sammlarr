using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Wantlist.Sync;

internal sealed class SinkSwitch(
	IServiceScopeFactory scopes,
	IUserRepository users,
	IProviderConfigService configs,
	IArrSinkConfigService arr,
	WantlistSyncGate gate,
	ICurrentUser currentUser,
	ILogger<SinkSwitch> logger
) : ISinkSwitch
{
	public async Task<Result<SinkSwitched>> SwitchAsync(SinkKind sink, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return await MoveAsync(sink, await IsReadyAsync(sink, cancellationToken), () => StoreSinkAsync(sink, cancellationToken), cancellationToken);
	}

	public async Task<Result<SinkSwitched>> MoveAsync(ArrSinkConfig config, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		if ((await configs.GetAsync(cancellationToken)).Sink != SinkKind.Arr)
		{
			return Error.From("The wantlist does not sync to Radarr + Sonarr, so there is nothing to move.");
		}

		return await MoveAsync(SinkKind.Arr, config.IsSetUp, () => arr.StoreAsync(config, cancellationToken), cancellationToken);
	}

	private async Task<Result<SinkSwitched>> MoveAsync(SinkKind sink, bool ready, Func<Task> commit, CancellationToken cancellationToken)
	{
		if (!ready)
		{
			return Error.From("Radarr + Sonarr is not set up yet: both apps need an address, an API key, a quality profile and a root folder.");
		}

		var everyone = await users.GetAllAsync(cancellationToken);
		using var held = new HeldGates();
		foreach (var user in everyone)
		{
			(await gate.EnterAsync(user.Id, cancellationToken)).Apply(held.Add);
		}

		if (held.Count < everyone.Count)
		{
			return Error.From("A wantlist sync is running right now. Switch again once it has finished.");
		}

		var unaddressable = 0;
		foreach (var user in everyone)
		{
			unaddressable += await ConvertAsync(user.Id, sink, cancellationToken);
		}

		var queued = 0;
		foreach (var user in everyone)
		{
			queued += await RequeueAsync(user.Id, cancellationToken);
		}

		await commit();
		logger.LogInformation("The wantlist now syncs to {Sink}: queued {Queued} titles, {Unaddressable} without an id it can add by", sink, queued, unaddressable);
		return Success.Of(new SinkSwitched(queued, unaddressable));
	}

	private async Task StoreSinkAsync(SinkKind sink, CancellationToken cancellationToken)
	{
		var config = await configs.GetAsync(cancellationToken);
		await configs.StoreAsync(config with { Sink = sink }, cancellationToken);
	}

	private async Task<bool> IsReadyAsync(SinkKind sink, CancellationToken cancellationToken)
	{
		return sink switch
		{
			SinkKind.Plex => true,
			SinkKind.Arr => (await arr.GetAsync(cancellationToken)).IsSetUp,
		};
	}

	private async Task<int> ConvertAsync(UserId user, SinkKind sink, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		using var _ = await scope.ServiceProvider.GetRequiredService<IUserScope>().AsAsync(user, cancellationToken);
		var wantlist = scope.ServiceProvider.GetRequiredService<IWantlistService>();
		var titles = await wantlist.GetAsync(cancellationToken);
		var outbox = await wantlist.GetOutboxAsync(cancellationToken);
		var blocked = await scope.ServiceProvider.GetRequiredService<IBlocklistService>().GetAsync(cancellationToken);
		var missing = await scope
			.ServiceProvider.GetRequiredService<IMediaService>()
			.EnsureSinkIdsAsync([.. titles.Ids, .. outbox.Entries.Select(entry => entry.MediaId), .. blocked.Ids], sink, cancellationToken);
		return missing.Count(titles.Contains);
	}

	private async Task<int> RequeueAsync(UserId user, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		using var _ = await scope.ServiceProvider.GetRequiredService<IUserScope>().AsAsync(user, cancellationToken);
		var queued = await scope.ServiceProvider.GetRequiredService<IWantlistService>().RequeueAllAsync(cancellationToken);
		return queued.Count;
	}

	private sealed class HeldGates : IDisposable
	{
		private readonly List<IDisposable> leases = [];

		public int Count => leases.Count;

		public void Add(IDisposable lease) => leases.Add(lease);

		public void Dispose()
		{
			foreach (var lease in leases)
			{
				lease.Dispose();
			}
		}
	}
}
