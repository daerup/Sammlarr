using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace UI.Tests.Settings.About;

internal static class Registrations
{
	internal static Keyed<RemoteService, IExternalIdConverter> Converter(RemoteService remote, params IdLookup[] lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns(lookups);
		return new Keyed<RemoteService, IExternalIdConverter>(remote, converter.Object);
	}

	internal static Keyed<RemoteService, IDetailsEnricher> Enricher(RemoteService remote, MediaFields fills)
	{
		var enricher = IDetailsEnricher.Mock();
		enricher.Fills.Returns(fills);
		return new Keyed<RemoteService, IDetailsEnricher>(remote, enricher.Object);
	}
}
