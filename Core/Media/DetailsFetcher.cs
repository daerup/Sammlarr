using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class DetailsFetcher(
	IKeyedServiceSet<RemoteService, IDetailsEnricher> enrichers,
	IProviderConfigService providerConfig,
	IRemoteCapacity capacity,
	IServiceReadiness readiness,
	IDateTimeProvider clock,
	ILogger<DetailsFetcher> logger
)
{
	public Task<IReadOnlySet<RemoteService>> ConnectedAsync(CancellationToken cancellationToken) => readiness.ConnectedAsync(enrichers.Keys, cancellationToken);

	public Optional<CachedDetails> FindReusable(MediaIdentity identity, Optional<CachedDetails> cached, IReadOnlySet<RemoteService> connected)
	{
		return cached.When(details => !NeedsRefetch(identity, details, connected));
	}

	public async Task<Result<MatchedMedia>> FetchAsync(
		MediaIdentity identity,
		Optional<CachedDetails> stored,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken = default
	)
	{
		var now = clock.Now;
		var reason = stored.Map(previous => Reason(previous, now)).GetValueOr(new Uncached());
		var preferred = await PreferredAsync(cancellationToken);
		var (enriched, answers, unavailable) = await EnrichAsync(identity, reason, Backup(reason, identity.Type, now), connected, preferred, cancellationToken);
		if (answers.Count == 0 && reason is not Gaps)
		{
			return unavailable.Match(remote => Error.From(new RemoteUnavailableException(remote)), () => Error.From("the details could not be fetched"));
		}

		var askedAt = clock.Now;
		var cached = new CachedDetails(enriched.Details, RefreshedAt(reason, askedAt), askedAt, Answers(reason, answers, now));
		buffer.Add(enriched, cached);
		return Success.Of(enriched);
	}

	private static FetchReason Reason(CachedDetails stored, DateTimeOffset now)
	{
		return stored.IsStale(now) ? new Stale(stored) : new Gaps(stored);
	}

	private bool NeedsRefetch(MediaIdentity identity, CachedDetails cached, IReadOnlySet<RemoteService> connected)
	{
		var leftToAsk = EnrichersLeftToAsk(identity, cached.Answered, connected);
		return EnricherChoice.NeedsRefetch(cached, leftToAsk, clock.Now);
	}

	private MediaDetails Backup(FetchReason reason, MediaType type, DateTimeOffset now)
	{
		return reason switch
		{
			Uncached => MediaDetails.Empty(type),
			Gaps gaps => gaps.Stored.Details,
			Stale stale => stale.Stored.Details.Only(GivenWithin(stale.Stored, now)),
		};
	}

	private MediaFields GivenWithin(CachedDetails stored, DateTimeOffset now)
	{
		return stored
			.Answers.Where(answer => now - answer.AnsweredAt < CachedDetails.StaleAfter)
			.Aggregate(MediaFields.None, (given, answer) => given | Given(answer));
	}

	private MediaFields Given(ProviderAnswer answer)
	{
		return enrichers.Find(answer.Provider).Map(enricher => enricher.Fills.Except(answer.Unfilled)).GetValueOr(MediaFields.None);
	}

	private static IReadOnlySet<RemoteService> AlreadyAnswered(FetchReason reason)
	{
		return reason switch
		{
			Gaps gaps => gaps.Stored.Answered,
			Uncached or Stale => [],
		};
	}

	private static DateTimeOffset RefreshedAt(FetchReason reason, DateTimeOffset now)
	{
		return reason switch
		{
			Gaps gaps => gaps.Stored.RefreshedAt,
			Uncached or Stale => now,
		};
	}

	private static ValueSet<ProviderAnswer> Answers(FetchReason reason, IEnumerable<ProviderAnswer> fresh, DateTimeOffset now)
	{
		IEnumerable<ProviderAnswer> previous = reason switch
		{
			Gaps gaps => gaps.Stored.Answers,
			Stale stale => stale.Stored.Answers.Where(answer => now - answer.AnsweredAt < CachedDetails.StaleAfter),
			Uncached => [],
		};
		return [.. fresh.UnionBy(previous, answer => answer.Provider)];
	}

	private async Task<IReadOnlySet<RemoteService>> PreferredAsync(CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return config.SearchProvider.Service.Remotes;
	}

	private async Task<(MatchedMedia Enriched, List<ProviderAnswer> Answers, Optional<RemoteService> Unavailable)> EnrichAsync(
		MediaIdentity identity,
		FetchReason reason,
		MediaDetails backup,
		IReadOnlySet<RemoteService> connected,
		IReadOnlySet<RemoteService> preferred,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var known = identity;
		var fresh = MediaDetails.Empty(backup.Type);
		var current = Layered(reason, fresh, backup, answered: false);
		List<ProviderAnswer> answers = [];
		var asked = AlreadyAnswered(reason).ToHashSet();
		var waited = new HashSet<RemoteService>();
		Optional<RemoteService> unavailable = new None();
		while (current.Missing is var missing && !missing.IsEmpty)
		{
			var leftToAsk = EnrichersLeftToAsk(known, asked, connected);
			if (EnricherChoice.Next(missing, leftToAsk, preferred, now) is not Some<EnricherCandidate>(var next))
			{
				if (await WaitForBlockedAsync(missing, leftToAsk, waited, now, cancellationToken) is not Some<DateTimeOffset>(var freeAt))
				{
					unavailable = unavailable.Or(() => EnricherChoice.Blocked(missing, leftToAsk, now).Map(blocked => blocked.Remote));
					break;
				}

				now = freeAt;
				continue;
			}

			asked.Add(next.Remote);
			var answer = await AskAsync(next, known, cancellationToken);
			answer.ApplyError(error => unavailable = unavailable.Or(() => error.CausedBy<RemoteUnavailableException>().Map(cause => cause.Remote)));
			var fitting = answer.ToOptional().When(found => found.Fits(backup.Type));
			fitting.Apply(found => known = known with { Externals = known.Externals + found.Externals });
			fitting.Apply(found => fresh = fresh.FillFrom(found.Details));
			fitting.Apply(found => answers.Add(ProviderAnswer.After(next.Remote, next.Fills, found.Details, now)));
			current = Layered(reason, fresh, backup, answered: answers.Count > 0);
		}

		return (new MatchedMedia(known, current), answers, unavailable);
	}

	private static MediaDetails Layered(FetchReason reason, MediaDetails fresh, MediaDetails backup, bool answered)
	{
		return reason is Stale && !answered ? fresh : fresh.FillFrom(backup);
	}

	private async Task<Optional<DateTimeOffset>> WaitForBlockedAsync(
		MediaFields missing,
		IEnumerable<EnricherCandidate> leftToAsk,
		HashSet<RemoteService> waited,
		DateTimeOffset now,
		CancellationToken cancellationToken
	)
	{
		var blocked = EnricherChoice.WorthWaitingFor(missing, leftToAsk, waited, now);
		blocked.Apply(candidate => waited.Add(candidate.Remote));
		var freeAt = blocked.Map(candidate => candidate.Load.FreeAt(now));
		await freeAt.Apply(until => clock.WaitUntilAsync(until, cancellationToken));
		return freeAt;
	}

	private async Task<Result<FoundMedia>> AskAsync(EnricherCandidate candidate, MediaIdentity identity, CancellationToken cancellationToken)
	{
		try
		{
			return await enrichers
				.Find(candidate.Remote)
				.ToResult($"{candidate.Remote} fills no details")
				.Map(enricher => enricher.LookUpAsync(candidate.Id, cancellationToken));
		}
		catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning(exception, "{Remote} could not fill the details of {MediaId}", candidate.Remote, identity.Id);
			return Error.From(new RemoteUnavailableException(candidate.Remote, exception));
		}
	}

	private List<EnricherCandidate> EnrichersLeftToAsk(MediaIdentity identity, IReadOnlySet<RemoteService> asked, IReadOnlySet<RemoteService> connected)
	{
		return
		[
			.. enrichers
				.Keys.Where(remote => !asked.Contains(remote) && connected.Contains(remote))
				.Select(remote => enrichers.Find(remote).Map(enricher => Candidate(identity, remote, enricher)))
				.WhereSome(),
		];
	}

	private Optional<EnricherCandidate> Candidate(MediaIdentity identity, RemoteService remote, IDetailsEnricher enricher)
	{
		return enricher
			.Accepts.Select(source => identity.Externals.OfProvider(source))
			.WhereSome()
			.FirstOrNone()
			.Map(id => new EnricherCandidate(remote, id, enricher.Fills, capacity.Of(remote)));
	}
}
