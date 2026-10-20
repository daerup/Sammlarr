using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using API.Http.Lanes;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace API.Mdblist;

internal sealed class MdblistBudget(TimeProvider clock, MdblistOptions options, ILogger<MdblistBudget> logger) : ILaneQuota
{
	private readonly ConcurrentDictionary<string, Allowance> _byKey = new();

	private readonly ConcurrentDictionary<(string ApiKey, DateTimeOffset ResetAt), byte> _warned = new();

	private static readonly TimeSpan Day = TimeSpan.FromDays(1);

	public void Record(string apiKey, HttpResponseMessage answer)
	{
		var headers = answer.Headers;
		var spent = answer.StatusCode is not HttpStatusCode.TooManyRequests;
		Header(headers, "x-ratelimit-remaining")
			.Map(remaining =>
				Header(headers, "x-ratelimit-reset")
					.Map(reset => new Reading(remaining, DateTimeOffset.FromUnixTimeSeconds(reset), Header(headers, "x-ratelimit-limit"), spent))
			)
			.Apply(seen => _byKey.AddOrUpdate(apiKey, _ => Allowance.First(seen), (_, before) => before.After(seen, options.FeedReserve, clock.GetUtcNow())));
	}

	public QuotaToday Today(string apiKey)
	{
		var now = clock.GetUtcNow();
		var known = _byKey.GetValueOrNone(apiKey);
		var nextReset = known
			.Map(allowance => Ahead(allowance.ResetAt, now))
			.GetValueOr(() => Ahead(now.UtcDateTime.Date.Add(options.DailyResetUtc.ToTimeSpan()), now));
		return new QuotaToday(nextReset, known.When(allowance => now < allowance.ResetAt).Map(Answered));
	}

	private QuotaAllowance Answered(Allowance allowance)
	{
		return new QuotaAllowance(
			allowance.Remaining,
			allowance.ResetAt,
			allowance.Daily,
			allowance.Outside,
			allowance.ReserveReachedAt,
			options.FeedReserve,
			options.BackgroundReserveShare
		)
		{
			DayBefore = allowance.Ended,
		};
	}

	private static DateTimeOffset Ahead(DateTimeOffset reset, DateTimeOffset now)
	{
		var behind = Math.Max(0, Math.Floor((now - reset) / Day) + 1);
		return reset + Day * behind;
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
				&& allowance.Limit.Or(allowance.Highest).Match(daily => allowance.Remaining >= daily * options.BackgroundReserveShare, () => true),
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

	private readonly record struct Reading(long Remaining, DateTimeOffset ResetAt, Optional<long> Limit, bool Spent)
	{
		public long Counted => Spent ? 1 : 0;
	}

	private sealed record Allowance(
		long Remaining,
		DateTimeOffset ResetAt,
		Optional<long> Highest,
		Optional<long> Limit,
		long Answers,
		Optional<DateTimeOffset> ReserveReachedAt
	)
	{
		private static readonly TimeSpan Drift = TimeSpan.FromMinutes(1);

		public Optional<DayEnd> Ended { get; init; } = new None();

		public Optional<long> Daily => Limit.Or(() => Highest.Map(highest => highest + 1));

		public Optional<long> Outside
		{
			get { return Highest.Map(highest => Limit.GetValueOr(highest + 1) - Remaining - Answers).When(outside => outside > 0); }
		}

		public static Allowance First(Reading seen) => new(seen.Remaining, seen.ResetAt, new None(), seen.Limit, seen.Counted, new None());

		public Allowance After(Reading seen, long reserve, DateTimeOffset now)
		{
			if (seen.ResetAt < ResetAt - Drift)
			{
				return this;
			}

			if (seen.ResetAt > ResetAt + Drift)
			{
				return new Allowance(seen.Remaining, seen.ResetAt, Some.Of(seen.Remaining), seen.Limit.Or(Limit), seen.Counted, new None())
				{
					Ended = Some.Of(new DayEnd(Remaining, ResetAt, ReserveReachedAt)),
				};
			}

			var crossed = Remaining > reserve && seen.Remaining <= reserve;
			return this with
			{
				Remaining = Math.Min(Remaining, seen.Remaining),
				ResetAt = seen.ResetAt > ResetAt ? seen.ResetAt : ResetAt,
				Highest = Highest.Map(highest => Math.Max(highest, seen.Remaining)),
				Limit = seen.Limit.Or(Limit),
				Answers = Answers + seen.Counted,
				ReserveReachedAt = ReserveReachedAt.Or(() => Optional.When(crossed, () => now)),
			};
		}
	}
}
