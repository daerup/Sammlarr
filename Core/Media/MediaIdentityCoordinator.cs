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
	) => CoordinateAsync(preliminary, [], buffer, cancellationToken);

	public async Task<IReadOnlyList<MediaIdentity>> CoordinateAsync(
		IReadOnlyList<MediaIdentity> preliminary,
		Dictionary<IExternalMediaId, MediaIdentity> building,
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
			MergeIntoCanonical(identity, persisted, building, buffer);
		}

		return preliminary.Select(p => p.Externals.Count > 0 ? building[p.Externals.First()] : p).ToArray();
	}

	public async Task<IReadOnlyList<Result<IdentifyHandoff>>> CoordinateHandoffsAsync(
		List<Result<IdentifyHandoff>> handoffs,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var pendingIndices = new List<int>();
		var pendingPreliminary = new List<MediaIdentity>();
		foreach (var (idx, handoff) in handoffs.Index())
		{
			handoff.ToOptional().When(match => match.Stored.IsNone()).Apply(_ => pendingIndices.Add(idx)).Apply(match => pendingPreliminary.Add(match.Identity));
		}
		if (pendingPreliminary.Count == 0)
		{
			return handoffs;
		}

		try
		{
			var corrected = await CoordinateAsync(pendingPreliminary, building, buffer, cancellationToken);
			for (var i = 0; i < corrected.Count; i++)
			{
				handoffs[pendingIndices[i]] = Success.Of(new IdentifyHandoff(corrected[i], new None()));
			}
		}
		catch (MediaIdentityConflictException)
		{
			for (var i = 0; i < pendingPreliminary.Count; i++)
			{
				handoffs[pendingIndices[i]] = await CoordinateOneAsync(pendingPreliminary[i], building, buffer, cancellationToken);
			}
		}

		return handoffs;
	}

	private async Task<Result<IdentifyHandoff>> CoordinateOneAsync(
		MediaIdentity preliminary,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var corrected = await CoordinateAsync([preliminary], building, buffer, cancellationToken);
			return Success.Of(new IdentifyHandoff(corrected[0], new None()));
		}
		catch (MediaIdentityConflictException conflict)
		{
			logger.LogWarning(conflict, "A title's ids belong to more than one stored title, so it stays unmatched");
			return Error.From(conflict);
		}
	}

	private static void MergeIntoCanonical(
		MediaIdentity preliminary,
		IReadOnlyDictionary<IExternalMediaId, MediaIdentity> persisted,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer
	)
	{
		var touching = Matches(preliminary, persisted).Concat(Matches(preliminary, building)).DistinctBy(match => match.Id).ToList();
		var anchor = touching.Append(preliminary).First();
		var canonical = anchor with { Externals = touching.Aggregate(preliminary.Externals, (all, match) => all + match.Externals) };

		foreach (var external in canonical.Externals)
		{
			building[external] = canonical;
		}

		foreach (var absorbed in touching.Where(match => match.Id != canonical.Id))
		{
			buffer.MergeIdentity(absorbed.Id, canonical);
		}

		buffer.AddIdentity(canonical);
	}

	private static IEnumerable<MediaIdentity> Matches(MediaIdentity preliminary, IReadOnlyDictionary<IExternalMediaId, MediaIdentity> index) =>
		preliminary.Externals.Where(index.ContainsKey).Select(external => index[external]);
}
