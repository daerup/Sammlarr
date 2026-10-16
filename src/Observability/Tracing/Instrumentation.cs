using System.Diagnostics;

namespace Observability.Tracing;

public static class Instrumentation
{
	public const string ServiceName = "Sammlarr";

	public static readonly ActivitySource Source = new(ServiceName);
}
