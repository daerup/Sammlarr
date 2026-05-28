using Contracts.Media.Metadata;

namespace Contracts.Media.Search;

public interface IMediaDetail
{
	public MediaIdentity Identity { get; }
	public MediaType MediaType { get; }

	public string PosterUrl { get; }
	public string BackdropUrl { get; }

	public string Title { get; }
	public string Tagline { get; }
	public string Summary { get; }

	public DateOnly ReleaseDate { get; }

	public IReadOnlyList<string> Genres { get; }

	public IReadOnlyList<MediaRating> Ratings { get; }

	public float? Rating => Ratings.Count > 0 ? Ratings[0].NormalizedValue : null;

	public int? RuntimeMinutes => null;
}
