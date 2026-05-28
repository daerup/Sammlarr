using System.ComponentModel;

namespace Contracts.Media;

public enum MediaType
{
	[Description("Movie")]
	Movie,

	[Description("Show")]
	Show,

	[Description("Unknown")]
	Unknown,
}
