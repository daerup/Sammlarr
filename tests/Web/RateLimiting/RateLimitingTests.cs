using System.Net;
using System.Security.Claims;
using Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Web.Auth;
using Web.RateLimiting;

namespace Web.Tests.RateLimiting;

public class RateLimitingTests
{
	private const string SignInForm = "/sign-in-form";

	private const string Page = "/page";

	[Test]
	public async Task SignInForms_PostedMoreThanTenTimesInAMinute_AnswerTooManyRequests()
	{
		await using var app = await StartAsync();
		using var client = Client(app);

		for (var attempt = 0; attempt < RateLimitingModule.SignInFormsPerMinute; attempt++)
		{
			using var accepted = await client.PostAsync(new Uri(SignInForm, UriKind.Relative), null);
			await Assert.That(accepted.StatusCode).IsEqualTo(HttpStatusCode.OK);
		}

		using var refused = await client.PostAsync(new Uri(SignInForm, UriKind.Relative), null);

		await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
		await Assert.That(refused.Headers.RetryAfter?.Delta).IsEqualTo(TimeSpan.FromMinutes(1));
		await Assert.That(await refused.Content.ReadAsStringAsync()).IsEqualTo(RateLimitingModule.TooManyRequests);
	}

	[Test]
	public async Task Requests_WhenAnAddressHasSpentItsBurst_StillReachASignedInUser()
	{
		await using var app = await StartAsync();
		using var member = Client(app);
		using var signedIn = await member.GetAsync(new Uri("/sign-in", UriKind.Relative));
		using var visitor = Client(app);

		var refused = false;
		for (var request = 0; request < RateLimitingModule.RequestBurst * 2 && !refused; request++)
		{
			using var response = await visitor.GetAsync(new Uri(Page, UriKind.Relative));
			refused = response.StatusCode == HttpStatusCode.TooManyRequests;
		}

		using var memberPage = await member.GetAsync(new Uri(Page, UriKind.Relative));

		await Assert.That(refused).IsTrue();
		await Assert.That(memberPage.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	private static async Task<WebApplication> StartAsync()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
		builder.Services.AddModule<AuthenticationModule>();
		builder.Services.AddModule<RateLimitingModule>();
		builder.Services.AddSingleton(ISecurityStampValidator.Mock().Object);
		var app = builder.Build();
		app.UseAuthentication();
		app.UseRateLimiter();
		app.UseAuthorization();
		app.MapGet("/sign-in", context => context.SignInAsync(Member()));
		app.MapGet(Page, () => Results.Ok());
		app.MapPost(SignInForm, () => Results.Ok()).RequireRateLimiting(RateLimitingModule.SignInForms);
		await app.StartAsync();
		return app;
	}

	private static ClaimsPrincipal Member()
	{
		Claim[] claims = [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, nameof(UserRole.Member))];
		return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
	}

	private static HttpClient Client(WebApplication app)
	{
		return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
	}
}
