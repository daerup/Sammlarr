using Contracts.Identity;
using Contracts.Messaging;
using DB;
using DB.Scheduling;
using Domain.Identity;

namespace Web.Staging;

internal static class StagingServices
{
	extension(IServiceCollection services)
	{
		internal IServiceCollection AddStagingSeed(IConfiguration configuration)
		{
			services.AddSammlarrScheduling(configuration);
			services.AddScoped<ICurrentUser, SeedingUser>();
			services.AddScoped<IMessagePublisher, UnsentMessages>();
			services.AddSingleton<ISessionInvalidator, NoSessions>();
			services.AddSingleton<StagingRun>();
			return services;
		}
	}

	private sealed class SeedingUser(CurrentUserOverride overrides) : ICurrentUser
	{
		private ICurrentUser Active => overrides.Value.GetValueOr(new AnonymousCurrentUser());

		public bool IsAuthenticated => Active.IsAuthenticated;

		public UserId Id => Active.Id;

		public string DisplayName => Active.DisplayName;

		public string AvatarUrl => Active.AvatarUrl;

		public UserRole Role => Active.Role;
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
