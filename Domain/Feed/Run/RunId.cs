using Vogen;

namespace Domain.Feed.Run;

[ValueObject<Guid>]
public readonly partial struct RunId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("RunId cannot be empty.") : Validation.Ok;

	public static bool operator <(RunId left, RunId right) => left.CompareTo(right) < 0;

	public static bool operator <=(RunId left, RunId right) => left.CompareTo(right) <= 0;

	public static bool operator >(RunId left, RunId right) => left.CompareTo(right) > 0;

	public static bool operator >=(RunId left, RunId right) => left.CompareTo(right) >= 0;
}
