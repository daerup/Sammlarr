using System.Web;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.DI;

namespace API.Telemetry;

public class TelemetryModule : IServiceModule
{
	private static readonly string[] SensitiveQueryParams = ["api_key", "apikey", "key", "token"];

	public void RegisterServices(IServiceCollection services)
	{
		services
			.AddOpenTelemetry()
			.WithTracing(tracing =>
				tracing.AddHttpClientInstrumentation(opts =>
				{
					opts.EnrichWithHttpRequestMessage = RedactSensitiveQueryParams;
				})
			)
			.WithMetrics(metrics => metrics.AddHttpClientInstrumentation());
	}

	private static void RedactSensitiveQueryParams(System.Diagnostics.Activity activity, HttpRequestMessage request)
	{
		if (request.RequestUri is not { } uri || string.IsNullOrEmpty(uri.Query))
		{
			return;
		}

		var builder = new UriBuilder(uri);
		var query = HttpUtility.ParseQueryString(builder.Query);
		var redacted = false;
		foreach (var key in query.AllKeys)
		{
			if (key is not null && SensitiveQueryParams.Contains(key, StringComparer.OrdinalIgnoreCase))
			{
				query[key] = "REDACTED";
				redacted = true;
			}
		}

		if (!redacted)
		{
			return;
		}

		builder.Query = query.ToString() ?? string.Empty;
		var redactedUrl = builder.Uri.ToString();
		activity.SetTag("url.full", redactedUrl);
		activity.SetTag("http.url", redactedUrl);
	}
}
