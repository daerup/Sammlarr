using Microsoft.EntityFrameworkCore;

namespace DB.Data.Wantlist.Outbox;

internal sealed class WantlistOutboxRowConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<WantlistOutboxRow>(builder =>
		{
			builder.HasUserOwner();
			builder.Property<Guid>("MediaId");
			builder.HasKey(UserOwnedConfigurationExtensions.UserIdColumn, "MediaId", nameof(WantlistOutboxRow.Sink));
			builder.HasOne(o => o.Media).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(o => o.Action);
		});
}
