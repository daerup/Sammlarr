namespace Domain.Feed;

public readonly record struct Append;

public readonly record struct Mirror
{
	public int AbsenceThreshold { get; init; } = 3;

	public Mirror() { }

	public Mirror(int absenceThreshold)
	{
		if (absenceThreshold < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(absenceThreshold), absenceThreshold, "Absence threshold must be at least 1.");
		}
		AbsenceThreshold = absenceThreshold;
	}
}

public readonly union FeedStrategy(Append, Mirror);