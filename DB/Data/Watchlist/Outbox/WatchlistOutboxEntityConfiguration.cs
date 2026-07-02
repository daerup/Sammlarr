using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Watchlist.Outbox;

internal sealed class WatchlistOutboxEntityConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WatchlistOutboxEntity>(builder =>
		{
			builder.Property<Guid>("MediaId");
			builder.HasKey("MediaId");
			builder.HasOne(o => o.Media).WithOne().HasForeignKey<WatchlistOutboxEntity>("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(o => o.Kind);
		});
}
