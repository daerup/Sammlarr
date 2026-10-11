using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;

namespace Core.Media;

internal sealed class TitleIdentifier(MediaIdResolver idResolver, IIdentityClaims claims)
{
	public async Task<Identification> IdentifyAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyList<StoredMedia> rows,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		if (rows.Count > 1)
		{
			return await ClaimAsync(new IdentityClaim([.. request.Candidates.OfType<IIdentityKey>()], new None(), request.Sources), new None(), cancellationToken);
		}

		var row = rows.FirstOrNone();
		return await row.When(stored => stored.Identity.Keys.OfProvider(target).IsSome())
			.Apply(_ => FeedRunSpans.Known())
			.Match(
				stored => ExtendAsync(request, stored, cancellationToken),
				() => ResolveAsync(row.Map(stored => Seeded(request, stored)).GetValueOr(request), target, row, connected, cancellationToken)
			);
	}

	private Task<Identification> ExtendAsync(IdentifyRequest request, StoredMedia stored, CancellationToken cancellationToken)
	{
		List<IIdentityKey> missing = [.. request.Candidates.OfType<IIdentityKey>().Where(key => !stored.Identity.Keys.Contains(key))];
		if (missing.Count == 0)
		{
			return Task.FromResult<Identification>(new IdentifyHandoff(stored.Identity, Some.Of(stored)));
		}

		return ClaimAsync(new IdentityClaim([.. missing], Some.Of(stored.Identity.Id), request.Sources), Some.Of(stored), cancellationToken);
	}

	private async Task<Identification> ResolveAsync(
		IdentifyRequest request,
		MediaProvider target,
		Optional<StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		var resolution = await idResolver.ResolveAsync(request, target, connected, cancellationToken);
		return resolution switch
		{
			Resolved resolved => await ClaimAsync(
				new IdentityClaim([.. resolved.Ids.OfType<IIdentityKey>()], stored.Map(row => row.Identity.Id), request.Sources + resolved.Sources),
				new None(),
				cancellationToken
			),
			NoMatchAnywhere => new Unidentified(UnmatchedReason.NotFound, []),
			NotFoundEarlier => new Unidentified(UnmatchedReason.NotFoundEarlier, []),
			NotRun => new Unidentified(UnmatchedReason.CouldNotLookUp, []),
		};
	}

	private async Task<Identification> ClaimAsync(IdentityClaim claim, Optional<StoredMedia> stored, CancellationToken cancellationToken)
	{
		var outcome = await claims.ClaimAsync(claim, cancellationToken);
		return outcome switch
		{
			Claimed claimed => new IdentifyHandoff(new MediaIdentity(claimed.Id, stored.Map(row => row.Identity.Keys).GetValueOr([]) + claimed.Keys), stored),
			Conflicted conflicted => Conflict(conflicted.Owners),
		};
	}

	private static IdentifyRequest Seeded(IdentifyRequest request, StoredMedia stored)
	{
		return request with { Candidates = [.. request.Candidates.Union<IExternalMediaId>(stored.Identity.Keys)], Hints = request.Hints.Or(() => stored.Hints()) };
	}

	private static Unidentified Conflict(ValueSet<MediaId> owners) => new(UnmatchedReason.Conflict, owners);
}
