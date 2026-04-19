using System.ComponentModel.DataAnnotations;
using DB.Data.MediaGroup;

namespace DB.Data.Blacklist;

internal record BlacklistEntryEntity : Entity<Guid>
{
	[Required]
	public MediaIdentifierGroupEntity Media { get; set; } = null!;
}
