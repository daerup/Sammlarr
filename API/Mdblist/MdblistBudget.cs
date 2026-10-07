using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using Contracts;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistBudget(IDateTimeProvider clock, MdblistOptions options, ILogger<MdblistBudget> logger)
{
	private readonly ConcurrentDictionary<string, Allowance> _byKey = new();

	private readonly ConcurrentDictionary<(string ApiKey, DateTimeOffset ResetAt), byte> _warned = new();

	public void Record(string apiKey, HttpResponseHeaders headers)
	{
		Header(headers, "x-ratelimit-remaining")
			.Map(remaining => Header(headers, "x-ratelimit-reset").Map(reset => new Allowance(remaining, DateTimeOffset.FromUnixTimeSeconds(reset))))
			.Apply(allowance => _byKey[apiKey] = allowance);
	}

	public bool LeavesReserve(string apiKey)
	{
		return _byKey.GetValueOrNone(apiKey).Match(allowance => Leaves(apiKey, allowance), () => true);
	}

	private bool Leaves(string apiKey, Allowance allowance)
	{
		var leaves = allowance.Remaining > options.FeedReserve || clock.Now >= allowance.ResetAt;
		if (!leaves && _warned.TryAdd((apiKey, allowance.ResetAt), 0))
		{
			logger.LogWarning(
				"MDBList has {Remaining} requests left until {ResetAt}, so title lookups wait for the reset and the key's feeds keep the last {Reserve}",
				allowance.Remaining,
				allowance.ResetAt,
				options.FeedReserve
			);
		}

		return leaves;
	}

	private static Optional<long> Header(HttpResponseHeaders headers, string name)
	{
		return Optional
			.When(headers.TryGetValues(name, out var values), () => values!.First())
			.Map(value => Optional.When(long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), () => number));
	}

	private readonly record struct Allowance(long Remaining, DateTimeOffset ResetAt);
}
