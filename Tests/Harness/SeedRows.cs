using DB;
using DB.Data.Access;
using DB.Data.Identity;
using Domain.Identity;
using Domain.Providers;
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
		context.Users.Add(
			new UserRow
			{
				Id = id,
				Provider = AuthProvider.Plex,
				ExternalUserId = id.ToString(),
				DisplayName = displayName,
				TagName = displayName,
				AvatarUrl = avatarUrl,
				Role = role,
				JoinedAt = DateTimeOffset.UnixEpoch,
				Hue = hue,
			}
		);
		Save(context);
	}

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
