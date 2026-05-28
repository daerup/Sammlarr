using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class AccessEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<CredentialEntity>().HasIndex(c => c.WatchlistProvider).IsUnique();
		modelBuilder.Entity<ApiKeyConfigEntity>().HasIndex(s => s.Provider).IsUnique();
	}
}
