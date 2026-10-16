namespace UI.Features.Settings.Components.Users;

internal static class LinkText
{
	internal static string Lifetime(TimeSpan lifetime)
	{
		var days = (int)Math.Round(lifetime.TotalDays);
		return days == 1 ? "1 day" : $"{days} days";
	}
}
