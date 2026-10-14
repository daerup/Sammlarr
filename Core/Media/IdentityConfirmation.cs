using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal interface IIdentityConfirmation
{
	Task<IReadOnlyList<Confirmation>> AskAsync(IReadOnlyList<ConfirmationRequest> requests, CancellationToken cancellationToken = default);
}

internal sealed record ConfirmationRequest(IdentityConflict Note, ValueSet<IIdentityKey> Held);

internal sealed record Confirmation(IdentityConflict Note, RemoteService By, Answer<IIdentityKey> Answer);

internal sealed class IdentityConfirmation(IServiceScopeFactory scopes, IRemoteCapacity capacity, IUserRepository users, ILogger<IdentityConfirmation> logger)
	: IIdentityConfirmation
{
	public async Task<IReadOnlyList<Confirmation>> AskAsync(IReadOnlyList<ConfirmationRequest> requests, CancellationToken cancellationToken = default)
	{
		Dictionary<ConfirmationRequest, Confirmation> answered = [];
		foreach (var user in await users.GetAllAsync(cancellationToken))
		{
			if (!Unsettled(requests, answered).Any())
			{
				break;
			}

			await using var serving = scopes.CreateAsyncScope();
			var services = serving.ServiceProvider;
			using var owner = services.GetRequiredService<IUserScope>().As(user);
			using var priority = LanePriority.Next(LaneClass.Background).Enter();
			IReadOnlyList<IdToId> hops =
			[
				.. IdLookupPlanner.Derive(services.GetRequiredService<IEnumerable<Keyed<RemoteService, IExternalIdConverter>>>()).Select(IdHop).WhereSome(),
			];
			var primers = services.GetRequiredService<IEnumerable<IBurstPrimer>>();
			var connected = await services.GetRequiredService<IServiceReadiness>().ConnectedAsync(hops.Select(hop => hop.Remote).Distinct(), cancellationToken);
			Dictionary<ConfirmationRequest, HashSet<RemoteService>> tried = [];
			List<Route> routes = [.. Unsettled(requests, answered).Select(request => Routed(request, hops, connected, tried)).WhereSome()];
			while (routes.Count > 0)
			{
				await Task.WhenAll(primers.Select(primer => PrimeAsync(primer, routes, cancellationToken))).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
				var asked = await Task.WhenAll(routes.Select(async route => (route.Request, Confirmation: await AnswerAsync(route, cancellationToken))));
				foreach (var (request, confirmation) in asked)
				{
					answered[request] = confirmation;
					tried[request] = [.. tried.GetValueOrNone(request).GetValueOr([]), confirmation.By];
				}

				routes = [.. asked.Where(each => Unsent(each.Confirmation)).Select(each => Routed(each.Request, hops, connected, tried)).WhereSome()];
			}
		}

		return [.. answered.Values];
	}

	private static IEnumerable<ConfirmationRequest> Unsettled(IEnumerable<ConfirmationRequest> requests, Dictionary<ConfirmationRequest, Confirmation> answered)
	{
		return requests.Where(request => answered.GetValueOrNone(request).Match(Unsent, () => true));
	}

	private static bool Unsent(Confirmation confirmation) => confirmation.Answer is Unavailable { Sent: false };

	private Optional<Route> Routed(
		ConfirmationRequest request,
		IReadOnlyList<IdToId> hops,
		IReadOnlySet<RemoteService> connected,
		Dictionary<ConfirmationRequest, HashSet<RemoteService>> tried
	)
	{
		var claimed = request.Note.Claimed;
		var skipped = tried.GetValueOrNone(request).GetValueOr([]);
		return hops.Where(hop =>
				hop.To == claimed.Source && connected.Contains(hop.Remote) && !request.Note.Sources.Contains(hop.Remote) && !skipped.Contains(hop.Remote)
			)
			.OrderBy(hop => capacity.Of(hop.Remote).Weight(hop.Lookup.Cost))
			.SelectMany(hop => request.Held.Where(key => key.Source == hop.From).Select(key => new Route(request, hop, key)))
			.FirstOrNone();
	}

	private static Task PrimeAsync(IBurstPrimer primer, IReadOnlyList<Route> routes, CancellationToken cancellationToken)
	{
		List<IReadOnlyList<IExternalMediaId>> wanted =
		[
			.. routes.Where(route => route.Hop.Remote == primer.Remote).Select(route => (IReadOnlyList<IExternalMediaId>)[route.From]),
		];
		return wanted.Count > 0 ? primer.PrimeAsync(wanted, cancellationToken) : Task.CompletedTask;
	}

	private async Task<Confirmation> AnswerAsync(Route route, CancellationToken cancellationToken)
	{
		var answer = await route.Hop.Converter.LookUpAsync(new ByIdRequest(route.Hop.Lookup, route.From), cancellationToken);
		if (answer is Faulted faulted)
		{
			logger.LogWarning(
				faulted.Error.Exception.ToNullable(),
				"{Remote} could not answer whether two stored titles are one: {Reason}",
				faulted.Remote,
				faulted.Error.Reason
			);
		}

		return new Confirmation(route.Request.Note, route.Hop.Remote, Claimed(answer, route.Request.Note.Claimed, route.Hop.Remote));
	}

	private static Answer<IIdentityKey> Claimed(Answer<IdLookupResult> answer, IIdentityKey claimed, RemoteService remote)
	{
		return answer switch
		{
			Found<IdLookupResult> found => found
				.Value.Ids.OfType<IIdentityKey>()
				.FirstOrNone(id => id.Source == claimed.Source)
				.Map(id => (Answer<IIdentityKey>)new Found<IIdentityKey>(id))
				.GetValueOr(new NoMatch(remote)),
			NoMatch noMatch => noMatch,
			Unavailable unavailable => unavailable,
			Faulted faulted => faulted,
		};
	}

	private static Optional<IdToId> IdHop(Hop hop)
	{
		return hop switch
		{
			IdToId id => Some.Of(id),
			IdToHints or HintsToId => new None(),
		};
	}

	private sealed record Route(ConfirmationRequest Request, IdToId Hop, IIdentityKey From);
}
