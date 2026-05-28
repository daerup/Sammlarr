using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist;

internal sealed class WatchlistEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WatchlistEntity>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId");
			builder.HasOne(w => w.Media).WithOne().HasForeignKey<WatchlistEntity>("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasOne(w => w.SourceFeed).WithMany().HasForeignKey(w => w.SourceFeedId).OnDelete(DeleteBehavior.SetNull);
			builder.HasIndex(w => w.SourceFeedId);
		});
}
