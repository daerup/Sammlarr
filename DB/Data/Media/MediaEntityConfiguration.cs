using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder
			.Entity<ExternalMediaIdEntity>()
			.HasKey(e => new
			{
				e.Source,
				e.MediaType,
				e.Id,
			});
		modelBuilder.Entity<MediaEntity>().HasMany(m => m.ExternalIds).WithOne(e => e.Media).OnDelete(DeleteBehavior.Cascade);
	}
}
