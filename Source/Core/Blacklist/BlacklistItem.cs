using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Search;

namespace Core.Blacklist;

internal record BlacklistItem : IBlacklistItem
{
	public required IMediaId SourceId { get; init; }
	public required string Title { get; init; }
	public required MediaType MediaType { get; init; }
	public required string Description { get; init; }
	public required string ArtUrl { get; init; }
	public required string PosterUrl { get; init; }
	public required DateTimeOffset ReleaseDate { get; init; }
	public required DateTimeOffset BlacklistDate { get; init; }

	public static BlacklistItem From(IMediaDetailResult detail, DateTimeOffset addedDate) =>
		new()
		{
			SourceId = detail.SourceId,
			Title = detail.Title,
			Description = detail.Summary,
			MediaType = detail.MediaType,
			ArtUrl = detail.BackdropUrl ?? string.Empty,
			PosterUrl = detail.PosterUrl ?? string.Empty,
			ReleaseDate = detail.ReleaseDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : DateTimeOffset.MinValue,
			BlacklistDate = addedDate,
		};
}
