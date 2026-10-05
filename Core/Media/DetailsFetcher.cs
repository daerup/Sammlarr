using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class DetailsFetcher(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IDetailsEnricher> enricherIndex,
	IProviderConfigService providerConfig,
	IProviderCapacity capacity,
	IApiKeyService apiKeys,
	ICredentialService credentials,
	IDateTimeProvider clock,
	ILogger<DetailsFetcher> logger
)
{
	private static readonly DetailsSource TmdbDetails = new(SearchProvider.Tmdb, [MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Imdb]);

	private static readonly DetailsSource PlexDetails = new(SearchProvider.Plex, [MediaProvider.Plex]);

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
		return await stored
			.When(previous => !previous.IsStale(clock.Now))
			.Match(
				previous => FillGapsAsync(identity, previous, connected, buffer, cancellationToken),
				() => RefreshAsync(identity, stored, connected, buffer, cancellationToken)
			);
	}

	private bool NeedsRefetch(MediaIdentity identity, CachedDetails cached, IReadOnlySet<MediaProvider> connected)
	{
		var leftToAsk = EnrichersLeftToAsk(new MatchedMedia(identity, cached.Details), cached.Answered, connected);
		return EnricherChoice.NeedsRefetch(cached, leftToAsk, clock.Now);
	}

	private async Task<Result<MatchedMedia>> FillGapsAsync(
		MediaIdentity identity,
		CachedDetails stored,
		IReadOnlySet<MediaProvider> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var (enriched, answers) = await EnrichAsync(new MatchedMedia(identity, stored.Details), stored.Answers, connected, cancellationToken);
		var filled = stored with { Details = enriched.Details, AskedAt = clock.Now, Answers = answers };
		return Success.Of(Store(enriched, filled, buffer));
	}

	private async Task<Result<MatchedMedia>> RefreshAsync(
		MediaIdentity identity,
		Optional<CachedDetails> stored,
		IReadOnlySet<MediaProvider> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var primary = Primary(identity, config.SearchProvider);
		return await LookUpAsync(identity, primary, cancellationToken)
			.Map(found => RefreshFromAsync(found, primary.Reads[0], stored, connected, buffer, cancellationToken));
	}

	private async Task<MatchedMedia> RefreshFromAsync(
		MatchedMedia found,
		MediaProvider primary,
		Optional<CachedDetails> stored,
		IReadOnlySet<MediaProvider> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var primaryAnswer = ProviderAnswer.After(primary, enricherIndex[primary].Fills, found.Details);
		var (enriched, answers) = await EnrichAsync(found, [primaryAnswer], connected, cancellationToken);
		var now = clock.Now;
		var refreshed = new CachedDetails(enriched.Details, now, now, answers);
		return Store(enriched, stored.Map(refreshed.Replacing).GetValueOr(refreshed), buffer);
	}

	private static DetailsSource Primary(MediaIdentity identity, SearchProvider search)
	{
		DetailsSource[] order = search switch
		{
			SearchProvider.Plex => [PlexDetails, TmdbDetails],
			SearchProvider.Tmdb or SearchProvider.Arr => [TmdbDetails, PlexDetails],
		};
		return order.FirstOrNone(source => source.IdOf(identity).IsSome()).GetValueOr(order[0]);
	}

	private Task<Result<MatchedMedia>> LookUpAsync(MediaIdentity identity, DetailsSource primary, CancellationToken cancellationToken)
	{
		var external = primary.IdOf(identity).GetValueOr(() => identity.Externals.First());
		return searchIndex[primary.Search]
			.LookUpAsync(external, cancellationToken)
			.When(found => found.Fits(identity.Type), "the details could not be fetched")
			.Map(found => found.Matched(identity));
	}

	private async Task<(MatchedMedia Enriched, ValueSet<ProviderAnswer> Answers)> EnrichAsync(
		MatchedMedia current,
		IEnumerable<ProviderAnswer> known,
		IReadOnlySet<MediaProvider> connected,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var answers = known.ToList();
		var asked = answers.Select(answer => answer.Provider).ToHashSet();
		var waited = new HashSet<MediaProvider>();
		while (current.Details.Missing is var missing && !missing.IsEmpty)
		{
			var leftToAsk = EnrichersLeftToAsk(current, asked, connected);
			if (EnricherChoice.Next(missing, leftToAsk, now) is not Some<EnricherCandidate>(var next))
			{
				if (await WaitForBlockedAsync(missing, leftToAsk, waited, now, cancellationToken) is not Some<DateTimeOffset>(var freeAt))
				{
					break;
				}

				now = freeAt;
				continue;
			}

			asked.Add(next.Provider);
			var answer = await AskAsync(next, current.Identity, cancellationToken);
			var fitting = answer.ToOptional().When(found => found.Fits(current.Details.Type));
			fitting.Apply(found => current = Merge(current, found));
			fitting.Apply(_ => answers.Add(ProviderAnswer.After(next.Provider, next.Fills, current.Details)));
		}

		return (current, [.. answers]);
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
			return Error.From(exception);
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

	private static MatchedMedia Store(MatchedMedia enriched, CachedDetails cached, MediaWriteBuffer buffer)
	{
		var result = enriched with { Details = cached.Details };
		buffer.Add(result, cached);
		return result;
	}

	private static MatchedMedia Merge(MatchedMedia current, FoundMedia found)
	{
		return current with
		{
			Identity = current.Identity with { Externals = current.Identity.Externals + found.Externals },
			Details = current.Details.FillFrom(found.Details),
		};
	}

	private List<EnricherCandidate> EnrichersLeftToAsk(MatchedMedia current, IReadOnlySet<MediaProvider> asked, IReadOnlySet<MediaProvider> connected)
	{
		return
		[
			.. Enrichers
				.Where(provider => !asked.Contains(provider) && connected.Contains(provider))
				.Select(provider => current.Identity.Externals.OfProvider(provider).Map(id => CandidateFor(provider, id)))
				.WhereSome(),
		];
	}

	private EnricherCandidate CandidateFor(MediaProvider provider, IExternalMediaId id)
	{
		return new EnricherCandidate(provider, id, enricherIndex[provider].Fills, capacity.Of(provider));
	}

	private sealed record DetailsSource(SearchProvider Search, IReadOnlyList<MediaProvider> Reads)
	{
		public Optional<IExternalMediaId> IdOf(MediaIdentity identity)
		{
			return Reads.Select(provider => identity.Externals.OfProvider(provider)).WhereSome().FirstOrNone();
		}
	}
}
