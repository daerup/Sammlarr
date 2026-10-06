using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Contracts;
using Contracts.Providers;
using Domain.Providers;

namespace API.Http;

internal sealed record ProviderWait(MediaProvider Provider, TimeSpan MaxWait, TimeSpan EnrichWait);

internal sealed class ProviderCapacity(ProviderGates gates, IDateTimeProvider clock, IEnumerable<ProviderWait> waits) : IProviderCapacity
{
	private static readonly TimeSpan DefaultBlock = TimeSpan.FromSeconds(30);

	private readonly ConcurrentDictionary<MediaProvider, DateTimeOffset> _blockedUntil = new();

	private readonly IReadOnlyDictionary<MediaProvider, ProviderWait> _waits = waits.DistinctBy(wait => wait.Provider).ToDictionary(wait => wait.Provider);

	public ProviderLoad Of(MediaProvider provider)
	{
		var blockedUntil = _blockedUntil.GetValueOrNone(provider);
		var wait = _waits.GetValueOrNone(provider);
		return new ProviderLoad(
			blockedUntil,
			gates.Busy(provider),
			wait.Map(found => found.MaxWait).GetValueOr(TimeSpan.Zero),
			wait.Map(found => found.EnrichWait).GetValueOr(TimeSpan.Zero)
		);
	}

	internal void Block(MediaProvider provider, RetryConditionHeaderValue? retryAfter) =>
		BlockUntil(provider, retryAfter?.Date ?? clock.Now + (retryAfter?.Delta ?? DefaultBlock));

	internal void Block(MediaProvider provider, TimeSpan duration) => BlockUntil(provider, clock.Now + duration);

	private void BlockUntil(MediaProvider provider, DateTimeOffset until) =>
		_blockedUntil.AddOrUpdate(provider, until, (_, current) => current > until ? current : until);
}

internal sealed class CapacityHandler(MediaProvider provider, ProviderCapacity capacity) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);
		if (response.StatusCode is HttpStatusCode.TooManyRequests)
		{
			capacity.Block(provider, response.Headers.RetryAfter);
		}
		return response;
	}
}
