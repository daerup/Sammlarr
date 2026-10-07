namespace API.Feeds;

internal readonly record struct AnimeSeason(string Name, int Year)
{
	private static readonly string[] Names = ["WINTER", "SPRING", "SUMMER", "FALL"];

	internal static AnimeSeason Of(DateOnly date) => new(Names[(date.Month - 1) / 3], date.Year);

	internal AnimeSeason Next()
	{
		var index = Array.IndexOf(Names, Name);
		return index == Names.Length - 1 ? new AnimeSeason(Names[0], Year + 1) : new AnimeSeason(Names[index + 1], Year);
	}
}
