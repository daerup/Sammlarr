using System.ComponentModel.DataAnnotations;
using Domain.Media;
using Domain.Providers;

namespace DB.Data.Media;

internal record ProviderAnswerRow
{
	[Required]
	public required MediaProvider Provider { get; set; }

	[Required]
	public required MediaFields Unfilled { get; set; }

#pragma warning disable CS8618
	public MediaDetailsRow Details { get; set; }
#pragma warning restore CS8618
}
