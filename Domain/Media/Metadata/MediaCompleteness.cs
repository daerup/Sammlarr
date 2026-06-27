using static Domain.Media.Metadata.MediaFields;

namespace Domain.Media.Metadata;

public static class MediaCompleteness
{
	extension(IPartialMedia partial)
	{
		public IReadOnlyList<string> MissingFields
		{
			get
			{
				var missing = new List<string>();
				if (!HasText(partial.Title))
					missing.Add(nameof(IPartialMedia.Title));
				if (!HasText(partial.Tagline))
					missing.Add(nameof(IPartialMedia.Tagline));
				if (!HasText(partial.Summary))
					missing.Add(nameof(IPartialMedia.Summary));
				if (!HasText(partial.PosterUrl))
					missing.Add(nameof(IPartialMedia.PosterUrl));
				if (!HasText(partial.BackdropUrl))
					missing.Add(nameof(IPartialMedia.BackdropUrl));
				if (!HasDate(partial.ReleaseDate))
					missing.Add(nameof(IPartialMedia.ReleaseDate));
				if (!HasItems(partial.Genres))
					missing.Add(nameof(IPartialMedia.Genres));

				switch (partial)
				{
					case PartialMovie movie when !HasDuration(movie.Runtime):
						missing.Add(nameof(PartialMovie.Runtime));
						break;
					case PartialShow show:
						if (!HasCount(show.Seasons))
							missing.Add(nameof(PartialShow.Seasons));
						if (!HasCount(show.Episodes))
							missing.Add(nameof(PartialShow.Episodes));
						if (!HasDuration(show.RuntimePerEpisode))
							missing.Add(nameof(PartialShow.RuntimePerEpisode));
						break;
				}

				return missing;
			}
		}

		public bool IsComplete => partial.MissingFields.Count == 0;
	}
}
