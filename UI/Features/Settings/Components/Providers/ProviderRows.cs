using Domain.Providers;

namespace UI.Features.Settings.Components.Providers;

internal static class ProviderRows
{
	internal const string Ringed = "RingedProvider";

	internal static string Id(MediaProvider provider) => $"provider-row-{provider.ToString().ToLowerInvariant()}";
}
