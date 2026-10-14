using System.Diagnostics.CodeAnalysis;
using Contracts;

namespace Core;

[SuppressMessage(
	"ApiDesign",
	"RS0030",
	Justification = "The one place allowed to read the system clock and time zone; everything else asks IDateTimeProvider."
)]
internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
	public DateTimeOffset Now => DateTimeOffset.UtcNow;

	public TimeZoneInfo Zone => TimeZoneInfo.Local;
}
