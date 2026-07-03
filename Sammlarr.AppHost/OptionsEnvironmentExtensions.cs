using System.Globalization;
using System.Linq.Expressions;
using Shared.Options;

namespace Sammlarr.AppHost;

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
		var name = $"{TOptions.SectionName}:{((MemberExpression)property.Body).Member.Name}".Replace(":", "__");
		return builder.WithEnvironment(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
	}
}
