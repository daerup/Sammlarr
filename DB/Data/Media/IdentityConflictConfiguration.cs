using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class IdentityConflictConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<IdentityConflictRow>(builder =>
		{
			builder.HasKey(row => row.Id);
			builder.HasIndex(row => new { row.HeldId, row.OtherId }).IsUnique();
			builder.HasOne<MediaRow>().WithMany().HasForeignKey(row => row.HeldId).OnDelete(DeleteBehavior.Cascade);
			builder.HasOne<MediaRow>().WithMany().HasForeignKey(row => row.OtherId).OnDelete(DeleteBehavior.Cascade);
			builder.HasMany(row => row.Sources).WithOne().HasForeignKey(source => source.ConflictId).OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity<IdentityConflictSourceRow>(builder => builder.HasKey(source => new { source.ConflictId, source.Service }));
	}
}
