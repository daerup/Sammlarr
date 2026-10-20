using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class ShowPartConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ShowPartRow>(builder =>
		{
			builder.HasKey(row => new { row.Id, row.Source });
			builder.HasOne(row => row.Media).WithMany().HasForeignKey(MediaQueries.MediaIdColumn).OnDelete(DeleteBehavior.Cascade);
		});
	}
}
