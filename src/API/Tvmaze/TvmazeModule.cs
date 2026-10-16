using API.Http;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Tvmaze;

internal sealed class TvmazeModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.ForRemote<TvmazeOptions>(RemoteService.Tvmaze).Client<TvmazeApi>(Configure).Converter<TvmazeIdConverter>().Enricher<TvmazeEnricher>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<TvmazeOptions>();
		client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
		client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
	}
}
