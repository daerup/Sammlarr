using Domain.Providers;

namespace Domain.Media;

public static class MediaIdentityInvariants
{
	extension(IEnumerable<IExternalMediaId> externals)
	{
		public void EnsureSinkIdPresent(MediaProvider sinkProvider)
		{
			if (externals.All(id => id.Source != sinkProvider))
			{
				throw new MissingSinkIdException(sinkProvider);
			}
		}

		public void EnsureSingleMediaType()
		{
			if (externals.Select(id => id.Type).Distinct().Take(2).Count() > 1)
			{
				throw new MixedMediaTypesException();
			}
		}
	}
}

public sealed class MissingSinkIdException(MediaProvider sinkProvider) : Exception($"No external ID for the configured sink '{sinkProvider}' is present.")
{
	public MediaProvider Sink { get; } = sinkProvider;
}

public sealed class MixedMediaTypesException() : Exception("All external IDs must be of the same media type.");

public sealed class UnknownMediaException(MediaId mediaId) : Exception($"Media '{mediaId.Value}' is not known.")
{
	public MediaId MediaId { get; } = mediaId;
}

public sealed class MediaIdentityConflictException(IReadOnlyList<MediaId> mediaIds, IReadOnlyList<IExternalMediaId> candidates)
	: Exception($"External IDs [{string.Join(", ", candidates)}] match {mediaIds.Count} media rows [{string.Join(", ", mediaIds)}]; expected at most one.")
{
	public IReadOnlyList<MediaId> MediaIds { get; } = mediaIds;
	public IReadOnlyList<IExternalMediaId> Candidates { get; } = candidates;
}
