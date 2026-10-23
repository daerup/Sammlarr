using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Shared.DI;
using Web.Instrumentation;

namespace Web.Tests.Instrumentation;

public class JoinLinkSpanTests
{
	private const string Canary = "canary-join-token";

	[Test]
	public async Task Get_OfAJoinLink_LeavesNoSpanHoldingItsToken()
	{
		var spans = new ConcurrentQueue<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStopped = spans.Enqueue,
		};
		ActivitySource.AddActivityListener(listener);
		await using var app = await StartAsync();
		using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

		using var response = await client.GetAsync(new Uri($"/join/{Guid.NewGuid()}?token={Canary}", UriKind.Relative));
		await app.StopAsync();

		await Assert.That(spans).Contains(span => Equals(span.GetTagItem("http.route"), "/join/{user:guid}"));
		await Assert.That(spans.SelectMany(Texts).Where(text => text.Contains(Canary, StringComparison.Ordinal))).IsEmpty();
	}

	private static IEnumerable<string> Texts(Activity span)
	{
		return [span.DisplayName, span.OperationName, .. span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")];
	}

	private static async Task<WebApplication> StartAsync()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Services.AddModule<InstrumentationModule>();
		var app = builder.Build();
		app.MapGet("/join/{user:guid}", () => Results.Ok());
		await app.StartAsync();
		return app;
	}
}
