using Contracts.Identity;
using Contracts.Messaging;
using DB.Scheduling;
using Domain.Identity;

namespace Web.Staging;

internal static class StagingServices
{
	extension(IServiceCollection services)
	{
		internal void AddStagingSeed(IConfiguration configuration)
		{
			services.AddSammlarrScheduling(configuration);
			services.AddScoped<ICurrentUser, SeedingUser>();
			services.AddScoped<IMessagePublisher, UnsentMessages>();
			services.AddSingleton<ISessionInvalidator, NoSessions>();
			services.AddSingleton<StagingRun>();
		}
	}

	private sealed class SeedingUser(CurrentUserOverride overrides) : ICurrentUser
	{
		private ICurrentUser Active => overrides.Value.GetValueOr(new Nobody());

		public bool IsAuthenticated => Active.IsAuthenticated;

		public UserId Id => Active.Id;

		public string DisplayName => Active.DisplayName;

		public string AvatarUrl => Active.AvatarUrl;

		public UserRole Role => Active.Role;
	}

	private sealed class Nobody : ICurrentUser
	{
		public bool IsAuthenticated => false;

		public UserId Id => throw new InvalidOperationException("Nobody is signed in to the staging seed yet.");

		public string DisplayName => string.Empty;

		public string AvatarUrl => string.Empty;

		public UserRole Role => UserRole.Member;
	}

	private sealed class UnsentMessages : IMessagePublisher
	{
		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull
		{
			return Task.CompletedTask;
		}
	}

	private sealed class NoSessions : ISessionInvalidator
	{
		public void Invalidate(UserId userId) { }
	}
}
