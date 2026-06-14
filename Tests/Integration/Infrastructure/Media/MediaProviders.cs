using System.ComponentModel;
using System.Reflection;
using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

internal static class MediaProviders
{
	public static MediaProvider Parse(string raw)
	{
		foreach (var provider in Enum.GetValues<MediaProvider>())
		{
			if (provider.ToString().Equals(raw, StringComparison.OrdinalIgnoreCase))
				return provider;
			var description = typeof(MediaProvider).GetField(provider.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description;
			if (description is not null && description.Equals(raw, StringComparison.OrdinalIgnoreCase))
				return provider;
		}
		throw new ArgumentException($"Unknown MediaProvider token '{raw}'.");
	}

	public static (MediaProvider Provider, string Value) ParseToken(string token)
	{
		var parts = token.Split(':', 2);
		if (parts.Length != 2)
			throw new InvalidOperationException($"Id token '{token}' must be written as 'provider:value'.");
		return (Parse(parts[0].Trim()), parts[1].Trim());
	}

	public static IReadOnlyList<MediaProvider> ParseList(IReadOnlyList<string>? tokens)
	{
		if (tokens is null || tokens.Count == 0)
			throw new InvalidOperationException("A 'providers' column listing at least one provider is required.");
		return [.. tokens.Select(Parse)];
	}
}
