using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ProviderConfigConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<ProviderConfigRow>(builder =>
		{
			builder.HasKey(row => row.Id);
			builder.Property(row => row.Id).ValueGeneratedNever();
			builder.OwnsOne(row => row.Radarr);
			builder.OwnsOne(row => row.Sonarr);
			builder.Navigation(row => row.Radarr).IsRequired();
			builder.Navigation(row => row.Sonarr).IsRequired();
		});
}
