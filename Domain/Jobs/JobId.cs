using Vogen;

namespace Domain.Jobs;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct JobId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("JobId cannot be empty.") : Validation.Ok;
}
