using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Wantlist;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Options;

namespace API.Http;

internal sealed class RemoteRegistration<TOptions>(IServiceCollection services, RemoteService remote)
	where TOptions : class, IProviderHttpOptions
{
	private readonly ServiceRegistration _roles = new(services, remote);

	public RemoteRegistration<TOptions> Client<TClient>()
		where TClient : class
	{
		return Client<TClient>((_, _) => { });
	}

	public RemoteRegistration<TOptions> Client<TClient>(Action<IServiceProvider, HttpClient> configure)
		where TClient : class
	{
		services.AddHttpClient<TClient>(configure).AddRemoteHttp<TOptions>(remote);
		services.AddSingleton(new ClientOf(remote, typeof(TClient)));
		return this;
	}

	public RemoteRegistration<TOptions> Client(string name)
	{
		return Client(name, (_, _) => { });
	}

	public RemoteRegistration<TOptions> Client(string name, Action<IServiceProvider, HttpClient> configure)
	{
		services.AddHttpClient(name, configure).AddRemoteHttp<TOptions>(remote);
		return this;
	}

	public RemoteRegistration<TOptions> Converter<TConverter>()
		where TConverter : class, IExternalIdConverter
	{
		return Store<IExternalIdConverter, TConverter>();
	}

	public RemoteRegistration<TOptions> Enricher<TEnricher>()
		where TEnricher : class, IDetailsEnricher
	{
		return Store<IDetailsEnricher, TEnricher>();
	}

	public RemoteRegistration<TOptions> Feed<TFeed>(FeedProvider feed)
		where TFeed : class, IFeedProvider
	{
		_roles.Feed<TFeed>(feed);
		return this;
	}

	public RemoteRegistration<TOptions> Trending<TTrending>(TrendingProvider trending)
		where TTrending : class, ITrendingService
	{
		_roles.Trending<TTrending>(trending);
		return this;
	}

	public RemoteRegistration<TOptions> Search<TSearch>(SearchProvider search)
		where TSearch : class, IMediaSearchProvider
	{
		_roles.Search<TSearch>(search);
		return this;
	}

	public RemoteRegistration<TOptions> Sink<TSink>(SinkKind sink)
		where TSink : class, IMediaSink
	{
		_roles.Sink<TSink>(sink);
		return this;
	}

	public RemoteRegistration<TOptions> Credential<TSignIn>(AuthProvider signIn)
		where TSignIn : class, IOAuthProvider, ICredentialValidator
	{
		_roles.Credential<TSignIn>(signIn);
		return this;
	}

	public RemoteRegistration<TOptions> ApiKey<TValidator>(ApiKeyProvider slot)
		where TValidator : class, IApiKeyValidator
	{
		_roles.ApiKey<TValidator>(slot);
		return this;
	}

	private RemoteRegistration<TOptions> Store<TCapability, TImplementation>()
		where TCapability : class
		where TImplementation : class, TCapability
	{
		services.TryAddScoped<TImplementation>();
		services.AddScoped(provider => new Keyed<RemoteService, TCapability>(remote, provider.GetRequiredService<TImplementation>()));
		return this;
	}
}

internal static class Registrations
{
	extension(IServiceCollection services)
	{
		public RemoteRegistration<TOptions> ForRemote<TOptions>(RemoteService remote)
			where TOptions : class, IProviderHttpOptions
		{
			return new RemoteRegistration<TOptions>(services, remote);
		}

		public ServiceRegistration ForService(Service service) => new(services, service);
	}
}
