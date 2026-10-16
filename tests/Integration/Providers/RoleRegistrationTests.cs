using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Providers;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RoleRegistrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IReadOnlyList<Registered> Registrations => [.. host.Resolve<IEnumerable<Registered>>()];

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Registered_EveryRole_IsServedByTheServiceThatRegistersIt()
	{
		await Assert.That(Registrations.Where(registered => registered.By != registered.Claimed)).IsEmpty();
	}

	[Test]
	public async Task Registered_EveryMemberOfAWholeRole_HasARegistration()
	{
		var registered = Registrations.Select(registration => registration.Role).ToHashSet();
		Type[] roles = [typeof(FeedProvider), typeof(TrendingProvider), typeof(SearchProvider), typeof(SinkKind), typeof(ApiKeyProvider)];

		await Assert.That(roles.SelectMany(role => Enum.GetValues(role).Cast<Enum>()).Where(member => !registered.Contains(member))).IsEmpty();
	}

	[Test]
	public async Task Resolve_EveryMemberOfAWholeRole_FindsItsService()
	{
		IEnumerable<object> services =
		[
			.. Enum.GetValues<FeedProvider>().Select(feed => host.ResolveKeyed<IFeedProvider>(feed)),
			.. Enum.GetValues<TrendingProvider>().Select(trending => host.ResolveKeyed<ITrendingService>(trending)),
			.. Enum.GetValues<SearchProvider>().Select(search => host.ResolveKeyed<IMediaSearchProvider>(search)),
			.. Enum.GetValues<SinkKind>().Select(sink => host.ResolveKeyed<IMediaSink>(sink)),
			.. Enum.GetValues<ApiKeyProvider>().Select(slot => host.ResolveKeyed<IApiKeyValidator>(slot)),
		];

		int[] members =
		[
			Enum.GetValues<FeedProvider>().Length,
			Enum.GetValues<TrendingProvider>().Length,
			Enum.GetValues<SearchProvider>().Length,
			Enum.GetValues<SinkKind>().Length,
			Enum.GetValues<ApiKeyProvider>().Length,
		];
		await Assert.That(services.Count()).IsEqualTo(members.Sum());
	}

	[Test]
	public async Task Clients_EveryRemoteService_HasOne()
	{
		var called = host.Resolve<IEnumerable<ClientOf>>().Select(client => client.Remote).ToHashSet();

		await Assert.That(Enum.GetValues<RemoteService>().Where(remote => !called.Contains(remote))).IsEmpty();
	}

	[Test]
	public async Task Converters_ThatSearchByHints_BelongToAServiceWithItsOwnIds()
	{
		var searching = host.Resolve<IEnumerable<Keyed<RemoteService, IExternalIdConverter>>>()
			.Where(converter => converter.Capability.Lookups.Any(lookup => lookup is ByHints))
			.Select(converter => converter.Key);

		await Assert.That(searching.Where(remote => remote.OwnIds.IsNone())).IsEmpty();
	}

	[Test]
	public async Task Enrichers_EveryRatingSource_HasOneThatFillsIt()
	{
		var fills = host.Resolve<IEnumerable<Keyed<RemoteService, IDetailsEnricher>>>()
			.Aggregate(MediaFields.None, (all, enricher) => all | enricher.Capability.Fills);

		await Assert.That(Enum.GetValues<RatingSource>().Where(source => !fills.Overlaps(MediaFields.Rated([source])))).IsEmpty();
	}

	[Test]
	public async Task Enrichers_FillOnlyTheRatingsTheirServiceSupplies()
	{
		var overreaching = host.Resolve<IEnumerable<Keyed<RemoteService, IDetailsEnricher>>>()
			.Where(enricher => !(enricher.Capability.Fills & MediaFields.AllRatings).Except(MediaFields.Rated(enricher.Key.SuppliedRatingSources())).IsEmpty)
			.Select(enricher => enricher.Key);

		await Assert.That(overreaching).IsEmpty();
	}
}
