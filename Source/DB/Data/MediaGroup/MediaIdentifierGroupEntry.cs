using System.ComponentModel.DataAnnotations;

namespace DB.Data.MediaGroup;

internal record MediaIdentifierGroupEntry
{
	[Required]
	public required MediaIdentifier Identifier { get; set; }

	[Required]
#pragma warning disable CS8618// Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	public MediaIdentifierGroupEntity MediaIdGroupEntity { get; set; }
#pragma warning restore CS8618// Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
