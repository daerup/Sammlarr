using Contracts.Media;

namespace DB.Data.Media;

internal record ExternalMediaIdRow
{
	public required MediaProvider Source { get; set; }

	public required MediaType MediaType { get; set; }

	public required string Id { get; set; }

	public required MediaRow Media { get; set; }
}
