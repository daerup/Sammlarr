using Domain.Media;
using Domain.Providers;

namespace DB.Data.Media;

internal sealed record ExternalMediaIdRow
{
	public required MediaProvider Source { get; set; }

	public required MediaType MediaType { get; set; }

	public required string Id { get; set; }

	public required MediaRow Media { get; set; }
}
