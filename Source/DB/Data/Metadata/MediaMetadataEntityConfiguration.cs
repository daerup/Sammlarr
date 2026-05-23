using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal sealed class MediaMetadataEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MediaMetadataEntity>(builder =>
		{
			builder.HasOne(m => m.Media).WithOne().HasForeignKey<MediaMetadataEntity>(m => m.MediaId).OnDelete(DeleteBehavior.Cascade);
			builder.HasDiscriminator(m => m.MediaType).HasValue<MovieMetadataEntity>(MediaType.Movie).HasValue<ShowMetadataEntity>(MediaType.Show);
		});

		modelBuilder.Entity<MediaRatingEntity>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId", nameof(MediaRatingEntity.Source));
			builder.HasOne(r => r.Metadata).WithMany(m => m.Ratings).HasForeignKey("MediaId").OnDelete(DeleteBehavior.Cascade);
		});
	}
}
