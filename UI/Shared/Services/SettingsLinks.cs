using Domain.Providers;

namespace UI.Shared.Services;

internal static class SettingsLinks
{
	internal const string Providers = "/settings?section=providers";

	internal const string Ratings = "/settings?section=ratings";

	internal const string Users = "/settings?section=users";

	internal const string Account = "/settings?section=account";

	internal static string Provider(MediaProvider provider) => $"{Providers}&provider={provider.ToString().ToLowerInvariant()}";
}
