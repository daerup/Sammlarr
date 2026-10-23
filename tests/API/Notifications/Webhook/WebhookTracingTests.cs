using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using API.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Shared.DI;

namespace API.Tests.Notifications.Webhook;

public class WebhookTracingTests
{
	private const string Canary = "canary-webhook-path";

	[Test]
	public async Task SendAsync_ForARequestWhoseUrlIsASecret_ExportsNoSpanButOthersStillDo()
	{
		var exported = new ConcurrentQueue<Activity>();
		await using var provider = Tracing(exported);
		using var client = new HttpClient();
		var address = $"http://127.0.0.1:{ClosedPort()}";
		using var secret = new HttpRequestMessage(HttpMethod.Post, $"{address}/hook/{Canary}");
		secret.Options.Set(InstrumentationModule.SecretUrl, true);
		using var plain = new HttpRequestMessage(HttpMethod.Get, $"{address}/plain");

		await Assert.That(async () => await SendAsync(client, secret)).Throws<HttpRequestException>();
		await Assert.That(async () => await SendAsync(client, plain)).Throws<HttpRequestException>();

		var urls = exported.Select(span => $"{span.GetTagItem("url.full")}").ToList();
		await Assert.That(urls).Contains(url => url.EndsWith("/plain", StringComparison.Ordinal));
		await Assert.That(urls.Where(url => url.Contains(Canary, StringComparison.Ordinal))).IsEmpty();
	}

	private static async Task SendAsync(HttpClient client, HttpRequestMessage request)
	{
		using var response = await client.SendAsync(request);
	}

	private static ServiceProvider Tracing(ConcurrentQueue<Activity> exported)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddModule<InstrumentationModule>();
		services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(new Exported(exported)));
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<TracerProvider>();
		return provider;
	}

	private static int ClosedPort()
	{
		using var probe = new TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		return ((IPEndPoint)probe.LocalEndpoint).Port;
	}

	private sealed class Exported(ConcurrentQueue<Activity> spans) : BaseProcessor<Activity>
	{
		public override void OnEnd(Activity data) => spans.Enqueue(data);
	}
}
