using System.ComponentModel;

namespace Domain.Media;

public enum MediaType
{
	[Description("Movie")]
	Movie,

	[Description("Show")]
	Show,

	[Description("Unknown")]
	Unknown,
}
