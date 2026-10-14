namespace UI.Features.Settings.Components.Users;

internal static class LinkText
{
	internal static string Lifetime(DateTimeOffset expiresAt, DateTimeOffset now)
	{
		var days = (int)Math.Round((expiresAt - now).TotalDays, MidpointRounding.ToEven);
		return days == 1 ? "1 day" : FormattableString.CurrentCulture($"{days} days");
	}
}
