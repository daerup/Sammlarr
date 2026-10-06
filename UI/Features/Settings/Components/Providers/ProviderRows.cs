using Domain.Providers;

namespace UI.Features.Settings.Components.Providers;

public union ProviderRowKey(MediaProvider, Arr, SammlarrAccounts)
{
	public static bool operator ==(ProviderRowKey left, ProviderRowKey right) => Equals(left.Value, right.Value);

	public static bool operator !=(ProviderRowKey left, ProviderRowKey right) => !Equals(left.Value, right.Value);

	public override bool Equals(object? obj) => obj is ProviderRowKey other && this == other;

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}

public readonly record struct Arr;

public readonly record struct SammlarrAccounts;

internal static class ProviderRows
{
	internal static string Id(ProviderRowKey key)
	{
		return key switch
		{
			MediaProvider provider => $"provider-row-{provider.ToString().ToLowerInvariant()}",
			Arr => "provider-row-arr",
			SammlarrAccounts => "provider-row-sammlarr",
		};
	}
}
