using Vogen;

namespace Domain.Jobs;

[ValueObject<Guid>]
public readonly partial struct JobId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("JobId cannot be empty.") : Validation.Ok;

	public static bool operator <(JobId left, JobId right) => left.CompareTo(right) < 0;

	public static bool operator <=(JobId left, JobId right) => left.CompareTo(right) <= 0;

	public static bool operator >(JobId left, JobId right) => left.CompareTo(right) > 0;

	public static bool operator >=(JobId left, JobId right) => left.CompareTo(right) >= 0;
}
