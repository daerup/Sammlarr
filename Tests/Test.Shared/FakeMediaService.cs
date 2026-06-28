using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;

namespace Test.Shared;

public sealed class FakeMediaService : IMediaService
{
	private readonly Dictionary<IExternalMediaId, MediaId> _resolutions = new();
	private readonly Dictionary<MediaId, IdentifiedMedia> _metadata = new();
	private readonly Dictionary<MediaId, TimeSpan> _delays = new();

	public List<IReadOnlyList<MediaId>> InvalidatedBatches { get; } = new();

	public FakeMediaService RegisterResolution(IExternalMediaId external, MediaId mediaId)
	{
		_resolutions[external] = mediaId;
		return this;
	}

	public FakeMediaService RegisterMetadata(MediaId id, IdentifiedMedia detail)
	{
		_metadata[id] = detail;
		return this;
	}

	public FakeMediaService WithDelay(MediaId id, TimeSpan delay)
	{
		_delays[id] = delay;
		return this;
	}

	public async IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(
		IEnumerable<ResolveRequest> requests,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var req in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return Identify(req);
			await Task.CompletedTask;
		}
	}

	public async IAsyncEnumerable<Result<IdentifiedMedia>> GetDetailsAsync(
		IEnumerable<MediaId> mediaIds,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var id in mediaIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_delays.TryGetValue(id, out var delay))
			{
				await Task.Delay(delay, cancellationToken);
			}
			yield return GetDetail(id);
		}
	}

	public async IAsyncEnumerable<MediaLookup> ResolveDetailsAsync(
		IEnumerable<ResolveRequest> requests,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		foreach (var req in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (Identify(req) is not MediaIdentity identity)
			{
				yield return ToUnmatched(req);
				continue;
			}
			if (_delays.TryGetValue(identity.Id, out var delay))
			{
				await Task.Delay(delay, cancellationToken);
			}
			yield return GetDetail(identity.Id) is IdentifiedMedia detail ? detail : ToUnmatched(req);
		}
	}

	private static MediaLookup ToUnmatched(ResolveRequest req) =>
		new UnmatchedMedia { Externals = [.. req.Candidates], Title = req.Hints is Some<ResolutionHints>(var hints) ? hints.Title : "" };

	public Task<IReadOnlyList<MediaIdentity>> ResolveIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	)
	{
		var identities = candidateGroups
			.Select(group =>
			{
				var existing = group.Select(c => _resolutions.GetValueOrDefault(c)).FirstOrDefault(id => id != default);
				return new MediaIdentity(existing == default ? MediaId.From(Guid.NewGuid()) : existing, [.. group]);
			})
			.ToList();
		return Task.FromResult<IReadOnlyList<MediaIdentity>>(identities);
	}

	public Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		InvalidatedBatches.Add(mediaIds.ToList());
		return Task.CompletedTask;
	}

	private Result<MediaIdentity> Identify(ResolveRequest req) =>
		TryResolve(req) is { } mediaId ? new MediaIdentity(mediaId, [.. req.Candidates]) : Error.From("unresolved");

	private Result<IdentifiedMedia> GetDetail(MediaId id) => _metadata.TryGetValue(id, out var found) ? found : Error.From("not found");

	private MediaId? TryResolve(ResolveRequest req)
	{
		foreach (var candidate in req.Candidates)
		{
			if (_resolutions.TryGetValue(candidate, out var id))
			{
				return id;
			}
		}
		return null;
	}
}
