using System.ComponentModel.DataAnnotations;
using Domain.Providers;

namespace DB.Data.Media;

internal record MediaPageLinkRow
{
	[Required]
	public required MediaProvider Site { get; set; }

	[Required]
	[MaxLength(300)]
	public required string Path { get; set; }

#pragma warning disable CS8618
	public MediaDetailsRow Details { get; set; }
#pragma warning restore CS8618
}
