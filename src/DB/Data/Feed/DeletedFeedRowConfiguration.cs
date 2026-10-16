using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class DeletedFeedRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<DeletedFeedRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasKey(row => row.FeedId);
			builder.HasIndex(row => row.DeletedAt);
		});
	}
}
