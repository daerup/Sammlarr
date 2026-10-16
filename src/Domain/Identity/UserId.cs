using Vogen;

namespace Domain.Identity;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct UserId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("UserId cannot be empty.") : Validation.Ok;

	public static UserId New() => From(Guid.NewGuid());
}
