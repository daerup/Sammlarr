namespace Contracts;

public interface IDateTimeProvider
{
	DateTimeOffset Now { get; }

	DateOnly Today { get; }

	TimeZoneInfo Zone { get; }
}
