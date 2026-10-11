using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using Contracts;
using Microsoft.Extensions.Logging;
using Shared.Concurrency;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistBudget(IDateTimeProvider clock, MdblistOptions options, ILogger<MdblistBudget> logger) : ILaneQuota
{
	private readonly ConcurrentDictionary<string, Allowance> _byKey = new();

	private readonly ConcurrentDictionary<(string ApiKey, DateTimeOffset ResetAt), byte> _warned = new();

	public void Record(string apiKey, HttpResponseHeaders headers)
	{
		Header(headers, "x-ratelimit-remaining")
			.Map(remaining => Header(headers, "x-ratelimit-reset").Map(reset => (Remaining: remaining, ResetAt: DateTimeOffset.FromUnixTimeSeconds(reset))))
			.Apply(seen =>
				_byKey.AddOrUpdate(apiKey, _ => new Allowance(seen.Remaining, seen.ResetAt, new None()), (_, before) => before.After(seen.Remaining, seen.ResetAt))
			);
	}

	public Optional<long> Left(string apiKey)
	{
		return _byKey.GetValueOrNone(apiKey).Map(allowance => Optional.When(clock.Now < allowance.ResetAt, () => allowance.Remaining));
	}

	public Optional<DateTimeOffset> BlockedUntil(string owner, LaneClass asking, DateTimeOffset now)
	{
		return _byKey.GetValueOrNone(owner).When(allowance => now < allowance.ResetAt && !Admits(owner, asking, allowance)).Map(allowance => allowance.ResetAt);
	}

	private bool Admits(string apiKey, LaneClass asking, Allowance allowance)
	{
		return asking switch
		{
			LaneClass.Source => true,
			LaneClass.Title or LaneClass.Interactive => AboveReserve(apiKey, allowance),
			LaneClass.Background => AboveReserve(apiKey, allowance)
				&& allowance.Highest.Match(highest => allowance.Remaining >= highest * options.BackgroundReserveShare, () => true),
		};
	}

	private bool AboveReserve(string apiKey, Allowance allowance)
	{
		var leaves = allowance.Remaining > options.FeedReserve;
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

	private readonly record struct Allowance(long Remaining, DateTimeOffset ResetAt, Optional<long> Highest)
	{
		private static readonly TimeSpan Drift = TimeSpan.FromMinutes(1);

		public Allowance After(long remaining, DateTimeOffset resetAt)
		{
			if (resetAt > ResetAt + Drift)
			{
				return new Allowance(remaining, resetAt, Some.Of(remaining));
			}

			return new Allowance(remaining, resetAt > ResetAt ? resetAt : ResetAt, Highest.Map(seen => Math.Max(seen, remaining)));
		}
	}
}
