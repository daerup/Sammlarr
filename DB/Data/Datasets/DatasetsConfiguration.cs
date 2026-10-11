using Microsoft.EntityFrameworkCore;

namespace DB.Data.Datasets;

internal sealed class DatasetsConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ImdbRatingRow>(builder =>
		{
			builder.HasKey(row => row.Id);
			builder.Property(row => row.Id).ValueGeneratedNever();
		});
		modelBuilder.Entity<DatasetLoadRow>(builder => builder.HasKey(row => row.Name));
		modelBuilder.Entity<DatasetCheckRow>(builder => builder.HasKey(row => row.Name));
		modelBuilder.Entity<AnimeIdRow>(builder =>
		{
			builder.HasIndex(row => row.AniListId);
			builder.HasIndex(row => row.MalId);
			builder.HasIndex(row => row.TvdbId);
			builder.HasIndex(row => new { row.TmdbId, row.Type });
			builder.HasIndex(row => new { row.ImdbId, row.Type });
		});
	}
}
