using Vogen;

namespace Domain.Feed;

[ValueObject<Guid>]
public readonly partial struct FeedId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("FeedId cannot be empty.") : Validation.Ok;

	public static bool operator <(FeedId left, FeedId right) => left.CompareTo(right) < 0;

	public static bool operator <=(FeedId left, FeedId right) => left.CompareTo(right) <= 0;

	public static bool operator >(FeedId left, FeedId right) => left.CompareTo(right) > 0;

	public static bool operator >=(FeedId left, FeedId right) => left.CompareTo(right) >= 0;
}
