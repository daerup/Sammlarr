using System.Diagnostics;
using OpenTelemetry;

namespace Shared.Telemetry;

internal sealed class LayerTaggingProcessor : BaseProcessor<Activity>
{
	private const string LayerTag = "sammlarr.layer";

	public override void OnStart(Activity data)
	{
		data.SetTag(LayerTag, ResolveLayer(data.Source.Name));
	}

	private static string ResolveLayer(string sourceName) =>
		sourceName switch
		{
			SammlarrTelemetry.ServiceName => "app",
			var s when s.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) => "ui",
			var s when s.StartsWith("System.Net.Http", StringComparison.Ordinal) => "api",
			var s when s.Contains("EntityFrameworkCore", StringComparison.Ordinal) => "db",
			_ => "other",
		};
}
