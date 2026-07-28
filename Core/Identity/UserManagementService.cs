using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Settings;

namespace Core.Identity;

internal sealed class UserManagementService(
	IUserRepository users,
	ICurrentUser currentUser,
	IProviderConfigRepository providerConfig,
	ISessionValidator sessionValidator,
	ISessionInvalidator sessionInvalidator
) : IUserManagementService
{
	public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return users.GetAllAsync(cancellationToken);
	}

	public async Task ChangeRoleAsync(UserId userId, UserRole role, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		if (role == UserRole.Member)
		{
			await EnsureNotLastAdminAsync(userId, cancellationToken);
		}
		await users.ChangeRoleAsync(userId, role, cancellationToken);
		sessionInvalidator.Invalidate(userId);
	}

	public async Task RemoveAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		await EnsureCallerIsAdminAsync(cancellationToken);
		await EnsureNotLastAdminAsync(userId, cancellationToken);
		await users.DeleteAsync(userId, cancellationToken);
		sessionInvalidator.Invalidate(userId);
	}

	public async ValueTask<RegistrationMode> GetRegistrationModeAsync(CancellationToken cancellationToken = default) =>
		(await providerConfig.GetAsync(cancellationToken)).RegistrationMode;

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

	private async Task EnsureNotLastAdminAsync(UserId userId, CancellationToken cancellationToken)
	{
		var target = await users.GetAsync(userId, cancellationToken);
		if (target.Role == UserRole.Admin && await users.CountByRoleAsync(UserRole.Admin, cancellationToken) <= 1)
		{
			throw new InvalidOperationException("Cannot remove or demote the last admin.");
		}
	}
}
