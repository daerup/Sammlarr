using System.Numerics;

namespace Domain.Media;

[Flags]
public enum MediaField
{
	None = 0,
	Title = 1 << 0,
	Tagline = 1 << 1,
	Summary = 1 << 2,
	Poster = 1 << 3,
	Backdrop = 1 << 4,
	ReleaseDate = 1 << 5,
	Genres = 1 << 6,
	Runtime = 1 << 7,
	Seasons = 1 << 8,
	Episodes = 1 << 9,
	RuntimePerEpisode = 1 << 10,
	Rating = 1 << 11,
}

public static class MediaFieldExtensions
{
	extension(MediaField fields)
	{
		public bool Overlaps(MediaField other) => (fields & other) is not MediaField.None;

		public MediaField Except(MediaField other) => fields & ~other;

		public int Count => BitOperations.PopCount((uint)fields);
	}
}
