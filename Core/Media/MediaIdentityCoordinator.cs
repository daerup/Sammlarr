using Contracts;
using Contracts.Media;

namespace Core.Media;

internal sealed class MediaIdentityCoordinator(IMediaRepository mediaRepository)
{
	public async Task<IReadOnlyList<MediaIdentity>> CoordinateAsync(
		IReadOnlyList<MediaIdentity> preliminary,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var groups = preliminary.Select(p => p.Externals.ToList()).ToList();
		var found = await mediaRepository.FindIdentitiesAsync(groups, cancellationToken);

		var cached = new Dictionary<IExternalMediaId, MediaIdentity>();
		foreach (var match in found.Where(f => f.HasValue).Select(f => f!.Value))
		{
			foreach (var external in match.Externals)
			{
				cached[external] = match;
			}
		}

		var building = new Dictionary<IExternalMediaId, MediaIdentity>();
		var order = Enumerable.Range(0, preliminary.Count).OrderByDescending(i => found[i] is not null);
		foreach (var i in order)
		{
			MergeIntoCanonical(preliminary[i], cached, building);
		}

		var corrected = preliminary.Select(p => p.Externals.Count > 0 ? building[p.Externals.First()] : p).ToArray();

		foreach (var unique in building.Values.DistinctBy(c => c.Id))
		{
			buffer.AddIdentity(unique);
		}

		return corrected;
	}

	public async Task<IReadOnlyList<Optional<ResolvedIdentity>>> CoordinateResolutionsAsync(
		List<Optional<ResolvedIdentity>> resolutions,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var pendingIndices = new List<int>();
		var pendingPreliminary = new List<MediaIdentity>();
		foreach (var (idx, resolution) in resolutions.Index())
		{
			if (resolution is Some<ResolvedIdentity>(var r) && r.Cached is null)
			{
				pendingIndices.Add(idx);
				pendingPreliminary.Add(r.Identity);
			}
		}
		if (pendingPreliminary.Count == 0)
		{
			return resolutions;
		}
		var corrected = await CoordinateAsync(pendingPreliminary, buffer, cancellationToken);
		for (var i = 0; i < corrected.Count; i++)
		{
			resolutions[pendingIndices[i]] = Some.Of(new ResolvedIdentity(corrected[i], null));
		}
		return resolutions;
	}

	private static void MergeIntoCanonical(
		MediaIdentity preliminary,
		IReadOnlyDictionary<IExternalMediaId, MediaIdentity> cached,
		Dictionary<IExternalMediaId, MediaIdentity> building
	)
	{
		var fromBuilding = preliminary.Externals.Select(e => building.TryGetValue(e, out var m) ? (MediaIdentity?)m : null).FirstOrDefault(m => m is not null);
		var fromCache = preliminary.Externals.Select(e => cached.TryGetValue(e, out var m) ? (MediaIdentity?)m : null).FirstOrDefault(m => m is not null);

		var anchor = fromBuilding ?? fromCache ?? preliminary;
		var canonical = anchor with { Externals = anchor.Externals + preliminary.Externals };

		foreach (var ext in canonical.Externals)
		{
			building[ext] = canonical;
		}
	}
}
