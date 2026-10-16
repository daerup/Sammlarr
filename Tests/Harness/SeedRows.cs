using DB;
using DB.Data.Auth;
using DB.Data.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace Harness;

internal static class SeedRows
{
	internal static void ApiKey(SammlarrDbContext context, ApiKeyProvider provider, string apiKey)
	{
		context.Set<ApiKeyConfigRow>().Add(new ApiKeyConfigRow { Provider = provider, ApiKey = apiKey });
		Save(context);
	}

	internal static void Credential(SammlarrDbContext context, AuthProvider provider, string token)
	{
		context
			.Set<CredentialRow>()
			.Add(
				new CredentialRow
				{
					Provider = provider,
					ClientId = Guid.NewGuid(),
					AccessToken = token,
					ExpiresAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
				}
			);
		Save(context);
	}

	internal static void PlexUser(SammlarrDbContext context, Guid id, string displayName, UserRole role, string avatarUrl, double hue)
	{
		var user = User(id, $"plex-{id}", displayName, avatarUrl);
		user.Hue = hue;
		Member(context, user, role, Some.Of(id.ToString()));
	}

	internal static UserRow User(Guid id, string userName, string displayName, string avatarUrl)
	{
		return new UserRow
		{
			Id = id,
			UserName = userName,
			NormalizedUserName = userName.ToUpperInvariant(),
			SecurityStamp = Guid.NewGuid().ToString(),
			ConcurrencyStamp = Guid.NewGuid().ToString(),
			LockoutEnabled = true,
			DisplayName = displayName,
			TagName = displayName,
			AvatarUrl = avatarUrl,
			JoinedAt = DateTimeOffset.UnixEpoch,
		};
	}

	internal static void Member(SammlarrDbContext context, UserRow user, UserRole role, Optional<string> plexId)
	{
		context.Users.Add(user);
		context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = RoleId(context, role) });
		plexId.Apply(key =>
			context.UserLogins.Add(
				new IdentityUserLogin<Guid>
				{
					LoginProvider = nameof(AuthProvider.Plex),
					ProviderKey = key,
					ProviderDisplayName = nameof(AuthProvider.Plex),
					UserId = user.Id,
				}
			)
		);
		Save(context);
	}

	internal static Guid RoleId(SammlarrDbContext context, UserRole role) => context.Roles.Single(found => found.Name == role.ToString()).Id;

	internal static void SetupFinished(SammlarrDbContext context)
	{
		context.ProviderSettings.Single().OnboardingCompletedAt = DateTimeOffset.UnixEpoch;
		Save(context);
	}

	internal static void SearchWith(SammlarrDbContext context, SearchProvider provider)
	{
		context.ProviderSettings.Single().SearchProvider = provider;
		Save(context);
	}

	internal static void WriteAheadLogging(SqliteConnection connection)
	{
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode=WAL;";
		command.ExecuteNonQuery();
	}

	private static void Save(SammlarrDbContext context)
	{
		context.SaveChanges();
		context.ChangeTracker.Clear();
	}
}
