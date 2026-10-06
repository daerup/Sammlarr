using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Wantlist;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Observability.Tracing;

namespace API.Http;

internal sealed class ServiceRegistration(IServiceCollection services, Service by)
{
	public ServiceRegistration Feed<TFeed>(FeedProvider feed)
		where TFeed : class, IFeedProvider
	{
		services.TryAddScoped<TFeed>();
		services.AddTracedKeyedScoped<IFeedProvider>(feed, provider => provider.GetRequiredService<TFeed>());
		return Claims(feed, feed.Service);
	}

	public ServiceRegistration Trending<TTrending>(TrendingProvider trending)
		where TTrending : class, ITrendingService
	{
		return Forward<ITrendingService, TTrending>(trending).Claims(trending, trending.Service);
	}

	public ServiceRegistration Search<TSearch>(SearchProvider search)
		where TSearch : class, IMediaSearchProvider
	{
		return Forward<IMediaSearchProvider, TSearch>(search).Claims(search, search.Service);
	}

	public ServiceRegistration Sink<TSink>(SinkKind sink)
		where TSink : class, IMediaSink
	{
		return Forward<IMediaSink, TSink>(sink).Claims(sink, sink.Service);
	}

	public ServiceRegistration SignIn<TSignIn>(AuthProvider signIn)
		where TSignIn : class, IOAuthProvider
	{
		return Forward<IOAuthProvider, TSignIn>(signIn).Claims(signIn, signIn.Service);
	}

	private ServiceRegistration Forward<TCapability, TImplementation>(Enum role)
		where TCapability : class
		where TImplementation : class, TCapability
	{
		services.TryAddScoped<TImplementation>();
		services.AddKeyedScoped<TCapability>(role, (provider, _) => provider.GetRequiredService<TImplementation>());
		return this;
	}

	private ServiceRegistration Claims(Enum role, Service claimed)
	{
		services.AddSingleton(new Registered(by, role, claimed));
		return this;
	}
}
