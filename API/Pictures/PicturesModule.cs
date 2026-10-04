using API.Http;
using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Pictures;

internal sealed class PicturesModule : IServiceModule
{
	private const int MostBytes = 4 * 1024 * 1024;

	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<IPictureGrids, PictureGrids>(Configure).AddProviderHttp<PictureOptions>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<PictureOptions>().Http.TotalTimeoutSeconds);
		client.MaxResponseContentBufferSize = MostBytes;
	}
}
