using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using Contracts;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistBudget(IDateTimeProvider clock, MdblistOptions options)
{
	private readonly ConcurrentDictionary<string, Allowance> _byKey = new();

	public void Record(string apiKey, HttpResponseHeaders headers)
	{
		Header(headers, "x-ratelimit-remaining")
			.Map(remaining => Header(headers, "x-ratelimit-reset").Map(reset => new Allowance(remaining, DateTimeOffset.FromUnixTimeSeconds(reset))))
			.Apply(allowance => _byKey[apiKey] = allowance);
	}

	public bool LeavesReserve(string apiKey)
	{
		return _byKey.GetValueOrNone(apiKey).Match(allowance => allowance.Remaining > options.FeedReserve || clock.Now >= allowance.ResetAt, () => true);
	}

	private static Optional<long> Header(HttpResponseHeaders headers, string name)
	{
		return Optional
			.When(headers.TryGetValues(name, out var values), () => values!.First())
			.Map(value => Optional.When(long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), () => number));
	}

	private readonly record struct Allowance(long Remaining, DateTimeOffset ResetAt);
}
