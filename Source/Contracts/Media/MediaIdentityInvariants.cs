namespace Contracts.Media;

public static class MediaIdentityInvariants
{
	extension(IEnumerable<IExternalMediaId> externals)
	{
		public void EnsureWatchlistProviderPresent(MediaProvider watchlistProvider)
		{
			if (externals.All(id => id.Source != watchlistProvider))
			{
				throw new MissingWatchlistExternalIdException(watchlistProvider);
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

public sealed class MissingWatchlistExternalIdException(MediaProvider watchlistProvider)
	: Exception($"No external ID for the configured watchlist provider '{watchlistProvider}' is present.")
{
	public MediaProvider WatchlistProvider { get; } = watchlistProvider;
}

public sealed class MixedMediaTypesException() : Exception("All external IDs must be of the same media type.");

public sealed class MediaIdentityConflictException(IReadOnlyList<MediaId> mediaIds, IReadOnlyList<IExternalMediaId> candidates)
	: Exception($"External IDs [{string.Join(", ", candidates)}] match {mediaIds.Count} media rows [{string.Join(", ", mediaIds)}]; expected at most one.")
{
	public IReadOnlyList<MediaId> MediaIds { get; } = mediaIds;
	public IReadOnlyList<IExternalMediaId> Candidates { get; } = candidates;
}
