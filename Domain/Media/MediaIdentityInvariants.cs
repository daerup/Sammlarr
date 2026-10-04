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
				throw new MissingSinkIdException(sinkProvider.ToString());
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

public sealed class MissingSinkIdException(string sink) : Exception($"No external ID for the configured sink '{sink}' is present.");

public sealed class MixedMediaTypesException() : Exception("All external IDs must be of the same media type.");

public sealed class UnknownMediaException(MediaId mediaId) : Exception($"Media '{mediaId.Value}' is not known.");

public sealed class MediaIdentityConflictException(IReadOnlyList<MediaId> mediaIds, IReadOnlyList<IExternalMediaId> candidates)
	: Exception($"External IDs [{string.Join(", ", candidates)}] match {mediaIds.Count} media rows [{string.Join(", ", mediaIds)}]; expected at most one.");
