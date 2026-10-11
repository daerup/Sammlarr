using System.Net;
using API.Http.Lanes;
using Contracts.Usage;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;
using Shared.Options;

namespace API.Http;

internal static class RemoteHttpExtensions
{
	internal static readonly TimeSpan LongestCall = TimeSpan.FromMinutes(10);

	public static IHttpClientBuilder AddClientHttp<TOptions>(this IHttpClientBuilder builder)
		where TOptions : class, IProviderHttpOptions
	{
		Configure<TOptions>(builder).AddStandardResilienceHandler().Configure((options, sp) => Resilience(options, sp.GetRequiredService<TOptions>().Http));
		return builder;
	}

	public static IHttpClientBuilder AddRemoteHttp<TOptions>(this IHttpClientBuilder builder, RemoteService remote)
		where TOptions : class, IProviderHttpOptions
	{
		builder.AddHttpMessageHandler(() => new UsageStampHandler(remote));
		Configure<TOptions>(builder).ConfigureHttpClient(client => client.Timeout = LongestCall).AddLanes<TOptions>(remote);
		builder.AddHttpMessageHandler(sp => new UsageTagsHandler(sp.GetRequiredService<IFeedRunUsage>()));
		builder.Services.AddSingleton(sp => new RemoteSettings(remote, sp.GetRequiredService<TOptions>()));
		return builder;
	}

	private static IHttpClientBuilder Configure<TOptions>(IHttpClientBuilder builder)
		where TOptions : class, IProviderHttpOptions
	{
		builder.ConfigureHttpClient(
			(sp, client) =>
			{
				var http = sp.GetRequiredService<TOptions>().Http;
				client.DefaultRequestVersion = ParseVersion(http.HttpVersion);
				client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
			}
		);
		builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
		{
			EnableMultipleHttp2Connections = sp.GetRequiredService<TOptions>().Http.EnableMultipleHttp2Connections,
		});
		return builder;
	}

	private static void Resilience(HttpStandardResilienceOptions options, ProviderHttpOptions http)
	{
		options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(http.AttemptTimeoutSeconds);
		options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(http.TimeoutSeconds);
		options.Retry.MaxRetryAttempts = Math.Max(1, http.RetryAttempts);
		options.Retry.UseJitter = true;
		options.Retry.ShouldHandle =
			http.RetryAttempts > 0
				? static args => new ValueTask<bool>(args.Outcome.Exception is not TimeoutRejectedException && HttpClientResiliencePredicates.IsTransient(args.Outcome))
				: static _ => new ValueTask<bool>(false);
	}

	private static Version ParseVersion(string version) =>
		version.Trim() switch
		{
			"1.0" => HttpVersion.Version10,
			"1.1" => HttpVersion.Version11,
			"3" or "3.0" => HttpVersion.Version30,
			_ => HttpVersion.Version20,
		};
}
