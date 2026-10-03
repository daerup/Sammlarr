using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaDetailsRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MediaDetailsRow>(builder =>
		{
			builder.HasOne(m => m.Media).WithOne(media => media.Details).HasForeignKey<MediaDetailsRow>(m => m.MediaId).OnDelete(DeleteBehavior.Cascade);
			builder.HasDiscriminator(m => m.MediaType).HasValue<MovieDetailsRow>(MediaType.Movie).HasValue<ShowDetailsRow>(MediaType.Show);
		});

		modelBuilder.Entity<MediaRatingRow>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId", nameof(MediaRatingRow.Source));
			builder.HasOne(r => r.Details).WithMany(m => m.Ratings).HasForeignKey("MediaId").OnDelete(DeleteBehavior.Cascade);
			builder.HasIndex(r => r.Source);
		});
	}
}
