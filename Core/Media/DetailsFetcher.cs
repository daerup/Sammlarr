using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Artwork;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Observability.Feed;

namespace Core.Media;

internal sealed class DetailsFetcher(
	IKeyedServiceSet<RemoteService, IDetailsEnricher> enrichers,
	IIdentityClaims claims,
	IProviderConfigService providerConfig,
	IRemoteCapacity capacity,
	IServiceReadiness readiness,
	IEnumerable<IArtworkResizer> resizers,
	IDateTimeProvider clock,
	ILogger<DetailsFetcher> logger
)
{
	private static readonly ArtworkSize AnySize = new(342, 513);

	public Task<IReadOnlySet<RemoteService>> ConnectedAsync(CancellationToken cancellationToken) => readiness.ConnectedAsync(enrichers.Keys, cancellationToken);

	public Optional<CachedDetails> FindReusable(MediaIdentity identity, Optional<CachedDetails> cached, IReadOnlySet<RemoteService> connected)
	{
		return cached.When(details => !NeedsRefetch(identity, details, connected));
	}

	public Task<Result<MatchedMedia>> LoadAsync(
		IdentifyHandoff handoff,
		MediaDetails placeholder,
		IReadOnlySet<RemoteService> connected,
		LibraryWriter writer,
		CancellationToken cancellationToken
	)
	{
		var cached = handoff.Stored.Map(row => row.Cached);
		return FindReusable(handoff.Identity, cached, connected)
			.Match(
				reusable => Task.FromResult<Result<MatchedMedia>>(Success.Of(new MatchedMedia(handoff.Identity, reusable.Details))),
				() => FetchOrFallBackAsync(handoff.Identity, handoff.Stored, cached, placeholder, connected, writer, cancellationToken)
			);
	}

	private async Task<Result<MatchedMedia>> FetchOrFallBackAsync(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		Optional<CachedDetails> cached,
		MediaDetails placeholder,
		IReadOnlySet<RemoteService> connected,
		LibraryWriter writer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var fetched = await FetchAsync(identity, cached, connected, writer, cancellationToken);
			return fetched.Match(_ => fetched, error => Fallback(identity, stored, cached, placeholder, error));
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			logger.Log(stored.Match(LogLevel.Warning, LogLevel.Error), ex, "Media details failed for {MediaId}", identity.Id);
			return Fallback(identity, stored, cached, placeholder, Error.From(ex));
		}
	}

	private static Result<MatchedMedia> Fallback(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		Optional<CachedDetails> cached,
		MediaDetails placeholder,
		Error error
	)
	{
		return cached
			.Map(stale => stale.Details)
			.Or(() => stored.Map(_ => placeholder))
			.Or(() => error.CausedBy<RemoteUnavailableException>().Map(_ => placeholder))
			.Map(details => new MatchedMedia(identity, details))
			.ToResult(error);
	}

	public async Task<Result<MatchedMedia>> FetchAsync(
		MediaIdentity identity,
		Optional<CachedDetails> stored,
		IReadOnlySet<RemoteService> connected,
		LibraryWriter writer,
		CancellationToken cancellationToken = default
	)
	{
		var now = clock.Now;
		var reason = stored.Map(previous => Reason(previous, now)).GetValueOr(new Uncached());
		var preferred = await PreferredAsync(cancellationToken);
		var (enriched, answered, outcomes, unavailable) = await EnrichAsync(
			identity,
			reason,
			Backup(reason, identity.Type, now),
			connected,
			preferred,
			cancellationToken
		);
		if (answered.Count == 0 && reason is not Gaps)
		{
			stored
				.When(_ => outcomes.Count > 0)
				.Apply(previous =>
					writer.Save(new DetailsChange(identity.Id, stored, previous with { AskedAt = clock.Now, Answers = Answers(reason, outcomes, now) }))
				);
			return unavailable.Match(remote => Error.From(new RemoteUnavailableException(remote, sent: false)), () => Error.From("the details could not be fetched"));
		}

		var identified = await KeepNewKeysAsync(identity, enriched.Identity, answered, cancellationToken);
		var askedAt = clock.Now;
		var cached = new CachedDetails(enriched.Details, RefreshedAt(reason, askedAt), askedAt, Answers(reason, outcomes, now));
		writer.Save(new DetailsChange(identity.Id, stored, cached));
		return Success.Of(enriched with { Identity = identified });
	}

	private async Task<MediaIdentity> KeepNewKeysAsync(
		MediaIdentity identity,
		MediaIdentity enriched,
		IEnumerable<RemoteService> answered,
		CancellationToken cancellationToken
	)
	{
		List<IIdentityKey> learned = [.. enriched.Keys.Where(key => !identity.Keys.Contains(key))];
		if (learned.Count == 0)
		{
			return identity;
		}

		var outcome = await claims.ClaimAsync(new IdentityClaim([.. learned], Some.Of(identity.Id), [.. answered]), cancellationToken);
		return outcome switch
		{
			Claimed claimed => new MediaIdentity(claimed.Id, identity.Keys + claimed.Keys),
			Conflicted => identity,
		};
	}

	private static FetchReason Reason(CachedDetails stored, DateTimeOffset now)
	{
		return stored.IsStale(now) ? new Stale(stored) : new Gaps(stored);
	}

	private bool NeedsRefetch(MediaIdentity identity, CachedDetails cached, IReadOnlySet<RemoteService> connected)
	{
		var leftToAsk = EnrichersLeftToAsk(identity, cached.Resting(clock.Now), connected);
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
			.Answers.Where(answer => answer.Failures == 0 && now - answer.AnsweredAt < CachedDetails.StaleAfter)
			.Aggregate(MediaFields.None, (given, answer) => given | Given(answer));
	}

	private MediaFields Given(ProviderAnswer answer)
	{
		return enrichers.Find(answer.Provider).Map(enricher => enricher.Fills.Except(answer.Unfilled)).GetValueOr(MediaFields.None);
	}

	private static Optional<CachedDetails> Stored(FetchReason reason)
	{
		return reason switch
		{
			Gaps gaps => Some.Of(gaps.Stored),
			Stale stale => Some.Of(stale.Stored),
			Uncached => new None(),
		};
	}

	private static Optional<ProviderAnswer> Previous(FetchReason reason, RemoteService remote)
	{
		return Stored(reason).Map(stored => stored.Answers.FirstOrNone(answer => answer.Provider == remote));
	}

	private static Optional<ProviderAnswer> Outcome(
		Answer<FoundMedia> answer,
		EnricherCandidate asked,
		Optional<ProviderAnswer> previous,
		MediaType type,
		DateTimeOffset now
	)
	{
		return answer switch
		{
			Found<FoundMedia> found => Optional.When(found.Value.Fits(type), () => ProviderAnswer.After(asked.Remote, asked.Fills, found.Value.Details, now)),
			NoMatch => Some.Of(new ProviderAnswer(asked.Remote, asked.Fills, now)),
			Unavailable { Sent: true } or Faulted => Some.Of(ProviderAnswer.Failed(asked.Remote, previous, now)),
			Unavailable => new None(),
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
			Stale stale => stale.Stored.Answers.Where(answer => answer.Failures > 0 || answer.AskableAt > now),
			Uncached => [],
		};
		return [.. fresh.UnionBy(previous, answer => answer.Provider)];
	}

	private async Task<IReadOnlySet<RemoteService>> PreferredAsync(CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return config.SearchProvider.Service.Remotes;
	}

	private async Task<(MatchedMedia Enriched, List<RemoteService> Answered, List<ProviderAnswer> Outcomes, Optional<RemoteService> Unavailable)> EnrichAsync(
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
		var gapFills = MediaDetails.Empty(backup.Type);
		var current = Layered(reason, fresh, backup, gapFills, answered: false);
		List<RemoteService> answered = [];
		List<ProviderAnswer> outcomes = [];
		var asked = Stored(reason).Map(stored => stored.Resting(now)).GetValueOr([]).ToHashSet();
		Optional<RemoteService> unavailable = new None();
		while (current.Missing is var missing && !missing.IsEmpty)
		{
			var leftToAsk = EnrichersLeftToAsk(known, asked, connected);
			var at = clock.Now;
			if (EnricherChoice.Next(missing, leftToAsk, preferred, at) is not Some<EnricherCandidate>(var next))
			{
				var stopped = EnricherChoice.Blocked(missing, leftToAsk, at).Map(blocked => blocked.Remote);
				stopped.Apply(remote => FeedRunSpans.GaveUp(remote, TraceStage.Details));
				unavailable = unavailable.Or(stopped);
				break;
			}

			asked.Add(next.Remote);
			var answer = await AskAsync(next, known, cancellationToken);
			Outcome(answer, next, Previous(reason, next.Remote), backup.Type, now).Apply(outcomes.Add);
			unavailable = unavailable.Or(() => Optional.When(!answer.IsDefinitive, () => next.Remote));
			var fitting = answer.ToOptional().When(found => found.Fits(backup.Type));
			fitting.Apply(found => known = known with { Keys = known.Keys + found.Externals.OfType<IIdentityKey>() });
			var deferred = fitting.Map(found => Deferred(next.Remote, found.Details)).GetValueOr(MediaFields.None);
			fitting.Apply(found => fresh = fresh.FillFrom(found.Details.Without(deferred)));
			fitting.Apply(found => gapFills = gapFills.FillFrom(found.Details.Only(deferred)));
			fitting.Apply(_ => answered.Add(next.Remote));
			current = Layered(reason, fresh, backup, gapFills, answered: answered.Count > 0);
		}

		return (new MatchedMedia(known, current), answered, outcomes, unavailable);
	}

	private static MediaDetails Layered(FetchReason reason, MediaDetails fresh, MediaDetails backup, MediaDetails gapFills, bool answered)
	{
		return reason is Stale && !answered ? fresh.FillFrom(gapFills) : fresh.FillFrom(backup).FillFrom(gapFills);
	}

	private MediaFields Deferred(RemoteService remote, MediaDetails details)
	{
		var defers = enrichers.Find(remote).Map(enricher => enricher.Defers).GetValueOr(MediaFields.None);
		return defers | Unresizable(details.PosterUrl, MediaField.Poster) | Unresizable(details.BackdropUrl, MediaField.Backdrop);
	}

	private MediaFields Unresizable(string? url, MediaField field)
	{
		return Optional
			.Of(url)
			.When(found => resizers.All(resizer => resizer.Resize(found, AnySize).IsNone()))
			.Map(_ => (MediaFields)field)
			.GetValueOr(MediaFields.None);
	}

	private async Task<Answer<FoundMedia>> AskAsync(EnricherCandidate candidate, MediaIdentity identity, CancellationToken cancellationToken)
	{
		using var span = FeedRunSpans.Hop(candidate.Remote, TraceStage.Details);
		var answer = await CallAsync(candidate, cancellationToken);
		span.Answered(answer.IsDefinitive ? HopOutcome.Answered : HopOutcome.Failed);
		if (answer is Faulted faulted)
		{
			faulted.Error.Exception.Apply(exception =>
				logger.LogWarning(exception, "{Remote} could not fill the details of {MediaId}", candidate.Remote, identity.Id)
			);
		}

		return answer;
	}

	private Task<Answer<FoundMedia>> CallAsync(EnricherCandidate candidate, CancellationToken cancellationToken)
	{
		return enrichers
			.Find(candidate.Remote)
			.Match(enricher => enricher.LookUpAsync(candidate.Id, cancellationToken), () => Task.FromResult<Answer<FoundMedia>>(new NoMatch(candidate.Remote)));
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
			.Accepts.Select(source => identity.Keys.OfProvider(source))
			.WhereSome()
			.FirstOrNone()
			.Map(id => new EnricherCandidate(remote, id, enricher.Fills, capacity.Of(remote)));
	}
}
