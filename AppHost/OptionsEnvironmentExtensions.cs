using System.Globalization;
using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Shared.Options;

namespace AppHost;

internal static class OptionsEnvironmentExtensions
{
	public static IResourceBuilder<T> WithEnvironment<T, TOptions, TValue>(
		this IResourceBuilder<T> builder,
		Expression<Func<TOptions, TValue>> property,
		TValue value
	)
		where T : IResourceWithEnvironment
		where TOptions : IOptionsSection
	{
		var name = $"{TOptions.SectionName}:{((MemberExpression)property.Body).Member.Name}".Replace(":", "__", StringComparison.Ordinal);
		return builder.WithEnvironment(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
	}

	public static IResourceBuilder<T> WithConfiguration<T>(this IResourceBuilder<T> builder, IConfigurationSection section)
		where T : IResourceWithEnvironment
	{
		foreach (var (key, value) in section.AsEnumerable().Where(setting => setting.Value is not null))
		{
			builder.WithEnvironment(key.Replace(":", "__", StringComparison.Ordinal), value);
		}

		return builder;
	}
}
