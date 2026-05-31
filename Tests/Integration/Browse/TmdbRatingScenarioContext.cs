using Contracts.Media.Metadata;

namespace Integration.Tests.Browse;

public sealed class TmdbRatingScenarioContext
{
	public IReadOnlyList<MediaRating>? Ratings { get; set; }
}
