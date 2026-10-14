using System.Net;
using System.Security.Claims;
using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Web.Auth;
using Web.Licences;

namespace Web.Tests.Licences;

public class NoticesEndpointTests
{
	private const string Notices = "/THIRD-PARTY-NOTICES.txt";

	private readonly DirectoryInfo _published = Directory.CreateTempSubdirectory("sammlarr-notices-");

	[After(Test)]
	public void RemovePublished() => _published.Delete(recursive: true);

	[Test]
	public async Task Notices_ForASignedInMember_DownloadAsTheTextFile()
	{
		await PublishAsync("Cronos 0.11.1 MIT");
		await using var app = await StartAsync();
		using var client = Client(app);
		await SignInAsync(client);

		using var response = await client.GetAsync(new Uri(Notices, UriKind.Relative));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(response.Content.Headers.ContentType?.ToString()).IsEqualTo("text/plain; charset=utf-8");
		await Assert.That(response.Content.Headers.ContentDisposition?.DispositionType).IsEqualTo("attachment");
		await Assert.That(response.Content.Headers.ContentDisposition?.FileName).IsEqualTo("THIRD-PARTY-NOTICES.txt");
		await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("Cronos 0.11.1 MIT");
	}

	[Test]
	public async Task Notices_ForASignedOutVisitor_SendThemToSignInInstead()
	{
		await PublishAsync("Cronos 0.11.1 MIT");
		await using var app = await StartAsync();
		using var client = Client(app);

		using var response = await client.GetAsync(new Uri(Notices, UriKind.Relative));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
		await Assert.That(response.Headers.Location?.AbsolutePath).IsEqualTo("/signin");
	}

	[Test]
	public async Task Notices_InALocalBuildWithoutTheFile_AreNotFound()
	{
		await using var app = await StartAsync();
		using var client = Client(app);
		await SignInAsync(client);

		using var response = await client.GetAsync(new Uri(Notices, UriKind.Relative));

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
	}

	private Task PublishAsync(string notices) => File.WriteAllTextAsync(Path.Combine(_published.FullName, "THIRD-PARTY-NOTICES.txt"), notices);

	private async Task<WebApplication> StartAsync()
	{
		var sessions = ISessionValidator.Mock();
		sessions.IsValidAsync(Any(), Any(), Any(), Any()).Returns(true);
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Services.AddModule<AuthenticationModule>();
		builder.Services.AddSingleton(sessions.Object);
		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapGet("/sign-in", context => context.SignInAsync(Member()));
		app.MapNoticesEndpoint(_published.FullName);
		await app.StartAsync();
		return app;
	}

	private static ClaimsPrincipal Member()
	{
		Claim[] claims = [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, nameof(UserRole.Member))];
		return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
	}

	private static HttpClient Client(WebApplication app)
	{
		return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
	}

	private static async Task SignInAsync(HttpClient client)
	{
		using var signedIn = await client.GetAsync(new Uri("/sign-in", UriKind.Relative));
	}
}
