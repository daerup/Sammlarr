namespace Contracts;

public interface IDateTimeProvider
{
	DateTimeOffset Now { get; }

	TimeZoneInfo Zone { get; }
}
