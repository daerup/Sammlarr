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
}
