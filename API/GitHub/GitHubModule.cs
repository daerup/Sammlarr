using System.Net.Http.Headers;
using API.Http;
using Contracts.Updates;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.GitHub;

internal sealed class GitHubModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<IReleaseFeed, GitHubReleaseFeed>(Configure).AddClientHttp<ReleaseOptions>();
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		var options = sp.GetRequiredService<ReleaseOptions>();
		client.BaseAddress = new Uri(options.ApiUrl);
		client.Timeout = TimeSpan.FromSeconds(options.Http.TimeoutSeconds);
		client.DefaultRequestHeaders.UserAgent.ParseAdd("Sammlarr");
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
	}
}
