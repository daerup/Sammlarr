using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Shared.DI;

namespace UI.Observability;

internal class InstrumentationModule : IServiceModule
{
	private static readonly string[] AssetExtensions = [".css", ".js", ".map", ".png", ".ico", ".svg", ".woff", ".woff2", ".webp", ".jpg", ".gif"];

	public void RegisterServices(IServiceCollection services)
	{
		services
			.AddOpenTelemetry()
			.WithTracing(tracing => tracing.SetSampler(new ExcludeHubNoiseSampler()).AddAspNetCoreInstrumentation(options => options.Filter = IsMeaningfulRequest));

		services.Configure<HubOptions>(o => o.AddFilter(new BlazorHubActivityFilter()));
	}

	private static bool IsMeaningfulRequest(HttpContext context)
	{
		var path = context.Request.Path.Value ?? string.Empty;
		if (
			path.StartsWith("/_blazor", StringComparison.Ordinal)
			|| path.StartsWith("/_framework", StringComparison.Ordinal)
			|| path.StartsWith("/_content", StringComparison.Ordinal)
		)
		{
			return false;
		}
		return !AssetExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
	}
}

file sealed class ExcludeHubNoiseSampler : Sampler
{
	private static readonly AlwaysOnSampler On = new();
	private static readonly SamplingResult Dropped = new(SamplingDecision.Drop);

	public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
		IsSignalRHub(samplingParameters) ? Dropped : On.ShouldSample(samplingParameters);

	private static bool IsSignalRHub(in SamplingParameters samplingParameters)
	{
		if (samplingParameters.Name.Contains("Hub", StringComparison.Ordinal))
		{
			return true;
		}
		if (samplingParameters.Tags is null)
		{
			return false;
		}
		foreach (var tag in samplingParameters.Tags)
		{
			if (tag.Key == "rpc.system")
			{
				return true;
			}
		}
		return false;
	}
}
