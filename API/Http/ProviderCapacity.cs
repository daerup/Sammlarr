using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Contracts;
using Contracts.Providers;
using Domain.Providers;

namespace API.Http;

internal sealed class ProviderCapacity(ProviderConcurrencyGates gates, IDateTimeProvider clock) : IProviderCapacity
{
	private static readonly TimeSpan DefaultBlock = TimeSpan.FromSeconds(30);

	private readonly ConcurrentDictionary<MediaProvider, DateTimeOffset> _blockedUntil = new();

	public ProviderLoad Of(MediaProvider provider) => new(_blockedUntil.TryGetValue(provider, out var until) ? Some.Of(until) : new None(), gates.Busy(provider));

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
