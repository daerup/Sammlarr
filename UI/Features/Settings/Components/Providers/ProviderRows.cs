using Domain.Providers;
using UI.Shared.Services;

namespace UI.Features.Settings.Components.Providers;

internal static class ProviderRows
{
	internal static string Id(Service service) => $"provider-row-{ServiceKeys.Key(service)}";
}
