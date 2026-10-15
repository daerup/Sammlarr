using Contracts.Auth;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Data.Identity;

internal sealed class IdentityCookie(IServiceScopeFactory scopes) : ISignInCookie
{
	public async Task SignInAsync(UserId user, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var signIns = scope.ServiceProvider.GetRequiredService<SignInManager<UserRow>>();
		await signIns.SignInAsync(await signIns.UserManager.RequireAsync(user), isPersistent: false);
	}
}
