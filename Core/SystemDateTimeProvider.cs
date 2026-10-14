using System.Diagnostics.CodeAnalysis;
using Contracts;

namespace Core;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
	[SuppressMessage("ApiDesign", "RS0030", Justification = "The one place allowed to read the system clock; everything else asks IDateTimeProvider.")]
	public DateTimeOffset Now => DateTimeOffset.UtcNow;

	[SuppressMessage("ApiDesign", "RS0030", Justification = "The one place allowed to read the server's time zone; everything else asks IDateTimeProvider.")]
	public TimeZoneInfo Zone => TimeZoneInfo.Local;
}
