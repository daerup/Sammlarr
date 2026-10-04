using Microsoft.EntityFrameworkCore;

namespace DB.Data.Identity;

internal sealed class AccountLinkConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		var links = modelBuilder.Entity<AccountLinkRow>();
		links.ToTable("AccountLinks");
		links.HasIndex(link => link.TokenHash).IsUnique();
		links.HasIndex(link => new { link.Kind, link.Username }).IsUnique();
		links.HasOne<UserRow>().WithMany().HasForeignKey(link => link.UserId).OnDelete(DeleteBehavior.Cascade);
	}
}
