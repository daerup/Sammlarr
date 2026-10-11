using API.Http;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Search;
using Domain.Auth;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Http;

public class RemoteRegistrationTests
{
	[Test]
	public async Task Client_RecordsTheRemoteServiceItCalls()
	{
		var services = new ServiceCollection();

		services.ForRemote<WikidataOptions>(RemoteService.Wikidata).Client<PlainClient>();

		await Assert.That(Singletons<ClientOf>(services)).IsEquivalentTo([new ClientOf(RemoteService.Wikidata, typeof(PlainClient))]);
	}

	[Test]
	public async Task Converter_IsStoredWithItsRemoteService()
	{
		var services = new ServiceCollection();

		services.ForRemote<WikidataOptions>(RemoteService.Wikidata).Converter<Both>();

		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var stored = scope.ServiceProvider.GetServices<Keyed<RemoteService, IExternalIdConverter>>().Single();
		await Assert.That(stored.Key).IsEqualTo(RemoteService.Wikidata);
		await Assert.That(stored.Capability).IsTypeOf<Both>();
	}

	[Test]
	public async Task SearchAndEnricher_OfOneClass_ShareItsInstanceWithinAScope()
	{
		var services = new ServiceCollection();

		services.ForRemote<PlexOptions>(RemoteService.Plex).Search<Both>(SearchProvider.Plex).Enricher<Both>();

		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		object search = scope.ServiceProvider.GetRequiredKeyedService<IMediaSearchProvider>(SearchProvider.Plex);
		var enricher = scope.ServiceProvider.GetServices<Keyed<RemoteService, IDetailsEnricher>>().Single().Capability;
		await Assert.That(enricher).IsSameReferenceAs(search);
	}

	[Test]
	public async Task Feed_AnswersThroughItsKeyAndRecordsWhoServesIt()
	{
		var services = new ServiceCollection();

		services.ForRemote<MdblistOptions>(RemoteService.Mdblist).Feed<Both>(FeedProvider.Mdblist);

		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var feed = scope.ServiceProvider.GetRequiredKeyedService<IFeedProvider>(FeedProvider.Mdblist);
		await Assert.That(await feed.NormalizeAsync("owner/list")).IsSuccess().And.IsEqualTo("owner/list");
		await Assert
			.That(Singletons<Registered>(services))
			.IsEquivalentTo([new Registered(RemoteService.Mdblist, FeedProvider.Mdblist, FeedProvider.Mdblist.Service)]);
	}

	[Test]
	public async Task Credential_SignsInAndChecksTheCredentialWithOneInstanceAndRecordsWhoServesIt()
	{
		var services = new ServiceCollection();

		services.ForRemote<PlexOptions>(RemoteService.Plex).Credential<Both>(AuthProvider.Plex);

		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		object signIn = scope.ServiceProvider.GetRequiredKeyedService<IOAuthProvider>(AuthProvider.Plex);
		var validator = scope.ServiceProvider.GetServices<Keyed<AuthProvider, ICredentialValidator>>().Single();
		await Assert.That(validator.Key).IsEqualTo(AuthProvider.Plex);
		await Assert.That(validator.Capability).IsSameReferenceAs(signIn);
		await Assert.That(Singletons<Registered>(services)).IsEquivalentTo([new Registered(RemoteService.Plex, AuthProvider.Plex, AuthProvider.Plex.Service)]);
	}

	[Test]
	public async Task ForService_RecordsTheCompositeAsWhoServesTheRole()
	{
		var services = new ServiceCollection();

		services.ForService(new Domain.Providers.Arr()).Search<Both>(SearchProvider.Arr);

		await Assert
			.That(Singletons<Registered>(services))
			.IsEquivalentTo([new Registered(new Domain.Providers.Arr(), SearchProvider.Arr, SearchProvider.Arr.Service)]);
	}

	private static IEnumerable<T> Singletons<T>(IServiceCollection services)
	{
		return services.Where(descriptor => descriptor.ServiceType == typeof(T)).Select(descriptor => (T)descriptor.ImplementationInstance!);
	}

	private sealed class PlainClient(HttpClient client)
	{
		public HttpClient Client { get; } = client;
	}

	private sealed class Both : IExternalIdConverter, IDetailsEnricher, IMediaSearchProvider, IFeedProvider, IOAuthProvider, ICredentialValidator
	{
		public IReadOnlyList<IdLookup> Lookups => [];

		public MediaFields Fills => MediaFields.None;

		public MediaFields Defers => MediaFields.None;

		public IReadOnlyList<MediaProvider> Accepts => [];

		public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default) =>
			Task.FromResult<Answer<IdLookupResult>>(new NoMatch(RemoteService.Tmdb));

		public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default) =>
			Task.FromResult<Answer<FoundMedia>>(new NoMatch(RemoteService.Tmdb));

		public Task<IReadOnlyList<FoundMedia>> SearchAsync(
			string query,
			IReadOnlyCollection<MediaType> mediaTypes,
			CancellationToken cancellationToken = default
		) => Task.FromResult<IReadOnlyList<FoundMedia>>([]);

		public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default) =>
			Task.FromResult<Result<string>>(Success.Of(input));

		public IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default) =>
			AsyncEnumerable.Empty<Listed<MediaMatchResult>>();

		public Task<Result<string>> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default) =>
			Task.FromResult<Result<string>>(Error.From("none"));

		public Task<Result<ExternalLogin>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default) =>
			Task.FromResult<Result<ExternalLogin>>(Error.From("none"));

		public Task<AccessCheck> CheckAsync(Guid clientId, string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(AccessCheck.Valid);
	}
}
