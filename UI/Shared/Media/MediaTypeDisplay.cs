using Domain.Media;

namespace UI.Shared.Media;

internal static class MediaTypeDisplay
{
	extension(MediaType type)
	{
		public string Icon
		{
			get
			{
				return type switch
				{
					MediaType.Movie => "film",
					MediaType.Show => "tv",
					MediaType.Unknown => "circle-help",
				};
			}
		}

		public string Label => type.GetDescription();

		public string Plural
		{
			get
			{
				return type switch
				{
					MediaType.Movie => "Movies",
					MediaType.Show => "Shows",
					MediaType.Unknown => "Unknown",
				};
			}
		}
	}
}
