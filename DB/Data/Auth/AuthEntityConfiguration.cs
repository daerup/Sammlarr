using Microsoft.EntityFrameworkCore;

namespace DB.Data.Auth;

internal sealed class AuthEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<CredentialRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn, nameof(CredentialRow.Provider)).IsUnique();
		});

		modelBuilder.Entity<ApiKeyConfigRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn, nameof(ApiKeyConfigRow.Provider)).IsUnique();
		});
	}
}
