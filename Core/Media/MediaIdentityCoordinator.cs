using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaIdentityCoordinator(IMediaRepository mediaRepository, ILogger<MediaIdentityCoordinator> logger)
{
	public Task<IReadOnlyList<MediaIdentity>> CoordinateAsync(
		IReadOnlyList<MediaIdentity> preliminary,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	) => CoordinateAsync(preliminary, new IdentityMerge(), buffer, cancellationToken);

	public async Task<IReadOnlyList<MediaIdentity>> CoordinateAsync(
		IReadOnlyList<MediaIdentity> preliminary,
		IdentityMerge merge,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var groups = preliminary.Select(p => p.Externals.ToList()).ToList();
		var found = await mediaRepository.FindIdentitiesAsync(groups, cancellationToken);

		var persisted = new Dictionary<IExternalMediaId, MediaIdentity>();
		foreach (var match in found.WhereSome())
		{
			foreach (var external in match.Externals)
			{
				persisted[external] = match;
			}
		}

		foreach (var identity in preliminary)
		{
			merge.Merge(identity, persisted, buffer);
		}

		return [.. preliminary.Select(merge.Canonical)];
	}

	public async Task<IReadOnlyList<Result<IdentifyHandoff>>> CoordinateHandoffsAsync(
		List<Result<IdentifyHandoff>> handoffs,
		IdentityMerge merge,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var pendingIndices = new List<int>();
		var pendingPreliminary = new List<MediaIdentity>();
		foreach (var (idx, handoff) in handoffs.Index())
		{
			Pending(handoff).Apply(_ => pendingIndices.Add(idx)).Apply(match => pendingPreliminary.Add(match.Identity));
		}
		if (pendingPreliminary.Count == 0)
		{
			return handoffs;
		}

		try
		{
			var corrected = await CoordinateAsync(pendingPreliminary, merge, buffer, cancellationToken);
			for (var i = 0; i < corrected.Count; i++)
			{
				handoffs[pendingIndices[i]] = Success.Of(new IdentifyHandoff(corrected[i], new None()));
			}
		}
		catch (MediaIdentityConflictException)
		{
			for (var i = 0; i < pendingPreliminary.Count; i++)
			{
				handoffs[pendingIndices[i]] = await CoordinateOneAsync(pendingPreliminary[i], merge, buffer, cancellationToken);
			}
		}

		return handoffs;
	}

	public Task<Result<IdentifyHandoff>> CoordinateHandoffAsync(
		Result<IdentifyHandoff> handoff,
		IdentityMerge merge,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		return Pending(handoff).Match(match => CoordinateOneAsync(match.Identity, merge, buffer, cancellationToken), () => Task.FromResult(handoff));
	}

	private static Optional<IdentifyHandoff> Pending(Result<IdentifyHandoff> handoff) => handoff.ToOptional().When(match => match.Stored.IsNone());

	private async Task<Result<IdentifyHandoff>> CoordinateOneAsync(
		MediaIdentity preliminary,
		IdentityMerge merge,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var corrected = await CoordinateAsync([preliminary], merge, buffer, cancellationToken);
			return Success.Of(new IdentifyHandoff(corrected[0], new None()));
		}
		catch (MediaIdentityConflictException conflict)
		{
			logger.LogWarning(conflict, "A title's ids belong to more than one stored title, so it stays unmatched");
			return Error.From(conflict);
		}
	}
}
