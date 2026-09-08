using System.Security.Claims;
using Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests;

internal static class AuthCookie
{
	internal const string Name = "sammlarr.auth";

	internal static string Forge(string keyRingPath, Guid userId, string displayName, UserRole role)
	{
		var protector = new ServiceCollection()
			.AddDataProtection()
			.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
			.SetApplicationName("Sammlarr")
			.Services.BuildServiceProvider()
			.GetRequiredService<IDataProtectionProvider>()
			.CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", CookieAuthenticationDefaults.AuthenticationScheme, "v2");

		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, displayName), new Claim(ClaimTypes.Role, role.ToString())],
			CookieAuthenticationDefaults.AuthenticationScheme
		);

		var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), CookieAuthenticationDefaults.AuthenticationScheme);
		return new TicketDataFormat(protector).Protect(ticket);
	}
}
