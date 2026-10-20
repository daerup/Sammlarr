using Humanizer;

namespace UI.Features.Settings.Components.Users;

internal static class LinkText
{
	internal static string Lifetime(TimeSpan lifetime)
	{
		return "day".ToQuantity((int)Math.Round(lifetime.TotalDays));
	}
}
