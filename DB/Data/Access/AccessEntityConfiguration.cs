using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class AccessEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<CredentialRow>().HasIndex(c => c.WatchlistProvider).IsUnique();
		modelBuilder.Entity<ApiKeyConfigRow>().HasIndex(s => s.Provider).IsUnique();
	}
}
