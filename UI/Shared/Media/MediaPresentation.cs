using Domain.Media;

namespace UI.Shared.Media;

public static class MediaPresentation
{
	extension(MediaLookup media)
	{
		public string Key
		{
			get
			{
				return media switch
				{
					IdentifiedMedia identified => identified.Key,
					UnmatchedMedia unmatched => string.Join("|", unmatched.Externals.Select(id => $"{id.Source}:{id.Value}").Order(StringComparer.Ordinal)),
				};
			}
		}
	}

	extension(IdentifiedMedia media)
	{
		public string Key => media.Identity.Id.Value.ToString();
	}

	extension(MediaDetails details)
	{
		public int? Year => details.ReleaseDate?.Year;
	}
}
