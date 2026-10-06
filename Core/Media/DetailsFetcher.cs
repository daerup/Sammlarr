using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class DetailsFetcher(
	IKeyedServiceIndex<MediaProvider, IDetailsEnricher> enricherIndex,
	IProviderConfigService providerConfig,
	IProviderCapacity capacity,
	IApiKeyService apiKeys,
	ICredentialService credentials,
	IDateTimeProvider clock,
	ILogger<DetailsFetcher> logger
)
{
	private static readonly IReadOnlyList<MediaProvider> Enrichers = [.. Enum.GetValues<EnricherProvider>().Select(enricher => enricher.AsMediaProvider())];

	public async Task<IReadOnlySet<MediaProvider>> ConnectedAsync(CancellationToken cancellationToken)
	{
		var keys = Enum.GetValues<ApiKeyProvider>()
			.Where(provider => Enrichers.Contains(provider.AsMediaProvider()))
			.Select(provider => CheckAsync(provider.AsMediaProvider(), () => apiKeys.GetAsync(provider, cancellationToken), cancellationToken));
		var signIns = Enum.GetValues<AuthProvider>()
			.Select(signIn => signIn.Provider().When(Enrichers.Contains).Map(provider => (SignIn: signIn, Provider: provider)))
			.WhereSome()
			.Select(pair => CheckAsync(pair.Provider, () => credentials.GetAsync(pair.SignIn, cancellationToken), cancellationToken));
		var checks = await Task.WhenAll(keys.Concat(signIns));
		var disconnected = checks.Where(check => !check.Ready).Select(check => check.Provider).ToHashSet();
		return Enrichers.Where(provider => !disconnected.Contains(provider)).ToHashSet();
	}

	public Optional<CachedDetails> FindReusable(MediaIdentity identity, Optional<CachedDetails> cached, IReadOnlySet<MediaProvider> connected)
	{
		return cached.When(details => !NeedsRefetch(identity, details, connected));
	}

	public async Task<Result<MatchedMedia>> FetchAsync(
		MediaIdentity identity,
		Optional<CachedDetails> stored,
		IReadOnlySet<MediaProvider> connected,
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
			return unavailable.Match(provider => Error.From(new ProviderUnavailableException(provider)), () => Error.From("the details could not be fetched"));
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

	private bool NeedsRefetch(MediaIdentity identity, CachedDetails cached, IReadOnlySet<MediaProvider> connected)
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
			.Aggregate(MediaFields.None, (given, answer) => given | enricherIndex[answer.Provider].Fills.Except(answer.Unfilled));
	}

	private static IReadOnlySet<MediaProvider> AlreadyAnswered(FetchReason reason)
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

	private async Task<Optional<MediaProvider>> PreferredAsync(CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return config.SearchProvider.Provider();
	}

	private async Task<(MatchedMedia Enriched, List<ProviderAnswer> Answers, Optional<MediaProvider> Unavailable)> EnrichAsync(
		MediaIdentity identity,
		FetchReason reason,
		MediaDetails backup,
		IReadOnlySet<MediaProvider> connected,
		Optional<MediaProvider> preferred,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var known = identity;
		var fresh = MediaDetails.Empty(backup.Type);
		var current = Layered(reason, fresh, backup, answered: false);
		List<ProviderAnswer> answers = [];
		var asked = AlreadyAnswered(reason).ToHashSet();
		var waited = new HashSet<MediaProvider>();
		Optional<MediaProvider> unavailable = new None();
		while (current.Missing is var missing && !missing.IsEmpty)
		{
			var leftToAsk = EnrichersLeftToAsk(known, asked, connected);
			if (EnricherChoice.Next(missing, leftToAsk, preferred, now) is not Some<EnricherCandidate>(var next))
			{
				if (await WaitForBlockedAsync(missing, leftToAsk, waited, now, cancellationToken) is not Some<DateTimeOffset>(var freeAt))
				{
					unavailable = unavailable.Or(() => EnricherChoice.Blocked(missing, leftToAsk, now).Map(blocked => blocked.Provider));
					break;
				}

				now = freeAt;
				continue;
			}

			asked.Add(next.Provider);
			var answer = await AskAsync(next, known, cancellationToken);
			answer.ApplyError(error => unavailable = unavailable.Or(() => error.CausedBy<ProviderUnavailableException>().Map(cause => cause.Provider)));
			var fitting = answer.ToOptional().When(found => found.Fits(backup.Type));
			fitting.Apply(found => known = known with { Externals = known.Externals + found.Externals });
			fitting.Apply(found => fresh = fresh.FillFrom(found.Details));
			fitting.Apply(found => answers.Add(ProviderAnswer.After(next.Provider, next.Fills, found.Details, now)));
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
		HashSet<MediaProvider> waited,
		DateTimeOffset now,
		CancellationToken cancellationToken
	)
	{
		var blocked = EnricherChoice.WorthWaitingFor(missing, leftToAsk, waited, now);
		blocked.Apply(candidate => waited.Add(candidate.Provider));
		var freeAt = blocked.Map(candidate => candidate.Load.FreeAt(now));
		await freeAt.Apply(until => clock.WaitUntilAsync(until, cancellationToken));
		return freeAt;
	}

	private async Task<Result<FoundMedia>> AskAsync(EnricherCandidate candidate, MediaIdentity identity, CancellationToken cancellationToken)
	{
		try
		{
			return await enricherIndex[candidate.Provider].LookUpAsync(candidate.Id, cancellationToken);
		}
		catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning(exception, "{Provider} could not fill the details of {MediaId}", candidate.Provider, identity.Id);
			return Error.From(new ProviderUnavailableException(candidate.Provider, exception));
		}
	}

	private async Task<(MediaProvider Provider, bool Ready)> CheckAsync<TSecret>(
		MediaProvider provider,
		Func<ValueTask<Result<TSecret>>> load,
		CancellationToken cancellationToken
	)
		where TSecret : notnull
	{
		try
		{
			return (provider, await load().IsSuccess());
		}
		catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning(exception, "The {Provider} secret could not be read, so it fills no details", provider);
			return (provider, false);
		}
	}

	private List<EnricherCandidate> EnrichersLeftToAsk(MediaIdentity identity, IReadOnlySet<MediaProvider> asked, IReadOnlySet<MediaProvider> connected)
	{
		return
		[
			.. Enrichers
				.Where(provider => !asked.Contains(provider) && connected.Contains(provider))
				.Select(provider => AcceptedId(identity, provider).Map(id => CandidateFor(provider, id)))
				.WhereSome(),
		];
	}

	private Optional<IExternalMediaId> AcceptedId(MediaIdentity identity, MediaProvider provider)
	{
		return enricherIndex[provider].Accepts.Select(source => identity.Externals.OfProvider(source)).WhereSome().FirstOrNone();
	}

	private EnricherCandidate CandidateFor(MediaProvider provider, IExternalMediaId id)
	{
		return new EnricherCandidate(provider, id, enricherIndex[provider].Fills, capacity.Of(provider));
	}
}
