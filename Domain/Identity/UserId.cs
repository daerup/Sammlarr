using Vogen;

namespace Domain.Identity;

[ValueObject<Guid>]
public readonly partial struct UserId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("UserId cannot be empty.") : Validation.Ok;

	public static UserId New() => From(Guid.NewGuid());

	public static bool operator <(UserId left, UserId right) => left.CompareTo(right) < 0;

	public static bool operator <=(UserId left, UserId right) => left.CompareTo(right) <= 0;

	public static bool operator >(UserId left, UserId right) => left.CompareTo(right) > 0;

	public static bool operator >=(UserId left, UserId right) => left.CompareTo(right) >= 0;
}
