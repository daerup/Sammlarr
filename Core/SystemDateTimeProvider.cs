using Contracts;

namespace Core;

internal class SystemDateTimeProvider : IDateTimeProvider
{
	public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
