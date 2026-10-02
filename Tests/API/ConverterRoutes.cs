using Contracts.Media.Conversions;
using Domain.Providers;

namespace API.Tests;

internal static class ConverterRoutes
{
	internal static IdRoute From(this IExternalIdConverter converter, MediaProvider source) =>
		converter.Routes.Single(route => route.From is Some<MediaProvider>(var from) && from == source);

	internal static IdRoute ByHints(this IExternalIdConverter converter) => converter.Routes.Single(route => route.From is None);
}
