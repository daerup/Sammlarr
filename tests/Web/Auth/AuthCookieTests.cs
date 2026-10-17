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

namespace Web.Tests.Auth;

public class AuthCookieTests
{
	private const string Page = "/page";

	[Test]
	public async Task Requests_WithAValidStamp_LeaveTheCookieAlone_SoALateOneCannotUndoASignOut()
	{
		await using var app = await StartAsync();
		using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(app.Urls.Single()) };
		using var signedIn = await client.GetAsync(new Uri("/sign-in", UriKind.Relative));
		using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Page, UriKind.Relative));
		request.Headers.Add("Cookie", signedIn.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);

		using var response = await client.SendAsync(request);

		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(response.Headers.Contains("Set-Cookie")).IsFalse();
	}

	private static async Task<WebApplication> StartAsync()
	{
		var stamps = ISecurityStampValidator.Mock();
		stamps.ValidateAsync(Any()).Callback(context => context.ShouldRenew = true);
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
		builder.Services.AddModule<AuthenticationModule>();
		builder.Services.AddSingleton(stamps.Object);
		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapGet("/sign-in", context => context.SignInAsync(Member()));
		app.MapGet(Page, () => Results.Ok()).RequireAuthorization();
		await app.StartAsync();
		return app;
	}

	private static ClaimsPrincipal Member()
	{
		Claim[] claims = [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, nameof(UserRole.Member))];
		return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
	}
}
