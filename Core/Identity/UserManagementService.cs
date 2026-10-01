using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Settings;

namespace Core.Identity;

internal sealed class UserManagementService(
	IUserRepository users,
	ICurrentUser currentUser,
	IProviderConfigService providerConfig,
	ISessionValidator sessionValidator,
	ISessionInvalidator sessionInvalidator
) : IUserManagementService
{
	public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return await users.GetAllAsync(cancellationToken);
	}

	public async Task ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		if (role == UserRole.Member)
		{
			await EnsureNotLastAdminAsync(id, cancellationToken);
		}
		await users.ChangeRoleAsync(id, role, cancellationToken);
		sessionInvalidator.Invalidate(id);
	}

	public async Task DeleteAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		await EnsureNotLastAdminAsync(id, cancellationToken);
		await users.DeleteAsync(id, cancellationToken);
		sessionInvalidator.Invalidate(id);
	}

	public async Task SetRegistrationModeAsync(RegistrationMode mode, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		var config = await providerConfig.GetAsync(cancellationToken);
		await providerConfig.StoreAsync(config with { RegistrationMode = mode }, cancellationToken);
	}

	private async Task EnsureCallerIsAdminAsync(CancellationToken cancellationToken)
	{
		if (await sessionValidator.IsCurrentAsync(currentUser.Id, UserRole.Admin, cancellationToken) is false)
		{
			throw new UnauthorizedAccessException("This action requires the Admin role.");
		}
	}

	private async Task EnsureNotLastAdminAsync(UserId id, CancellationToken cancellationToken)
	{
		var target = await users.GetAsync(id, cancellationToken);
		if (target.Role == UserRole.Admin && (await users.GetAllAsync(cancellationToken)).Count(user => user.Role == UserRole.Admin) <= 1)
		{
			throw new InvalidOperationException("Cannot remove or demote the last admin.");
		}
	}
}
