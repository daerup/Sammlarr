using System.Net;
using System.Text;
using API.GitHub;
using Contracts.Updates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.GitHub;

public class GitHubReleaseFeedTests
{
	private readonly MockHttpHandler _github = Mock.HttpHandler();

	private GitHubReleaseFeed Testee => new(_github.CreateClient("https://api.github.com/"), NullLogger<GitHubReleaseFeed>.Instance);

	[Test]
	public async Task LatestAsync_ForAPublishedRelease_ReadsItsTagAndPage()
	{
		_github
			.OnGet("/repos/daerup/Sammlarr/releases/latest")
			.RespondWithJson("""{"tag_name":"v1.5.0","html_url":"https://github.com/daerup/Sammlarr/releases/tag/v1.5.0","name":"v1.5.0"}""");

		var latest = await Testee.LatestAsync("daerup/Sammlarr");

		await Assert.That(latest).IsSome().And.IsEqualTo(new PublishedRelease("v1.5.0", new Uri("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0")));
	}

	[Test]
	[Arguments(HttpStatusCode.NotFound)]
	[Arguments(HttpStatusCode.Forbidden)]
	[Arguments(HttpStatusCode.TooManyRequests)]
	public async Task LatestAsync_WhenGitHubRefuses_GivesNothing(HttpStatusCode status)
	{
		_github.WithDefaultStatus(status);

		await Assert.That(await Testee.LatestAsync("daerup/Sammlarr")).IsNone();
	}

	[Test]
	public async Task LatestAsync_WhenGitHubCannotBeReached_GivesNothing()
	{
		_github.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));

		await Assert.That(await Testee.LatestAsync("daerup/Sammlarr")).IsNone();
	}

	[Test]
	public async Task LatestAsync_ForAnAnswerThatIsNoRelease_GivesNothing()
	{
		_github.OnAnyRequest().RespondWithJson("""{"message":"Moved Permanently"}""");

		await Assert.That(await Testee.LatestAsync("daerup/Sammlarr")).IsNone();
	}

	[Test]
	public async Task LatestAsync_ForAnAnswerThatIsNoJson_GivesNothing()
	{
		_github
			.OnAnyRequest()
			.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Unicorn!</html>", Encoding.UTF8, "application/json") });

		await Assert.That(await Testee.LatestAsync("daerup/Sammlarr")).IsNone();
	}

	[Test]
	public async Task LatestAsync_ThroughItsRegisteredClient_NamesSammlarrAndAsksForGitHubsJson()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		await using var github = builder.Build();
		(string Agent, string Accept) sent = default;
		github.MapGet(
			"/repos/daerup/Sammlarr/releases/latest",
			(HttpRequest request) =>
			{
				sent = (request.Headers.UserAgent.ToString(), request.Headers.Accept.ToString());
				return Results.Text("""{"tag_name":"v1.5.0","html_url":"https://github.com/daerup/Sammlarr/releases/tag/v1.5.0"}""", "application/json");
			}
		);
		await github.StartAsync();
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(new ReleaseOptions { ApiUrl = $"{github.Urls.Single()}/" });
				services.AddModule<GitHubModule>();
			})
			.Build();

		await Assert.That(await host.Services.GetRequiredService<IReleaseFeed>().LatestAsync("daerup/Sammlarr")).IsSome();
		await Assert.That(sent.Agent).IsEqualTo("Sammlarr");
		await Assert.That(sent.Accept).IsEqualTo("application/vnd.github+json");
	}
}
