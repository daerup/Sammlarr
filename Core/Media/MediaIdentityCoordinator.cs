using Contracts.Media;
using Domain.Media;

namespace Core.Media;

internal sealed class MediaIdentityCoordinator(IMediaRepository mediaRepository)
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
		foreach (var match in found.Where(f => f.HasValue).Select(f => f!.Value))
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

	public async Task<IReadOnlyList<Result<IdentityMatch>>> CoordinateIdentityMatchesAsync(
		List<Result<IdentityMatch>> identityMatches,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var pendingIndices = new List<int>();
		var pendingPreliminary = new List<MediaIdentity>();
		foreach (var (idx, identityMatch) in identityMatches.Index())
		{
			if (identityMatch is Success<IdentityMatch>(var r) && r.Stored.IsNone())
			{
				pendingIndices.Add(idx);
				pendingPreliminary.Add(r.Identity);
			}
		}
		if (pendingPreliminary.Count == 0)
		{
			return identityMatches;
		}
		var corrected = await CoordinateAsync(pendingPreliminary, building, buffer, cancellationToken);
		for (var i = 0; i < corrected.Count; i++)
		{
			identityMatches[pendingIndices[i]] = Success.Of(new IdentityMatch(corrected[i], new None()));
		}
		return identityMatches;
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
