using System.ComponentModel;

namespace Domain.Processing;

public enum LimitPick
{
	[Description("From the top")]
	First,

	[Description("At random")]
	Random,
}
