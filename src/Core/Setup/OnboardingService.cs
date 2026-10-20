using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Setup;
using Core.Auth;
using Core.Notifications;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Core.Setup;

internal sealed class OnboardingService(
	SetupGate gate,
	IUserRepository users,
	ILocalAccountRepository accounts,
	ISetupStateRepository state,
	IProviderConfigService providerConfig,
	IApiKeyService apiKeys,
	IUserScope userScope,
	ICurrentUser currentUser,
	TimeProvider clock,
	WebUiNotificationSeeder webUiNotifications,
	ILogger<OnboardingService> logger
) : IOnboardingService
{
	private static readonly Error Taken = Error.From("This Sammlarr already has an admin. Sign in instead.");

	private static readonly Error WrongCode = Error.From("That is not the setup code.");

	public async Task<bool> NeedsAdminAsync(CancellationToken cancellationToken = default)
	{
		if (gate.AdminSeen)
		{
			return false;
		}

		var empty = await users.CountAsync(cancellationToken) == 0;
		if (!empty)
		{
			gate.SawAdmin();
		}

		return empty;
	}

	public async Task<Optional<Error>> CodeProblemAsync(string setupCode, CancellationToken cancellationToken = default)
	{
		var needsAdmin = await NeedsAdminAsync(cancellationToken);
		return Optional.When(!needsAdmin, () => Taken).Or(() => Optional.When(!gate.Accepts(setupCode), () => WrongCode));
	}

	public async Task<Result<User>> ClaimAsync(AdminClaim claim, CancellationToken cancellationToken = default)
	{
		using var lease = await gate.EnterAsync(cancellationToken);
		var problem = (await CodeProblemAsync(claim.SetupCode, cancellationToken))
			.Or(() => Usernames.Problem(claim.Username))
			.Or(() => Passwords.Mismatch(claim.Password, claim.Confirmation));
		return await problem.Match(error => error, () => CreateAdminAsync(claim, cancellationToken));
	}

	public async Task<bool> IsCompleteAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return (await state.CompletedAtAsync(cancellationToken)).IsSome();
	}

	public async Task CompleteAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await state.SetCompletedAtAsync(clock.GetUtcNow(), cancellationToken);
	}

	public async Task<bool> NeedsKeyPromptAsync(CancellationToken cancellationToken = default)
	{
		return currentUser.IsAuthenticated && currentUser.Role == UserRole.Member && !await apiKeys.IsStoredAsync(ApiKeyProvider.Tmdb, cancellationToken);
	}

	private async Task<Result<User>> CreateAdminAsync(AdminClaim claim, CancellationToken cancellationToken)
	{
		var admin = new User(UserId.New(), claim.Username.Trim(), string.Empty, UserRole.Admin, clock.GetUtcNow());
		var created = await accounts.CreateAsync(admin, claim.Username, claim.Password, cancellationToken);
		return await created.Map(found => SettleAdminAsync(found, cancellationToken));
	}

	private async Task<Result<User>> SettleAdminAsync(User admin, CancellationToken cancellationToken)
	{
		gate.SawAdmin();
		using (userScope.As(admin))
		{
			var config = await providerConfig.GetAsync(cancellationToken);
			await providerConfig.StoreAsync(config with { AuthProvider = AuthProvider.Local, RegistrationMode = RegistrationMode.Closed }, cancellationToken);
			await webUiNotifications.SeedAsync(cancellationToken);
		}

		logger.LogInformation("Setup created the admin {UserId} with a Sammlarr account", admin.Id);
		return Success.Of(admin);
	}
}
