using System.ComponentModel;

namespace Contracts.Media;

public enum MediaType
{
	[Description("Unknown")]
	Unknown,

	[Description("Movie")]
	Movie,

	[Description("Show")]
	Show,
}
