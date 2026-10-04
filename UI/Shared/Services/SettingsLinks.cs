using Domain.Providers;

namespace UI.Shared.Services;

internal static class SettingsLinks
{
	internal const string Providers = "/settings?section=providers";

	internal const string Ratings = "/settings?section=ratings";

	internal static string Provider(MediaProvider provider) => $"{Providers}&provider={provider.ToString().ToLowerInvariant()}";
}
