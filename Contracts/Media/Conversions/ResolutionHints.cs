using Domain.Media;

namespace Contracts.Media.Conversions;

public record ResolutionHints
{
	public required string Title { get; init; }

	public required int Year { get; init; }

	public required MediaType MediaType { get; init; }
}
