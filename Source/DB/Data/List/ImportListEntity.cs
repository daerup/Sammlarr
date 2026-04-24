using System.ComponentModel.DataAnnotations;
using Contracts.Media.List;
using Contracts.Processing;
using DB.Conversion.Enums;

namespace DB.Data.List;

internal record ImportListEntity : Entity<Guid>
{
	[Required]
	public bool IsEnabled { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	[EnumAsString]
	public MediaListProvider Provider { get; set; }

	[Required]
	[EnumAsString]
	public MediaListAddStrategy Strategy { get; set; }

	[Required]
	public string Identifier { get; set; } = string.Empty;

	public QueryFilter? Filter { get; set; }
	public QuerySort? Sort { get; set; }
	public QueryLimit? Limit { get; set; }
}
