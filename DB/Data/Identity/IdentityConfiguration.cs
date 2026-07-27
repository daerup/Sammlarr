using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal sealed class IdentityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) => modelBuilder.Entity<UserRow>().HasIndex(user => new { user.Provider, user.ExternalUserId }).IsUnique();
}
