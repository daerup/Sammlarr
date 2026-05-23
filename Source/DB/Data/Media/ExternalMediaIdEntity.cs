using Contracts.Media;

namespace DB.Data.Media;

internal record ExternalMediaIdEntity
{
	public required MediaProvider Source { get; set; }

	public required MediaType MediaType { get; set; }

	public required string Id { get; set; }

	public required MediaEntity Media { get; set; }
}
