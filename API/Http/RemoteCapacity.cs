using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Contracts;
using Contracts.Providers;
using Domain.Providers;

namespace API.Http;

internal sealed record RemoteWait(RemoteService Remote, TimeSpan MaxWait, TimeSpan EnrichWait);

internal sealed class RemoteCapacity(ProviderGates gates, IDateTimeProvider clock, IEnumerable<RemoteWait> waits) : IRemoteCapacity
{
	private static readonly TimeSpan DefaultBlock = TimeSpan.FromSeconds(30);

	private readonly ConcurrentDictionary<RemoteService, DateTimeOffset> _blockedUntil = new();

	private readonly IReadOnlyDictionary<RemoteService, RemoteWait> _waits = waits.DistinctBy(wait => wait.Remote).ToDictionary(wait => wait.Remote);

	public RemoteLoad Of(RemoteService remote)
	{
		var blockedUntil = _blockedUntil.GetValueOrNone(remote);
		var wait = _waits.GetValueOrNone(remote);
		return new RemoteLoad(
			blockedUntil,
			gates.Busy(remote),
			wait.Map(found => found.MaxWait).GetValueOr(TimeSpan.Zero),
			wait.Map(found => found.EnrichWait).GetValueOr(TimeSpan.Zero)
		);
	}

	internal void Block(RemoteService remote, RetryConditionHeaderValue? retryAfter) =>
		BlockUntil(remote, retryAfter?.Date ?? clock.Now + (retryAfter?.Delta ?? DefaultBlock));

	internal void Block(RemoteService remote, TimeSpan duration) => BlockUntil(remote, clock.Now + duration);

	private void BlockUntil(RemoteService remote, DateTimeOffset until) =>
		_blockedUntil.AddOrUpdate(remote, until, (_, current) => current > until ? current : until);
}

internal sealed class CapacityHandler(RemoteService remote, RemoteCapacity capacity) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);
		if (response.StatusCode is HttpStatusCode.TooManyRequests)
		{
			capacity.Block(remote, response.Headers.RetryAfter);
		}
		return response;
	}
}
