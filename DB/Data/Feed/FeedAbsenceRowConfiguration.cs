using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedAbsenceRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<FeedAbsenceRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn);
			builder.HasKey(x => new { x.FeedId, x.MediaId });
			builder.HasOne<FeedRow>().WithMany().HasForeignKey(x => x.FeedId).OnDelete(DeleteBehavior.Cascade);
			builder.HasOne<MediaRow>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Cascade);
		});
	}
}
