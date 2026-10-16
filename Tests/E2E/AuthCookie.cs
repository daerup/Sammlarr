using System.Security.Claims;
using Harness;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests;

internal static class AuthCookie
{
	internal const string Name = "sammlarr.auth";

	internal static string Forge(string keyRingPath, SeededUser user, string securityStamp)
	{
		using var provider = new ServiceCollection()
			.AddDataProtection()
			.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
			.SetApplicationName("Sammlarr")
			.Services.BuildServiceProvider();
		var protector = provider
			.GetRequiredService<IDataProtectionProvider>()
			.CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", IdentityConstants.ApplicationScheme, "v2");

		var identity = new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Role, user.Role.ToString()),
				new Claim(new ClaimsIdentityOptions().SecurityStampClaimType, securityStamp),
			],
			IdentityConstants.ApplicationScheme
		);

		var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme);
		return new TicketDataFormat(protector).Protect(ticket);
	}
}
