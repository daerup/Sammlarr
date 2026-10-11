using Contracts.Media;
using Contracts.Media.Conversions;

namespace Core.Media;

internal static class StoredMediaHints
{
	extension(StoredMedia media)
	{
		internal Optional<IdentifyHints> Hints()
		{
			return media.Cached.Map(cached =>
				Optional
					.Of(cached.Details.Title)
					.Map(title =>
						Optional
							.Of(cached.Details.ReleaseDate)
							.Map(released => new IdentifyHints
							{
								Title = title,
								Year = released.Year,
								MediaType = cached.Details.Type,
							})
					)
			);
		}
	}
}
