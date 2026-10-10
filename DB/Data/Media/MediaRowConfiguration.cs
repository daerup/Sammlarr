using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder
			.Entity<ExternalMediaIdRow>()
			.HasKey(e => new
			{
				e.Source,
				e.MediaType,
				e.Id,
			});
		modelBuilder.Entity<ExternalMediaIdRow>().HasIndex("MediaId", nameof(ExternalMediaIdRow.Source)).IsUnique();
		modelBuilder.Entity<MediaRow>().HasMany(m => m.ExternalIds).WithOne(e => e.Media).OnDelete(DeleteBehavior.Cascade);
	}
}
