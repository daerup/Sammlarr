using Vogen;

namespace Domain.Feed;

[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct FeedId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("FeedId cannot be empty.") : Validation.Ok;
}
