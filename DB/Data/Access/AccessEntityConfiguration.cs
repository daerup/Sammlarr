using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class AccessEntityConfiguration : IModelConfiguration
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
