using Vogen;

namespace Domain.Feed.Run;

[ValueObject<Guid>]
public readonly partial struct RunId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("RunId cannot be empty.") : Validation.Ok;
}
