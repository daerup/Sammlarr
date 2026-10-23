using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Shared.DI;

namespace API.Observability;

public class InstrumentationModule : IServiceModule
{
	internal static readonly HttpRequestOptionsKey<bool> SecretUrl = new("sammlarr.secret-url");

	public void RegisterServices(IServiceCollection services)
	{
		services.AddOpenTelemetry().WithTracing(tracing => tracing.AddHttpClientInstrumentation(options => options.FilterHttpRequestMessage = IsTraced));
	}

	private static bool IsTraced(HttpRequestMessage request) => !request.Options.TryGetValue(SecretUrl, out var secret) || !secret;
}
