using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ProviderConfigConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<ProviderConfigRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn);
		});
}
