using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media.Metadata;

internal sealed class MediaMetadataRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MediaMetadataRow>(builder =>
		{
			builder.HasOne(m => m.Media).WithOne(media => media.Metadata).HasForeignKey<MediaMetadataRow>(m => m.MediaId).OnDelete(DeleteBehavior.Cascade);
			builder.HasDiscriminator(m => m.MediaType).HasValue<MovieMetadataRow>(MediaType.Movie).HasValue<ShowMetadataRow>(MediaType.Show);
		});

		modelBuilder.Entity<MediaRatingRow>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId", nameof(MediaRatingRow.Source));
			builder.HasOne(r => r.Metadata).WithMany(m => m.Ratings).HasForeignKey("MediaId").OnDelete(DeleteBehavior.Cascade);
			builder.HasIndex(r => r.Source);
		});
	}
}
